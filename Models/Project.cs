using System.Text.Json;

namespace StrataVelyx.Models;

/// <summary>
/// Represents a GIS project with data and map state
/// Like QGIS project files (.qgs)
/// </summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled Project";
    public string Description { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    
    // Map view state
    public double CenterLongitude { get; set; }
    public double CenterLatitude { get; set; }
    public double ZoomLevel { get; set; } = 2; // World view by default
    
    // Data references
    public string? WellsDataPath { get; set; }
    public string? PolygonsDataPath { get; set; }
    public string? DatabasePath { get; set; }
    
    // Layers configuration
    public List<LayerConfig> Layers { get; set; } = new();
    
    // Statistics
    public int WellCount { get; set; }
    public int PolygonCount { get; set; }
    
    /// <summary>
    /// Save project to JSON file
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(filePath, json);
    }
    
    /// <summary>
    /// Load project from JSON file
    /// </summary>
    public static async Task<Project?> LoadFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Project>(json);
        }
        catch
        {
            return null;
        }
    }
}

public class LayerConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Point, Polygon, Line
    public bool IsVisible { get; set; } = true;
    public Dictionary<string, object> Style { get; set; } = new();
}
