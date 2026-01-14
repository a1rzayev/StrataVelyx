namespace StrataVelyx.Models;

public class Well
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = string.Empty; // Active, Inactive, Injector, Producer
    public double OilRate { get; set; } // bbl/day
    public double WaterRate { get; set; } // bbl/day
    public double GasRate { get; set; } // mcf/day
    public double Watercut { get; set; } // percentage
    public string Field { get; set; } = string.Empty;
    public DateTime? LastUpdate { get; set; }
    
    // Computed property
    public double TotalLiquidRate => OilRate + WaterRate;
    
    public override string ToString()
    {
        return $"{Name} ({Id}) - {Status}";
    }
}
