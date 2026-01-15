using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.Json;
using StrataVelyx.Models;
using StrataVelyx.Services;

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
    
    public WebViewBridgeService? BridgeService { get; set; }
    
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
    
    private async void UpdateMapWells()
    {
        if (BridgeService == null) return;
        
        try
        {
        // Remove existing wells layer
            await BridgeService.RemoveLayerAsync("wells");
            
            if (_filteredWells.Count == 0) return;
            
            // Create GeoJSON from filtered wells
            var geojson = CreateWellsGeoJSON(_filteredWells);
            
            // Add layer to map
            var style = new Dictionary<string, object>
            {
                { "circle-radius", 6 },
                { "circle-color", "#3bb2d0" },
                { "circle-stroke-color", "#fff" },
                { "circle-stroke-width", 1 }
            };
            
            await BridgeService.AddLayerAsync("wells", geojson, style);
        
        // Zoom to wells if first time
            if (_allWells.Count > 0)
        {
                var bounds = GetWellsBounds(_filteredWells);
                if (bounds.HasValue)
            {
                    await BridgeService.ZoomToBoundsAsync(bounds.Value.MinLon, bounds.Value.MinLat, bounds.Value.MaxLon, bounds.Value.MaxLat);
            }
        }
    }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateMapWells Error: {ex}");
            StatusMessage = $"Error updating map: {ex.Message}";
        }
    }
    
    private string CreateWellsGeoJSON(List<Well> wells)
    {
        var features = new List<object>();
        
        foreach (var well in wells)
        {
            var color = GetWellColorHex(well);
            features.Add(new
            {
                type = "Feature",
                id = well.Name,
                properties = new
                {
                    id = well.Name,
                    name = well.Name,
                    status = well.Status,
                    watercut = well.Watercut,
                    oilRate = well.OilRate,
                    color = color
                },
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { well.Longitude, well.Latitude }
                }
            });
        }
        
        var featureCollection = new
        {
            type = "FeatureCollection",
            features = features
        };
        
        return JsonSerializer.Serialize(featureCollection);
    }
    
    private string GetWellColorHex(Well well)
    {
        return well.Status.ToLower() switch
        {
            "producer" => "#008000",      // Green
            "injector" => "#0000FF",      // Blue
            "active" => "#00C800",        // Light green
            "inactive" => "#808080",      // Gray
            _ => "#FFA500"                // Orange
        };
    }
    
    private (double MinLon, double MinLat, double MaxLon, double MaxLat)? GetWellsBounds(List<Well> wells)
    {
        if (wells.Count == 0) return null;
        
        var minLon = wells.Min(w => w.Longitude);
        var maxLon = wells.Max(w => w.Longitude);
        var minLat = wells.Min(w => w.Latitude);
        var maxLat = wells.Max(w => w.Latitude);
        
        return (minLon, minLat, maxLon, maxLat);
    }
    
    public void InitializeBridge(WebViewBridgeService bridgeService)
    {
        try
        {
            BridgeService = bridgeService;
            StatusMessage = "Map ready. Click 📁 to import wells or type a command.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeBridge Error: {ex}");
            StatusMessage = $"Map initialization warning: {ex.Message}. You can still import wells.";
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
