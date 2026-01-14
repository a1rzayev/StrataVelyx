using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StrataVelyx.Models;
using StrataVelyx.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using MapsuiMap = Mapsui.Map;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiPen = Mapsui.Styles.Pen;

namespace StrataVelyx.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly WellDataService _wellDataService;
    private readonly PolygonDataService _polygonDataService;
    private readonly ChatCommandService _chatCommandService;
    
    private List<Well> _allWells = new();
    private List<Well> _filteredWells = new();
    private List<Well> _selectedWells = new();
    private List<FieldPolygon> _polygons = new();
    
    private string _chatInput = string.Empty;
    private string _statusMessage = "Ready. Type a command or import wells to start.";
    
    public MainViewModel()
    {
        _wellDataService = new WellDataService();
        _polygonDataService = new PolygonDataService();
        _chatCommandService = new ChatCommandService();
        
        SendCommandCommand = new Command(async () => await SendCommandAsync());
        ImportWellsCommand = new Command(async () => await ImportWellsAsync());
        ExportWellsCommand = new Command(async () => await ExportWellsAsync());
        
        ChatHistory = new ObservableCollection<string>();
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
    
    public ICommand SendCommandCommand { get; }
    public ICommand ImportWellsCommand { get; }
    public ICommand ExportWellsCommand { get; }
    
    public MapsuiMap? MapControl { get; set; }
    
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput))
            return;
        
        var input = ChatInput;
        ChatHistory.Add($"You: {input}");
        ChatInput = string.Empty;
        
        var command = _chatCommandService.ParseCommand(input);
        ChatHistory.Add($"Assistant: {command.Message}");
        
        await ExecuteCommandAsync(command);
    }
    
    private async Task ExecuteCommandAsync(ChatCommand command)
    {
        try
        {
            switch (command.Type)
            {
                case CommandType.ImportWells:
                    await ImportWellsAsync();
                    break;
                    
                case CommandType.FilterByStatus:
                    if (command.Parameters.TryGetValue("status", out var status))
                    {
                        FilterByStatus(status);
                    }
                    break;
                    
                case CommandType.FilterByWatercut:
                    if (command.Parameters.TryGetValue("operator", out var op) && 
                        command.Parameters.TryGetValue("value", out var val))
                    {
                        FilterByWatercut(op, double.Parse(val));
                    }
                    break;
                    
                case CommandType.ExportWells:
                    await ExportWellsAsync();
                    break;
                    
                case CommandType.ShowAll:
                    ShowAllWells();
                    break;
                    
                case CommandType.SelectInPolygon:
                    StatusMessage = "Polygon selection - coming soon! Use filter commands for now.";
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ChatHistory.Add($"Error: {ex.Message}");
        }
    }
    
    public async Task ImportWellsAsync()
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
                _allWells = await _wellDataService.ImportWellsFromCsvAsync(result.FullPath);
                _filteredWells = new List<Well>(_allWells);
                UpdateMapWells();
                StatusMessage = $"Imported {_allWells.Count} wells";
                ChatHistory.Add($"Imported {_allWells.Count} wells from {result.FileName}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            ChatHistory.Add($"Import failed: {ex.Message}");
        }
    }
    
    private void FilterByStatus(string status)
    {
        _filteredWells = _wellDataService.FilterWells(_allWells, 
            w => w.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        UpdateMapWells();
        StatusMessage = $"Filtered to {_filteredWells.Count} {status} wells";
    }
    
    private void FilterByWatercut(string operator_, double value)
    {
        _filteredWells = operator_ switch
        {
            ">" => _wellDataService.FilterWells(_allWells, w => w.Watercut > value),
            "<" => _wellDataService.FilterWells(_allWells, w => w.Watercut < value),
            ">=" => _wellDataService.FilterWells(_allWells, w => w.Watercut >= value),
            "<=" => _wellDataService.FilterWells(_allWells, w => w.Watercut <= value),
            "=" or "==" => _wellDataService.FilterWells(_allWells, w => Math.Abs(w.Watercut - value) < 0.01),
            _ => _allWells
        };
        UpdateMapWells();
        StatusMessage = $"Filtered to {_filteredWells.Count} wells (watercut {operator_} {value}%)";
    }
    
    private void ShowAllWells()
    {
        _filteredWells = new List<Well>(_allWells);
        UpdateMapWells();
        StatusMessage = $"Showing all {_allWells.Count} wells";
    }
    
    private async Task ExportWellsAsync()
    {
        try
        {
            var exportList = _selectedWells.Count > 0 ? _selectedWells : _filteredWells;
            
            if (exportList.Count == 0)
            {
                StatusMessage = "No wells to export";
                return;
            }
            
            var fileName = $"wells_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            
            await _wellDataService.ExportWellsToCsvAsync(exportList, filePath);
            
            StatusMessage = $"Exported {exportList.Count} wells to {fileName}";
            ChatHistory.Add($"Exported {exportList.Count} wells to {filePath}");
            
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Export Wells",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            ChatHistory.Add($"Export failed: {ex.Message}");
        }
    }
    
    private void UpdateMapWells()
    {
        if (MapControl == null) return;
        
        // Remove existing wells layer
        var existingLayer = MapControl.Layers.FirstOrDefault(l => l.Name == "Wells");
        if (existingLayer != null)
        {
            MapControl.Layers.Remove(existingLayer);
        }
        
        // Create new layer with filtered wells
        var wellsLayer = CreateWellsLayer(_filteredWells);
        MapControl.Layers.Add(wellsLayer);
        
        // Zoom to wells if first time
        if (_allWells.Count > 0 && MapControl.Navigator != null)
        {
            var extent = GetWellsExtent(_filteredWells);
            if (extent != null)
            {
                MapControl.Navigator.ZoomToBox(extent);
            }
        }
    }
    
    private ILayer CreateWellsLayer(List<Well> wells)
    {
        var layer = new MemoryLayer
        {
            Name = "Wells",
            IsMapInfoLayer = true
        };
        
        var features = new List<IFeature>();
        
        foreach (var well in wells)
        {
            var point = SphericalMercator.FromLonLat(well.Longitude, well.Latitude);
            var feature = new PointFeature(new MPoint(point.x, point.y));
            
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.5,
                Fill = new MapsuiBrush(GetWellColor(well)),
                Outline = new MapsuiPen(MapsuiColor.Black, 2)
            });
            
            feature["Name"] = well.Name;
            feature["Status"] = well.Status;
            feature["Watercut"] = well.Watercut.ToString("F1");
            feature["OilRate"] = well.OilRate.ToString("F0");
            
            features.Add(feature);
        }
        
        layer.Features = features;
        return layer;
    }
    
    private MapsuiColor GetWellColor(Well well)
    {
        return well.Status.ToLower() switch
        {
            "producer" => MapsuiColor.FromArgb(255, 0, 128, 0),      // Green
            "injector" => MapsuiColor.FromArgb(255, 0, 0, 255),      // Blue
            "active" => MapsuiColor.FromArgb(255, 0, 200, 0),        // Light green
            "inactive" => MapsuiColor.FromArgb(255, 128, 128, 128),  // Gray
            _ => MapsuiColor.FromArgb(255, 255, 165, 0)              // Orange
        };
    }
    
    private MRect? GetWellsExtent(List<Well> wells)
    {
        if (wells.Count == 0) return null;
        
        var minLon = wells.Min(w => w.Longitude);
        var maxLon = wells.Max(w => w.Longitude);
        var minLat = wells.Min(w => w.Latitude);
        var maxLat = wells.Max(w => w.Latitude);
        
        var min = SphericalMercator.FromLonLat(minLon, minLat);
        var max = SphericalMercator.FromLonLat(maxLon, maxLat);
        
        return new MRect(min.x, min.y, max.x, max.y);
    }
    
    public void InitializeMap(MapsuiMap map)
    {
        try
        {
            MapControl = map;
            
            // Add OpenStreetMap background
            var tileLayer = OpenStreetMap.CreateTileLayer();
            if (tileLayer != null)
            {
                map.Layers.Add(tileLayer);
            }
            
            // Set initial map position (Houston area - center of sample data)
            map.Navigator?.CenterOn(new MPoint(-10609293, 3475163)); // Houston in Web Mercator
            map.Navigator?.ZoomTo(50000); // Zoom level
            
            StatusMessage = "Map ready. Click 📁 to import wells or type a command.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeMap Error: {ex}");
            StatusMessage = $"Map initialization warning: {ex.Message}. You can still import wells.";
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
