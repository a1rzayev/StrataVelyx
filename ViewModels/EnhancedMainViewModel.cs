using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.Json;
using StrataVelyx.Models;
using StrataVelyx.Services;
using StrataVelyx.Services.DataValidation;
using StrataVelyx.Services.Database;

namespace StrataVelyx.ViewModels;

/// <summary>
/// Enhanced ViewModel that connects all Phase 2 & 3 components
/// Uses real data services, spatial engine, and database
/// </summary>
public class EnhancedMainViewModel : INotifyPropertyChanged
{
    private readonly EnhancedWellDataService _wellDataService;
    private readonly EnhancedPolygonDataService _polygonDataService;
    private readonly SpatialEngine _spatialEngine;
    private readonly ChatCommandService _chatCommandService;
    private GeoDatabase? _database;
    
    private List<Well> _allWells = new();
    private List<Well> _filteredWells = new();
    private List<FieldPolygon> _allPolygons = new();
    
    private string _chatInput = string.Empty;
    private string _statusMessage = "Ready. Import data to begin.";
    
    private Project? _currentProject;
    
    public EnhancedMainViewModel()
    {
        _wellDataService = new EnhancedWellDataService();
        _polygonDataService = new EnhancedPolygonDataService();
        _spatialEngine = new SpatialEngine();
        _chatCommandService = new ChatCommandService();
        
        ImportWellsCommand = new Command(async () => await ImportWellsAsync());
        ImportPolygonsCommand = new Command(async () => await ImportPolygonsAsync());
        ExportWellsCommand = new Command(async () => await ExportWellsAsync());
        SendCommandCommand = new Command(async () => await SendCommandAsync());
        TestSpatialCommand = new Command(async () => await TestSpatialOperationsAsync());
        NewProjectCommand = new Command(async () => await NewProjectAsync());
        SaveProjectCommand = new Command(async () => await SaveProjectAsync());
        LoadProjectCommand = new Command(async () => await LoadProjectAsync());
        
        ChatHistory = new ObservableCollection<string>();
        Layers = new ObservableCollection<LayerInfo>();
        
        // Initialize with new project
        _currentProject = new Project { Name = "New Project" };
        
        // Initialize database
        InitializeDatabaseAsync();
    }
    
    public string ChatInput
    {
        get => _chatInput;
        set
        {
            _chatInput = value;
            OnPropertyChanged();
        }
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }
    
    public ObservableCollection<string> ChatHistory { get; }
    public ObservableCollection<LayerInfo> Layers { get; }
    
    public ICommand ImportWellsCommand { get; }
    public ICommand ImportPolygonsCommand { get; }
    public ICommand ExportWellsCommand { get; }
    public ICommand SendCommandCommand { get; }
    public ICommand TestSpatialCommand { get; }
    public ICommand NewProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand LoadProjectCommand { get; }
    
    public WebViewBridgeService? BridgeService { get; set; }
    
    public Project? CurrentProject => _currentProject;
    
