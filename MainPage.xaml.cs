using StrataVelyx.ViewModels;
using StrataVelyx.Services;
using System.Text.Json;

namespace StrataVelyx;

public partial class MainPage : ContentPage
{
	private EnhancedMainViewModel? _viewModel;
	private WebViewBridgeService? _bridgeService;
	private bool _isDrawingMode = false;
	private System.Timers.Timer? _messagePollTimer;

	public MainPage()
	{
		try
		{
			InitializeComponent();
			
			_viewModel = new EnhancedMainViewModel();
			_bridgeService = new WebViewBridgeService();
			_viewModel.BridgeService = _bridgeService;
			
			BindingContext = _viewModel;
			
			// Bind UI elements to ViewModel
			if (ChatEntry != null)
				ChatEntry.SetBinding(Entry.TextProperty, nameof(MainViewModel.ChatInput));
			if (StatusLabel != null)
				StatusLabel.SetBinding(Label.TextProperty, nameof(MainViewModel.StatusMessage));
			
			// Initialize map when loaded
			Loaded += OnPageLoaded;
			
			// Setup bridge event handlers
			SetupBridgeHandlers();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"MainPage Constructor Error: {ex}");
		}
	}
	
	private void SetupBridgeHandlers()
	{
		if (_bridgeService == null) return;
		
		_bridgeService.MapReady += (s, e) =>
		{
			Dispatcher.Dispatch(() =>
			{
				StatusLabel.Text = "Map ready";
				AddChatMessage("System", "Map loaded and ready");
			});
		};
		
		_bridgeService.FeatureClick += (s, e) =>
		{
			Dispatcher.Dispatch(() =>
			{
				StatusLabel.Text = $"Clicked feature: {e.FeatureId}";
				AddChatMessage("Map", $"Feature clicked: {e.FeatureId} at [{e.Coordinates[0]:F4}, {e.Coordinates[1]:F4}]");
			});
		};
		
		_bridgeService.MapClick += (s, e) =>
		{
			Dispatcher.Dispatch(() =>
			{
				StatusLabel.Text = $"Map clicked: [{e.Coordinates[0]:F4}, {e.Coordinates[1]:F4}]";
			});
		};
		
		_bridgeService.PolygonDrawn += (s, e) =>
		{
			Dispatcher.Dispatch(() =>
			{
				StatusLabel.Text = $"Polygon drawn with {e.Coordinates.Count} points";
				AddChatMessage("Map", $"Polygon drawn: {e.Coordinates.Count} points, area: {e.Area:F2}");
				_isDrawingMode = false;
			});
		};
	}
	
	private async void OnPageLoaded(object? sender, EventArgs e)
	{
		try
		{
			if (MapWebView == null || _bridgeService == null) return;
			
			// Load the map HTML from resources
			var htmlSource = new HtmlWebViewSource();
			try
			{
				using var stream = await FileSystem.OpenAppPackageFileAsync("map.html");
				using var reader = new StreamReader(stream);
				var html = await reader.ReadToEndAsync();
				htmlSource.Html = html;
			}
			catch
			{
				// Fallback: use inline HTML if file not found
				htmlSource.Html = GetInlineMapHtml();
			}
			
			MapWebView.Source = htmlSource;
			_bridgeService.SetWebView(MapWebView);
			
			// Setup platform-specific message handling
			SetupPlatformMessageHandling();
			
			// Start polling for messages (for platforms without direct message handlers)
			StartMessagePolling();
			
			StatusLabel.Text = "Loading map...";
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"OnPageLoaded Error: {ex}");
			StatusLabel.Text = $"Error loading map: {ex.Message}";
		}
	}
	
	private string GetInlineMapHtml()
	{
		// Return a minimal HTML if file loading fails
		// This is a fallback - the actual HTML should be loaded from Resources/Raw/map.html
		return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Map</title>
    <script src='https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js'></script>
    <link href='https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css' rel='stylesheet' />
    <script src='https://unpkg.com/@maplibre/maplibre-gl-draw@1.3.0/dist/maplibre-gl-draw.js'></script>
    <link href='https://unpkg.com/@maplibre/maplibre-gl-draw@1.3.0/dist/maplibre-gl-draw.css' rel='stylesheet' />
    <style>body { margin: 0; } #map { width: 100%; height: 100vh; }</style>
</head>
<body>
    <div id='map'></div>
    <script>
        const map = new maplibregl.Map({ container: 'map', style: 'https://demotiles.maplibre.org/style.json', center: [-95.37, 29.76], zoom: 10 });
        const draw = new MapLibreDraw({ displayControlsDefault: false, controls: { polygon: true, trash: true } });
        map.addControl(draw);
        window.mapBridge = { addLayer: () => {}, removeLayer: () => {}, setLayerStyle: () => {}, zoomToBounds: () => {}, setDrawingMode: () => {} };
        window.mapMessageQueue = [];
        window.getMapMessages = () => { const m = window.mapMessageQueue.slice(); window.mapMessageQueue = []; return JSON.stringify(m); };
        function postMessageToCSharp(msg) { window.mapMessageQueue.push(JSON.stringify(msg)); if (window.mapMessageQueue.length > 100) window.mapMessageQueue.shift(); }
        map.on('load', () => postMessageToCSharp({ type: 'mapReady' }));
        map.on('click', (e) => { const f = map.queryRenderedFeatures(e.point); if (f.length > 0) postMessageToCSharp({ type: 'featureClick', featureId: f[0].properties?.id || 'unknown', coordinates: [e.lngLat.lng, e.lngLat.lat], properties: f[0].properties }); else postMessageToCSharp({ type: 'mapClick', coordinates: [e.lngLat.lng, e.lngLat.lat] }); });
        map.on('draw.create', (e) => { const c = e.features[0].geometry.coordinates[0]; postMessageToCSharp({ type: 'polygonDrawn', coordinates: c }); });
    </script>
</body>
</html>";
	}
	
	private void StartMessagePolling()
	{
		_messagePollTimer = new System.Timers.Timer(100); // Poll every 100ms
		_messagePollTimer.Elapsed += async (s, e) =>
		{
			if (MapWebView == null || _bridgeService == null) return;
			
			try
			{
				// Get messages from JavaScript queue
				var result = await MapWebView.EvaluateJavaScriptAsync("window.getMapMessages ? window.getMapMessages() : '[]'");
				
				if (!string.IsNullOrEmpty(result) && result != "[]" && result != "null")
				{
					// Parse messages array
					var messages = JsonSerializer.Deserialize<string[]>(result);
					if (messages != null)
					{
						foreach (var message in messages)
						{
							Dispatcher.Dispatch(() =>
							{
								_bridgeService?.HandleMessage(message);
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				// Silently handle errors - polling will continue
				System.Diagnostics.Debug.WriteLine($"Message polling error: {ex.Message}");
			}
		};
		_messagePollTimer.Start();
	}
	
	private void SetupPlatformMessageHandling()
	{
#if WINDOWS
		MapWebView.WebMessageReceived += (s, e) =>
		{
			if (_bridgeService != null)
			{
				_bridgeService.HandleMessage(e.WebMessageAsJson);
			}
		};
#elif MACCATALYST || IOS
		// For iOS/MacCatalyst, we'll use a polling approach or JavaScript injection
		// The HTML page will use console.log which we can capture
		// For now, we'll rely on JavaScript evaluation callbacks
#elif ANDROID
		// Android WebView message handling would go here
		// For now, we'll use JavaScript evaluation
#endif
		
		// Alternative: Use JavaScript to call back to C# via EvaluateJavaScriptAsync
		// This requires injecting a callback function that C# can poll or listen to
	}
	
	private async void OnImportClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.ImportWellsCommand?.CanExecute(null) == true)
			{
				_viewModel.ImportWellsCommand.Execute(null);
				// Update layers panel after import
				UpdateLayersPanel();
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnImportPolygonsClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.ImportPolygonsCommand?.CanExecute(null) == true)
			{
				_viewModel.ImportPolygonsCommand.Execute(null);
				// Update layers panel after import
				UpdateLayersPanel();
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private void UpdateLayersPanel()
	{
		// Layers are bound to ViewModel.Layers ObservableCollection
		// This will update automatically via binding
	}
	
	private async void OnNewProjectClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.NewProjectCommand?.CanExecute(null) == true)
			{
				_viewModel.NewProjectCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnSaveProjectClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.SaveProjectCommand?.CanExecute(null) == true)
			{
				_viewModel.SaveProjectCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnLoadProjectClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.LoadProjectCommand?.CanExecute(null) == true)
			{
				_viewModel.LoadProjectCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.ToggleBasemapSelectorAsync();
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	
	private async void OnSatelliteBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.SetBasemapAsync("satellite");
				StatusLabel.Text = "Basemap: Satellite";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnOSMBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.SetBasemapAsync("osm");
				StatusLabel.Text = "Basemap: OpenStreetMap";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnTopoBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.SetBasemapAsync("topo");
				StatusLabel.Text = "Basemap: Topographic";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnDarkBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.SetBasemapAsync("dark");
				StatusLabel.Text = "Basemap: Dark";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnLightBasemapClicked(object sender, EventArgs e)
	{
		try
		{
			if (_bridgeService != null)
			{
				await _bridgeService.SetBasemapAsync("light");
				StatusLabel.Text = "Basemap: Light";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnStatisticsClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.CurrentProject != null)
			{
				var project = _viewModel.CurrentProject;
				var stats = $"Project Statistics:\n\n" +
				           $"Project: {project.Name}\n" +
				           $"Wells: {project.WellCount}\n" +
				           $"Polygons: {project.PolygonCount}\n" +
				           $"Created: {project.Created:yyyy-MM-dd}\n" +
				           $"Modified: {project.LastModified:yyyy-MM-dd}\n" +
				           $"\nUse Tools menu for spatial operations.";
				await DisplayAlert("Statistics", stats, "OK");
			}
			else
			{
				await DisplayAlert("Statistics", "No project loaded. Import data to see statistics.", "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnAboutClicked(object sender, EventArgs e)
	{
		await DisplayAlert("About StrataVelyx", 
			"StrataVelyx - Professional GIS for Reservoir Management\n\n" +
			"Version 1.0\n" +
			"Built with .NET MAUI & MapLibre GL\n\n" +
			"Features:\n" +
			"• Global map with multiple basemaps\n" +
			"• Spatial analysis engine\n" +
			"• Data import with validation\n" +
			"• Project management", 
			"OK");
	}
	
	private async void OnFileMenuClicked(object sender, EventArgs e)
	{
		var action = await DisplayActionSheet("File", "Cancel", null,
			"📁 Import Wells",
			"📐 Import Polygons",
			"💾 Export Data",
			"",
			"📋 New Project",
			"💾 Save Project",
			"📂 Open Project");
		
		if (action == "📁 Import Wells") OnImportClicked(sender, e);
		else if (action == "📐 Import Polygons") OnImportPolygonsClicked(sender, e);
		else if (action == "💾 Export Data") OnExportClicked(sender, e);
		else if (action == "📋 New Project") OnNewProjectClicked(sender, e);
		else if (action == "💾 Save Project") OnSaveProjectClicked(sender, e);
		else if (action == "📂 Open Project") OnLoadProjectClicked(sender, e);
	}
	
	private async void OnMapMenuClicked(object sender, EventArgs e)
	{
		var action = await DisplayActionSheet("Map", "Cancel", null,
			"🗺️ Change Basemap",
			"✏️ Draw Polygon",
			"🔍 Zoom to Fit",
			"",
			"🛰️ Satellite",
			"🗺️ OpenStreetMap",
			"⛰️ Topographic",
			"🌙 Dark",
			"☀️ Light");
		
		if (action == "🗺️ Change Basemap") OnBasemapClicked(sender, e);
		else if (action == "✏️ Draw Polygon") OnDrawPolygonClicked(sender, e);
		else if (action == "🔍 Zoom to Fit") OnZoomToFitClicked(sender, e);
		else if (action == "🛰️ Satellite") OnSatelliteBasemapClicked(sender, e);
		else if (action == "🗺️ OpenStreetMap") OnOSMBasemapClicked(sender, e);
		else if (action == "⛰️ Topographic") OnTopoBasemapClicked(sender, e);
		else if (action == "🌙 Dark") OnDarkBasemapClicked(sender, e);
		else if (action == "☀️ Light") OnLightBasemapClicked(sender, e);
	}
	
	private async void OnToolsMenuClicked(object sender, EventArgs e)
	{
		var action = await DisplayActionSheet("Tools", "Cancel", null,
			"🔬 Test Spatial Operations",
			"📊 Statistics");
		
		if (action == "🔬 Test Spatial Operations") OnTestButtonClicked(sender, e);
		else if (action == "📊 Statistics") OnStatisticsClicked(sender, e);
	}
	
	private async void OnHelpMenuClicked(object sender, EventArgs e)
	{
		var action = await DisplayActionSheet("Help", "Cancel", null,
			"ℹ️ About");
		
		if (action == "ℹ️ About") OnAboutClicked(sender, e);
	}
	
	// Unified menu action handler
	public async Task HandleMenuAction(string action)
	{
		switch (action)
		{
			case "ImportWells":
				OnImportClicked(this, EventArgs.Empty);
				break;
			case "ImportPolygons":
				OnImportPolygonsClicked(this, EventArgs.Empty);
				break;
			case "Export":
				OnExportClicked(this, EventArgs.Empty);
				break;
			case "NewProject":
				OnNewProjectClicked(this, EventArgs.Empty);
				break;
			case "SaveProject":
				OnSaveProjectClicked(this, EventArgs.Empty);
				break;
			case "OpenProject":
				OnLoadProjectClicked(this, EventArgs.Empty);
				break;
			case "ChangeBasemap":
				OnBasemapClicked(this, EventArgs.Empty);
				break;
			case "DrawPolygon":
				OnDrawPolygonClicked(this, EventArgs.Empty);
				break;
			case "ZoomToFit":
				OnZoomToFitClicked(this, EventArgs.Empty);
				break;
			case "SatelliteBasemap":
				OnSatelliteBasemapClicked(this, EventArgs.Empty);
				break;
			case "OSMBasemap":
				OnOSMBasemapClicked(this, EventArgs.Empty);
				break;
			case "TopoBasemap":
				OnTopoBasemapClicked(this, EventArgs.Empty);
				break;
			case "DarkBasemap":
				OnDarkBasemapClicked(this, EventArgs.Empty);
				break;
			case "LightBasemap":
				OnLightBasemapClicked(this, EventArgs.Empty);
				break;
			case "TestSpatial":
				OnTestButtonClicked(this, EventArgs.Empty);
				break;
			case "Statistics":
				OnStatisticsClicked(this, EventArgs.Empty);
				break;
			case "About":
				OnAboutClicked(this, EventArgs.Empty);
				break;
		}
	}
	
	private async void OnSendCommand(object sender, EventArgs e)
	{
		try
		{
			if (ChatEntry != null && !string.IsNullOrWhiteSpace(ChatEntry.Text))
			{
				AddChatMessage("You", ChatEntry.Text);
				ChatEntry.Text = string.Empty;
			}
			
			if (_viewModel?.SendCommandCommand?.CanExecute(null) == true)
			{
				_viewModel.SendCommandCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnExportClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.ExportWellsCommand?.CanExecute(null) == true)
			{
				_viewModel.ExportWellsCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnDrawPolygonClicked(object sender, EventArgs e)
	{
		try
		{
			_isDrawingMode = !_isDrawingMode;
			if (_bridgeService != null)
			{
				await _bridgeService.SetDrawingModeAsync(_isDrawingMode);
				StatusLabel.Text = _isDrawingMode ? "Drawing mode: Click to draw polygon" : "Drawing mode disabled";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnZoomToFitClicked(object sender, EventArgs e)
	{
		try
		{
			// Zoom to sample data bounds (Houston area)
			if (_bridgeService != null)
			{
				await _bridgeService.ZoomToBoundsAsync(-95.39, 29.74, -95.35, 29.78);
				StatusLabel.Text = "Zoomed to fit";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnTestButtonClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.TestSpatialCommand?.CanExecute(null) == true)
			{
				_viewModel.TestSpatialCommand.Execute(null);
			}
			else
			{
				// Load sample data if no data loaded yet
				await LoadSampleDataAsync();
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to load test data: {ex.Message}", "OK");
		}
	}
	
	private async Task LoadSampleDataAsync()
	{
		try
		{
			if (_bridgeService == null) return;
			
			// Load sample GeoJSON
			using var stream = await FileSystem.OpenAppPackageFileAsync("sample_polygons.geojson");
			using var reader = new StreamReader(stream);
			var geojson = await reader.ReadToEndAsync();
			
			// Add layer to map
			var success = await _bridgeService.AddLayerAsync("sample-polygons", geojson, new Dictionary<string, object>
			{
				{ "fill-color", "#3bb2d0" },
				{ "fill-opacity", 0.3 },
				{ "stroke-color", "#1e90ff" },
				{ "stroke-width", 2 }
			});
			
			if (success)
			{
				StatusLabel.Text = "Sample data loaded - Use Import to load your own data";
				AddChatMessage("System", "Sample polygons loaded. Click Import to load real data.");
				
				// Add to layers panel
				AddLayerToPanel("sample-polygons", "Sample Polygons (3)", true);
			}
			else
			{
				StatusLabel.Text = "Failed to load sample data";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to load sample data: {ex.Message}", "OK");
		}
	}
	
	private void AddChatMessage(string sender, string message)
	{
		if (ChatHistoryStack == null) return;
		
		var label = new Label
		{
			Text = $"{sender}: {message}",
			TextColor = sender == "You" ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x95, 0xA5, 0xA6),
			FontSize = 11,
			Margin = new Thickness(0, 2, 0, 2),
			LineBreakMode = LineBreakMode.WordWrap
		};
		
		ChatHistoryStack.Children.Add(label);
		
		// Scroll to bottom
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
		{
			// ScrollView will auto-scroll if content changes
		});
	}
	
	private void AddLayerToPanel(string layerId, string layerName, bool isVisible)
	{
		if (LayersStack == null) return;
		
		var layerFrame = new Frame
		{
			BackgroundColor = Color.FromRgb(0x1a, 0x1a, 0x1a),
			BorderColor = Color.FromRgb(0x3d, 0x3d, 0x3d),
			CornerRadius = 4,
			Padding = 8,
			Margin = new Thickness(0, 0, 0, 4)
		};
		
		var layerGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new ColumnDefinition { Width = GridLength.Star },
				new ColumnDefinition { Width = GridLength.Auto }
			}
		};
		
		var nameLabel = new Label
		{
			Text = layerName,
			TextColor = Color.FromRgb(0xE0, 0xE0, 0xE0),
			FontSize = 12,
			VerticalOptions = LayoutOptions.Center
		};
		
		var visibilitySwitch = new Switch
		{
			IsToggled = isVisible,
			OnColor = Color.FromRgb(0x27, 0xAE, 0x60),
			VerticalOptions = LayoutOptions.Center
		};
		
		visibilitySwitch.Toggled += async (s, e) =>
		{
			// Toggle layer visibility
			if (_bridgeService != null)
			{
				if (e.Value)
				{
					// Show layer - would need to re-add or change opacity
				}
				else
				{
					// Hide layer - set opacity to 0
					await _bridgeService.SetLayerStyleAsync(layerId, new Dictionary<string, object>
					{
						{ "fill-opacity", 0 },
						{ "line-opacity", 0 }
					});
		}
			}
		};
		
		Grid.SetColumn(nameLabel, 0);
		Grid.SetColumn(visibilitySwitch, 1);
		layerGrid.Children.Add(nameLabel);
		layerGrid.Children.Add(visibilitySwitch);
		
		layerFrame.Content = layerGrid;
		LayersStack.Children.Add(layerFrame);
	}
}
