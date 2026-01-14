using NetTopologySuite.Geometries;

namespace StrataVelyx.Models;

public class FieldPolygon
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Block, Pad, Lease, Field
    public Polygon Geometry { get; set; } = null!;
    public Dictionary<string, string> Properties { get; set; } = new();
    
    public override string ToString()
    {
        return $"{Name} ({Type})";
    }
}