    private async void InitializeDatabaseAsync()
    {
        try
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "stratavelyx.db");
            _database = new GeoDatabase(dbPath);
            await _database.InitializeAsync();
            AddChatMessage("System", $"Database initialized at {dbPath}");
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", $"Database init failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Import wells from CSV with full validation
    /// </summary>
    private async Task ImportWellsAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Wells CSV File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values" } },
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.macOS, new[] { "csv" } },
                    { DevicePlatform.MacCatalyst, new[] { "csv" } }
                })
            });
            
            if (result != null)
            {
                StatusMessage = "Importing wells...";
                
                // Import with validation
                var importResult = await _wellDataService.ImportWellsFromCsvAsync(result.FullPath);
                
                // Show results
                AddChatMessage("Import", importResult.GetSummary());
                
                if (importResult.Success)
                {
                    _allWells = importResult.ImportedItems;
                    _filteredWells = new List<Well>(_allWells);
                    
                    // Save to database
                    if (_database != null)
                    {
                        await _database.SaveWellsBulkAsync(_allWells);
                        AddChatMessage("Database", $"Saved {_allWells.Count} wells to database");
                    }
                    
                    // Display on map
                    await DisplayWellsOnMapAsync(_filteredWells);
                    
                    // Add layer
                    AddLayer("wells", "Wells", "Point", "Wells", _allWells.Count, true);
                    
                    // Zoom to extent
                    await ZoomToWellsAsync(_filteredWells);
                    
                    StatusMessage = $"Imported {importResult.SuccessfulRows}/{importResult.TotalRows} wells";
                }
                else
                {
                    StatusMessage = $"Import failed: {importResult.ValidationResult.Errors.Count} errors";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            AddChatMessage("Error", ex.Message);
        }
    }
    
    /// <summary>
    /// Import polygons from GeoJSON with validation
    /// </summary>
    private async Task ImportPolygonsAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select GeoJSON File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.Android, new[] { "application/json", "application/geo+json" } },
                    { DevicePlatform.WinUI, new[] { ".json", ".geojson" } },
                    { DevicePlatform.macOS, new[] { "json", "geojson" } },
                    { DevicePlatform.MacCatalyst, new[] { "json", "geojson" } }
                })
            });
            
            if (result != null)
            {
                StatusMessage = "Importing polygons...";
                
                // Import with validation
                var importResult = await _polygonDataService.ImportPolygonsFromGeoJsonAsync(result.FullPath);
                
                // Show results
                AddChatMessage("Import", importResult.GetSummary());
                
                if (importResult.Success)
                {
                    _allPolygons = importResult.ImportedItems;
                    
                    // Save to database
                    if (_database != null)
                    {
                        foreach (var polygon in _allPolygons)
                        {
                            await _database.SavePolygonAsync(polygon);
                        }
                        AddChatMessage("Database", $"Saved {_allPolygons.Count} polygons to database");
                    }
                    
                    // Display on map
                    await DisplayPolygonsOnMapAsync(_allPolygons);
                    
                    // Determine group based on polygon type
                    var group = DeterminePolygonGroup(_allPolygons);
                    
                    // Add layer
                    AddLayer("polygons", "Polygons", "Polygon", group, _allPolygons.Count, true);
                    
                    // Zoom to extent
                    await ZoomToPolygonsAsync(_allPolygons);
                    
                    StatusMessage = $"Imported {importResult.SuccessfulRows}/{importResult.TotalRows} polygons";
                }
                else
                {
                    StatusMessage = $"Import failed: {importResult.ValidationResult.Errors.Count} errors";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            AddChatMessage("Error", ex.Message);
        }
    }
    
    /// <summary>
    /// Display wells on MapLibre map
    /// </summary>
    private async Task DisplayWellsOnMapAsync(List<Well> wells)
    {
        if (BridgeService == null || wells.Count == 0) return;
        
        // Create GeoJSON
        var features = new List<object>();
        foreach (var well in wells)
        {
            features.Add(new
            {
                type = "Feature",
                id = well.Id,
                properties = new
                {
                    id = well.Id,
                    name = well.Name,
                    status = well.Status,
                    oilRate = well.OilRate,
                    waterRate = well.WaterRate,
                    watercut = well.Watercut,
                    field = well.Field
                },
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { well.Longitude, well.Latitude }
                }
            });
        }
        
        var geojson = JsonSerializer.Serialize(new
        {
            type = "FeatureCollection",
            features = features
        });
        
        // Style by status - use simple color for now
        var style = new Dictionary<string, object>
        {
            { "circle-radius", 6 },
            { "circle-color", "#2E7D32" },  // Green for all wells
            { "circle-stroke-color", "#FFFFFF" },
            { "circle-stroke-width", 1.5 }
        };
        
        await BridgeService.AddLayerAsync("wells", geojson, style);
    }
    
    /// <summary>
    /// Display polygons on MapLibre map
    /// </summary>
    private async Task DisplayPolygonsOnMapAsync(List<FieldPolygon> polygons)
    {
        if (BridgeService == null || polygons.Count == 0) return;
        
        // Convert to GeoJSON using NetTopologySuite
        var geoJsonWriter = new NetTopologySuite.IO.GeoJsonWriter();
        var featureCollection = new NetTopologySuite.Features.FeatureCollection();
        
        foreach (var polygon in polygons)
        {
            var attributes = new NetTopologySuite.Features.AttributesTable
            {
                { "id", polygon.Id },
                { "name", polygon.Name },
                { "type", polygon.Type }
            };
            
            var feature = new NetTopologySuite.Features.Feature(polygon.Geometry, attributes);
            featureCollection.Add(feature);
        }
        
        var geojson = geoJsonWriter.Write(featureCollection);
        
        // Style polygons
        var style = new Dictionary<string, object>
        {
            { "fill-color", "#3BB2D0" },
            { "fill-opacity", 0.3 },
            { "stroke-color", "#1E90FF" },
            { "stroke-width", 2 }
        };
        
        await BridgeService.AddLayerAsync("polygons", geojson, style);
    }
    
    /// <summary>
    /// Zoom map to show all wells
    /// </summary>
    private async Task ZoomToWellsAsync(List<Well> wells)
    {
        if (BridgeService == null || wells.Count == 0) return;
        
        var bounds = _spatialEngine.GetWellsBoundingBox(wells);
        await BridgeService.ZoomToBoundsAsync(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
    }
    
    /// <summary>
    /// Zoom map to show all polygons
    /// </summary>
    private async Task ZoomToPolygonsAsync(List<FieldPolygon> polygons)
    {
        if (BridgeService == null || polygons.Count == 0) return;
        
        var bounds = _spatialEngine.GetPolygonsBoundingBox(polygons);
        await BridgeService.ZoomToBoundsAsync(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
    }
    
    /// <summary>
    /// Test spatial operations with current data
    /// </summary>
    private async Task TestSpatialOperationsAsync()
    {
        if (_allWells.Count == 0 || _allPolygons.Count == 0)
        {
            AddChatMessage("Test", "Need both wells and polygons to test spatial operations");
            return;
        }
        
        AddChatMessage("Test", "Running spatial queries...");
        
        // Test 1: Find wells within first polygon
        var firstPolygon = _allPolygons[0];
        var wellsInPolygon = _spatialEngine.SelectWellsWithinField(_allWells, firstPolygon);
        AddChatMessage("Query", $"Found {wellsInPolygon.Count} wells in {firstPolygon.Name}");
        
        // Test 2: Find wells within 500m of first well
        if (_allWells.Count > 0)
        {
            var targetWell = _allWells[0];
            var nearbyWells = _spatialEngine.FindWellsWithinDistance(_allWells, targetWell, 500);
            AddChatMessage("Query", $"Found {nearbyWells.Count} wells within 500m of {targetWell.Name}");
        }
        
        // Test 3: Calculate polygon areas
        foreach (var polygon in _allPolygons.Take(3))
        {
            var areaMeters = _spatialEngine.CalculateAreaSquareMeters(polygon.Geometry);
            var areaAcres = areaMeters / 4046.86; // Convert to acres
            AddChatMessage("Area", $"{polygon.Name}: {areaAcres:F2} acres");
        }
        
        await Task.CompletedTask;
    }
    
    private async Task ExportWellsAsync()
    {
        try
        {
            if (_filteredWells.Count == 0)
            {
                StatusMessage = "No wells to export";
                return;
            }
            
            var fileName = $"wells_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            
            await _wellDataService.ExportWellsToCsvAsync(_filteredWells, filePath);
            
            StatusMessage = $"Exported {_filteredWells.Count} wells";
            AddChatMessage("Export", $"Saved to {filePath}");
            
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Export Wells",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
    
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput))
            return;
        
        var input = ChatInput;
        AddChatMessage("You", input);
        ChatInput = string.Empty;
        
        // Parse and execute command
        var command = _chatCommandService.ParseCommand(input);
        AddChatMessage("Assistant", command.Message);
        
        await ExecuteCommandAsync(command);
    }
    
    private async Task ExecuteCommandAsync(ChatCommand command)
    {
        try
        {
            switch (command.Type)
            {
                case CommandType.FilterByStatus:
                    if (command.Parameters.TryGetValue("status", out var status))
                    {
                        _filteredWells = _wellDataService.FilterWells(_allWells, 
                            w => w.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                        await DisplayWellsOnMapAsync(_filteredWells);
                        StatusMessage = $"Filtered to {_filteredWells.Count} {status} wells";
                    }
                    break;
                    
                case CommandType.ShowAll:
                    _filteredWells = new List<Well>(_allWells);
                    await DisplayWellsOnMapAsync(_filteredWells);
                    StatusMessage = $"Showing all {_allWells.Count} wells";
                    break;
            }
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", ex.Message);
        }
    }
    
    private void AddChatMessage(string sender, string message)
    {
        ChatHistory.Add($"[{DateTime.Now:HH:mm:ss}] {sender}: {message}");
    }
    
    public void AddLayer(string layerId, string name, string type, string group, int featureCount, bool visible)
    {
        Layers.Add(new LayerInfo
        {
            LayerId = layerId,
            Name = name,
            Type = type,
            Group = group,
            FeatureCount = featureCount,
            IsVisible = visible,
            Opacity = 1.0
        });
    }
    
    /// <summary>
    /// Toggle layer visibility
    /// </summary>
    public async Task ToggleLayerVisibilityAsync(string layerId, bool isVisible)
    {
        var layer = Layers.FirstOrDefault(l => l.LayerId == layerId);
        if (layer == null || BridgeService == null) return;
        
        layer.IsVisible = isVisible;
        
        // Update map layer visibility
        if (isVisible)
        {
            // Show layer by restoring opacity
            await BridgeService.SetLayerOpacityAsync(layerId, layer.Opacity);
        }
        else
        {
            // Hide layer by setting opacity to 0
            await BridgeService.SetLayerOpacityAsync(layerId, 0.0);
        }
    }
    
    /// <summary>
    /// Set layer opacity
    /// </summary>
    public async Task SetLayerOpacityAsync(string layerId, double opacity)
    {
        var layer = Layers.FirstOrDefault(l => l.LayerId == layerId);
        if (layer == null || BridgeService == null) return;
        
        layer.Opacity = opacity;
        
        // Only update map if layer is visible
        if (layer.IsVisible)
        {
            await BridgeService.SetLayerOpacityAsync(layerId, opacity);
        }
    }
    
    /// <summary>
    /// Determine the appropriate group for polygons based on their types
    /// </summary>
    private string DeterminePolygonGroup(List<FieldPolygon> polygons)
    {
        if (polygons.Count == 0) return "Geology";
        
        // Check for seismic-related polygons
        var hasSeismic = polygons.Any(p => 
            p.Type?.ToLower().Contains("seismic") == true ||
            p.Name?.ToLower().Contains("seismic") == true);
        
        if (hasSeismic) return "Seismic";
        
        // Default to Geology for reservoirs, fields, licenses, leases, etc.
        return "Geology";
    }
    
    /// <summary>
    /// Determine the appropriate group for line layers (faults, pipelines, seismic lines)
    /// </summary>
    private string DetermineLineGroup(string layerType, string? layerName = null)
    {
        var typeLower = layerType?.ToLower() ?? "";
        var nameLower = layerName?.ToLower() ?? "";
        
        // Seismic lines
        if (typeLower.Contains("seismic") || nameLower.Contains("seismic"))
        {
            return "Seismic";
        }
        
        // Faults go to Geology
        if (typeLower.Contains("fault") || nameLower.Contains("fault"))
        {
            return "Geology";
        }
        
        // Pipelines could be infrastructure, but for now put in Geology
        if (typeLower.Contains("pipeline") || nameLower.Contains("pipeline"))
        {
            return "Geology";
        }
        
        // Default to Geology
        return "Geology";
    }
    
    #region Project Management
    
    /// <summary>
    /// Create new project (clears current data)
    /// </summary>
    private async Task NewProjectAsync()
    {
        var projectName = await Application.Current?.MainPage?.DisplayPromptAsync(
            "New Project",
            "Enter project name:",
            "Create",
            "Cancel",
            "My Project"
        );
        
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            _currentProject = new Project 
            { 
                Name = projectName,
                CenterLongitude = 0,
                CenterLatitude = 20,
                ZoomLevel = 2
            };
            
            // Clear data
            _allWells.Clear();
            _filteredWells.Clear();
            _allPolygons.Clear();
            Layers.Clear();
            ChatHistory.Clear();
            
            // Reset map to world view
            if (BridgeService != null)
            {
                await BridgeService.RemoveLayerAsync("wells");
                await BridgeService.RemoveLayerAsync("polygons");
            }
            
            StatusMessage = $"New project: {projectName}";
            AddChatMessage("Project", $"Created new project '{projectName}'");
        }
    }
    
    /// <summary>
    /// Save current project
    /// </summary>
    private async Task SaveProjectAsync()
    {
        if (_currentProject == null) return;
        
        try
        {
            // Update project stats
            _currentProject.WellCount = _allWells.Count;
            _currentProject.PolygonCount = _allPolygons.Count;
            _currentProject.LastModified = DateTime.Now;
            
            // Get current map view from JavaScript
            if (BridgeService != null)
            {
                try
                {
                    // This would need to be implemented in the bridge
                    // For now, keep current values
                }
                catch { }
            }
            
            // Save to file
            var fileName = $"{_currentProject.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.svproject";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            
            await _currentProject.SaveToFileAsync(filePath);
            
            StatusMessage = $"Project saved: {fileName}";
            AddChatMessage("Project", $"Saved to {filePath}");
            
            // Offer to share
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Project",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            AddChatMessage("Error", $"Could not save project: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Load existing project
    /// </summary>
    private async Task LoadProjectAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Project File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.data" } },
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".svproject" } },
                    { DevicePlatform.macOS, new[] { "svproject" } },
                    { DevicePlatform.MacCatalyst, new[] { "svproject" } }
                })
            });
            
            if (result != null)
            {
                var project = await Project.LoadFromFileAsync(result.FullPath);
                if (project != null)
                {
                    _currentProject = project;
                    
                    StatusMessage = $"Loaded project: {project.Name}";
                    AddChatMessage("Project", $"Loaded '{project.Name}' (Wells: {project.WellCount}, Polygons: {project.PolygonCount})");
                    
                    // Restore map view
                    if (BridgeService != null)
                    {
                        await BridgeService.ZoomToBoundsAsync(
                            project.CenterLongitude - 1,
                            project.CenterLatitude - 1,
                            project.CenterLongitude + 1,
                            project.CenterLatitude + 1
                        );
                    }
                    
                    // Note: Actual data would need to be re-imported from saved paths
                    AddChatMessage("Info", "Note: Re-import data files to restore layers");
                }
                else
                {
                    AddChatMessage("Error", "Could not load project file");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
            AddChatMessage("Error", $"Could not load project: {ex.Message}");
        }
    }
    
    #endregion
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LayerInfo : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private double _opacity = 1.0;
    
    public string LayerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // Point, Line, Polygon
    public string Group { get; set; } = "Other"; // Wells, Geology, Seismic, Other
    public int FeatureCount { get; set; }
    
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged();
        }
    }
    
    public double Opacity
    {
        get => _opacity;
        set
        {
            _opacity = Math.Clamp(value, 0.0, 1.0);
            OnPropertyChanged();
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
