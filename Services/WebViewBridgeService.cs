using System.Text.Json;

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
        if (_webView == null) return false;

        var script = $@"window.mapBridge.setDrawingMode({enabled.ToString().ToLower()});";

        try
        {
            await _webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting drawing mode: {ex.Message}");
            return false;
        }
    }

    // Event handlers (can be overridden)
    public event EventHandler<MapReadyEventArgs>? MapReady;
    public event EventHandler<FeatureClickEventArgs>? FeatureClick;
    public event EventHandler<MapClickEventArgs>? MapClick;
    public event EventHandler<PolygonDrawnEventArgs>? PolygonDrawn;

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

            PolygonDrawn?.Invoke(this, new PolygonDrawnEventArgs
            {
                Coordinates = coordinates,
                Area = area
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing polygon drawn: {ex.Message}");
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
}
