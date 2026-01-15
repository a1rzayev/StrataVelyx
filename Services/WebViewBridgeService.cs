using System.Text.Json;
using Microsoft.Maui.Storage;

namespace StrataVelyx.Services;

public class WebViewBridgeService
{
    private WebView? _webView;
    private readonly Dictionary<string, Action<JsonElement>> _messageHandlers = new();

    public WebViewBridgeService()
    {
        // Register default message handlers
        RegisterHandler("mapReady", OnMapReady);
        RegisterHandler("featureClick", OnFeatureClick);
        RegisterHandler("mapClick", OnMapClick);
        RegisterHandler("polygonDrawn", OnPolygonDrawn);
        RegisterHandler("drawingModeEnabled", OnDrawingModeEnabled);
        RegisterHandler("drawingUpdate", OnDrawingUpdate);
        RegisterHandler("drawingModeChanged", OnDrawingModeChanged);
    }

    public void SetWebView(WebView webView)
    {
        _webView = webView;
    }

    public void RegisterHandler(string messageType, Action<JsonElement> handler)
    {
        _messageHandlers[messageType] = handler;
    }

    public void HandleMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                if (type != null && _messageHandlers.TryGetValue(type, out var handler))
                {
                    handler(root);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
        }
    }

    // C# → JS: Add GeoJSON layer
    public async Task<bool> AddLayerAsync(string layerId, string geojson, Dictionary<string, object>? style = null)
    {
        if (_webView == null) return false;

        var styleJson = style != null ? JsonSerializer.Serialize(style) : "null";
        var script = $@"
            window.mapBridge.addLayer('{layerId}', {geojson}, {styleJson});
        ";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding layer: {ex.Message}");
            return false;
        }
    }

    // C# → JS: Remove layer
    public async Task<bool> RemoveLayerAsync(string layerId)
    {
        if (_webView == null) return false;

        var script = $@"window.mapBridge.removeLayer('{layerId}');";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing layer: {ex.Message}");
            return false;
        }
    }

    // C# → JS: Set layer style
    public async Task<bool> SetLayerStyleAsync(string layerId, Dictionary<string, object> style)
    {
        if (_webView == null) return false;

        var styleJson = JsonSerializer.Serialize(style);
        var script = $@"window.mapBridge.setLayerStyle('{layerId}', {styleJson});";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting layer style: {ex.Message}");
            return false;
        }
    }
    
    // C# → JS: Set layer opacity
    public async Task<bool> SetLayerOpacityAsync(string layerId, double opacity)
    {
        if (_webView == null) return false;

        var script = $@"window.mapBridge.setLayerOpacity('{layerId}', {opacity});";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting layer opacity: {ex.Message}");
            return false;
        }
    }

    // C# → JS: Zoom to bounds
    public async Task<bool> ZoomToBoundsAsync(double minLon, double minLat, double maxLon, double maxLat)
    {
        if (_webView == null) return false;

        var script = $@"
            window.mapBridge.zoomToBounds([
                [{minLon}, {minLat}],
                [{maxLon}, {maxLat}]
            ]);
        ";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error zooming to bounds: {ex.Message}");
            return false;
        }
    }

    // C# → JS: Enable/disable drawing
    public async Task<bool> SetDrawingModeAsync(bool enabled)
    {
        // #region agent log
        try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:159", message = "SetDrawingModeAsync entry", data = new { enabled = enabled, webViewNull = _webView == null }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine($"[DEBUG] SetDrawingModeAsync entry: enabled={enabled}"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex.Message}"); }
        // #endregion
        
        if (_webView == null)
        {
            // #region agent log
            try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:165", message = "WebView is null", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine("[DEBUG] WebView is null"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex.Message}"); }
            // #endregion
            return false;
        }

        // Use a simpler, more reliable JavaScript call
        var enabledStr = enabled ? "true" : "false";
        var script = $"window.mapBridge && window.mapBridge.setDrawingMode && window.mapBridge.setDrawingMode({enabledStr});";

        // #region agent log
        try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:172", message = "About to execute JavaScript", data = new { script = script }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine($"[DEBUG] About to execute JS: {script}"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex.Message}"); }
        // #endregion

        try
        {
            var result = await _webView.EvaluateJavaScriptAsync(script);
            // #region agent log
            try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:178", message = "JavaScript executed successfully", data = new { result = result?.ToString() ?? "null" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine($"[DEBUG] JS executed: result={result}"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex.Message}"); }
            // #endregion
            System.Diagnostics.Debug.WriteLine($"SetDrawingMode called with: {enabled}");
            return true;
        }
        catch (Exception ex)
        {
            // #region agent log
            try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:185", message = "JavaScript execution failed", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine($"[DEBUG] JS execution failed: {ex.Message}"); } catch (Exception ex2) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex2.Message}"); }
            // #endregion
            System.Diagnostics.Debug.WriteLine($"Error setting drawing mode: {ex.Message}");
            // Try alternative approach
            try
            {
                var altScript = $@"
                    if (typeof window !== 'undefined' && window.mapBridge) {{
                        window.mapBridge.setDrawingMode({enabledStr});
                    }}
                ";
                await _webView.EvaluateJavaScriptAsync(altScript);
                // #region agent log
                try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:198", message = "Alternative script executed", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine("[DEBUG] Alternative script executed"); } catch (Exception ex3) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex3.Message}"); }
                // #endregion
                return true;
            }
            catch (Exception ex2)
            {
                // #region agent log
                try { var logPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log"); var log = System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WebViewBridgeService.cs:203", message = "Alternative script also failed", data = new { error = ex2.Message }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }); System.IO.File.AppendAllText(logPath, log + "\n"); System.Diagnostics.Debug.WriteLine($"[DEBUG] Alternative script also failed: {ex2.Message}"); } catch (Exception ex4) { System.Diagnostics.Debug.WriteLine($"[DEBUG LOG ERROR] {ex4.Message}"); }
                // #endregion
                System.Diagnostics.Debug.WriteLine($"Alternative approach also failed: {ex2.Message}");
                return false;
            }
        }
    }
    
    // C# → JS: Switch basemap
    public async Task<bool> SetBasemapAsync(string basemapName)
    {
        if (_webView == null) return false;

        var script = $@"window.mapBridge.setBasemap('{basemapName}');";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting basemap: {ex.Message}");
            return false;
        }
    }
    
    // C# → JS: Toggle basemap selector
    public async Task<bool> ToggleBasemapSelectorAsync()
    {
        if (_webView == null) return false;

        var script = @"window.mapBridge.toggleBasemapSelector();";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error toggling basemap selector: {ex.Message}");
            return false;
        }
    }
    
    // C# → JS: Get current map view
    public async Task<MapView?> GetMapViewAsync()
    {
        if (_webView == null) return null;

        var script = @"JSON.stringify(window.mapBridge.getMapView());";

        try
        {
            var result = await _webView.EvaluateJavaScriptAsync(script);
            if (!string.IsNullOrEmpty(result) && result != "null")
            {
                return JsonSerializer.Deserialize<MapView>(result);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting map view: {ex.Message}");
        }
        
        return null;
    }
    
    // C# → JS: Set map view
    public async Task<bool> SetMapViewAsync(double longitude, double latitude, double zoom, double bearing = 0, double pitch = 0)
    {
        if (_webView == null) return false;

        var script = $@"window.mapBridge.setMapView({longitude}, {latitude}, {zoom}, {bearing}, {pitch});";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting map view: {ex.Message}");
            return false;
        }
    }

    // Event handlers (can be overridden)
    public event EventHandler<MapReadyEventArgs>? MapReady;
    public event EventHandler<FeatureClickEventArgs>? FeatureClick;
    public event EventHandler<MapClickEventArgs>? MapClick;
    public event EventHandler<PolygonDrawnEventArgs>? PolygonDrawn;
    public event EventHandler<DrawingModeEventArgs>? DrawingModeEnabled;
    public event EventHandler<DrawingUpdateEventArgs>? DrawingUpdate;
    public event EventHandler<DrawingModeChangedEventArgs>? DrawingModeChanged;

    private void OnMapReady(JsonElement data)
    {
        MapReady?.Invoke(this, new MapReadyEventArgs());
    }

    private void OnFeatureClick(JsonElement data)
    {
        try
        {
            var featureId = data.TryGetProperty("featureId", out var id) ? id.GetString() ?? "" : "";
            var coords = data.TryGetProperty("coordinates", out var coord) 
                ? new[] { coord[0].GetDouble(), coord[1].GetDouble() } 
                : new[] { 0.0, 0.0 };
            
            var properties = new Dictionary<string, string>();
            if (data.TryGetProperty("properties", out var props))
            {
                foreach (var prop in props.EnumerateObject())
                {
                    properties[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            FeatureClick?.Invoke(this, new FeatureClickEventArgs
            {
                FeatureId = featureId,
                Coordinates = coords,
                Properties = properties
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing feature click: {ex.Message}");
        }
    }

    private void OnMapClick(JsonElement data)
    {
        try
        {
            var coords = data.TryGetProperty("coordinates", out var coord)
                ? new[] { coord[0].GetDouble(), coord[1].GetDouble() }
                : new[] { 0.0, 0.0 };

            MapClick?.Invoke(this, new MapClickEventArgs
            {
                Coordinates = coords
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing map click: {ex.Message}");
        }
    }

    private void OnPolygonDrawn(JsonElement data)
    {
        try
        {
            var coordinates = new List<double[]>();
            if (data.TryGetProperty("coordinates", out var coords))
            {
                foreach (var coord in coords.EnumerateArray())
                {
                    coordinates.Add(new[] { coord[0].GetDouble(), coord[1].GetDouble() });
                }
            }

            var area = data.TryGetProperty("area", out var areaProp) ? areaProp.GetDouble() : 0.0;
            var featureId = data.TryGetProperty("featureId", out var idProp) ? idProp.GetString() ?? "" : "";

            PolygonDrawn?.Invoke(this, new PolygonDrawnEventArgs
            {
                Coordinates = coordinates,
                Area = area,
                FeatureId = featureId
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing polygon drawn: {ex.Message}");
        }
    }
    
    private void OnDrawingModeEnabled(JsonElement data)
    {
        try
        {
            var message = data.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
            DrawingModeEnabled?.Invoke(this, new DrawingModeEventArgs { Message = message });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing drawing mode enabled: {ex.Message}");
        }
    }
    
    private void OnDrawingUpdate(JsonElement data)
    {
        try
        {
            var pointCount = data.TryGetProperty("pointCount", out var countProp) ? countProp.GetInt32() : 0;
            var message = data.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
            DrawingUpdate?.Invoke(this, new DrawingUpdateEventArgs 
            { 
                PointCount = pointCount,
                Message = message 
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing drawing update: {ex.Message}");
        }
    }
    
    private void OnDrawingModeChanged(JsonElement data)
    {
        try
        {
            var mode = data.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() ?? "" : "";
            var message = data.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
            DrawingModeChanged?.Invoke(this, new DrawingModeChangedEventArgs 
            { 
                Mode = mode,
                Message = message 
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing drawing mode changed: {ex.Message}");
        }
    }
    
    // C# → JS: Clear current drawing
    public async Task<bool> ClearDrawingAsync()
    {
        if (_webView == null) return false;

        var script = @"window.mapBridge.clearDrawing();";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing drawing: {ex.Message}");
            return false;
        }
    }
}

// Event args
public class MapReadyEventArgs : EventArgs { }

public class FeatureClickEventArgs : EventArgs
{
    public string FeatureId { get; set; } = "";
    public double[] Coordinates { get; set; } = Array.Empty<double>();
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class MapClickEventArgs : EventArgs
{
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}

public class PolygonDrawnEventArgs : EventArgs
{
    public List<double[]> Coordinates { get; set; } = new();
    public double Area { get; set; }
    public string FeatureId { get; set; } = "";
}

public class DrawingModeEventArgs : EventArgs
{
    public string Message { get; set; } = "";
}

public class DrawingUpdateEventArgs : EventArgs
{
    public int PointCount { get; set; }
    public string Message { get; set; } = "";
}

public class DrawingModeChangedEventArgs : EventArgs
{
    public string Mode { get; set; } = "";
    public string Message { get; set; } = "";
}

public class MapView
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public double Zoom { get; set; }
    public double Bearing { get; set; }
    public double Pitch { get; set; }
}
