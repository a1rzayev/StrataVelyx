using NetTopologySuite.Geometries;

namespace StrataVelyx.Models;

/// <summary>
/// Represents a pipeline (line geometry) in the reservoir
/// Can also be used for faults, well paths, seismic lines
/// </summary>
public class Pipeline
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Pipeline, Fault, WellPath, SeismicLine
    public LineString Geometry { get; set; } = null!;
    public Dictionary<string, string> Properties { get; set; } = new();
    
    // Pipeline-specific properties
    public double? Diameter { get; set; } // inches
    public string? Material { get; set; } // Steel, PVC, etc.
    public string? FluidType { get; set; } // Oil, Gas, Water
    public string? Status { get; set; } // Active, Inactive, Abandoned
    
    /// <summary>
    /// Length in degrees (use SpatialEngine.CalculateLengthMeters for actual distance)
    /// </summary>
    public double LengthDegrees => Geometry.Length;
    
    public override string ToString()
    {
        return $"{Name} ({Type})";
    }
}
