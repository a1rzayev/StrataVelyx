namespace StrataVelyx.Models;

public class KPI
{
    public string WellId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double OilProduction { get; set; } // bbl
    public double WaterProduction { get; set; } // bbl
    public double GasProduction { get; set; } // mcf
    public double InjectionVolume { get; set; } // bbl
    public double Pressure { get; set; } // psi
    public double Temperature { get; set; } // F
    
    public double Watercut => OilProduction + WaterProduction > 0 
        ? (WaterProduction / (OilProduction + WaterProduction)) * 100 
        : 0;
}
