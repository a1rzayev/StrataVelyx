namespace StrataVelyx.Services.DataValidation;

/// <summary>
/// Validates and normalizes geographic coordinates
/// </summary>
public class CoordinateValidator
{
    // WGS84 bounds
    private const double MIN_LATITUDE = -90.0;
    private const double MAX_LATITUDE = 90.0;
    private const double MIN_LONGITUDE = -180.0;
    private const double MAX_LONGITUDE = 180.0;
    
    /// <summary>
    /// Validates latitude value
    /// </summary>
    public (bool isValid, string? error) ValidateLatitude(double latitude)
    {
        if (double.IsNaN(latitude))
            return (false, "Latitude is NaN");
        
        if (double.IsInfinity(latitude))
            return (false, "Latitude is Infinity");
        
        if (latitude < MIN_LATITUDE || latitude > MAX_LATITUDE)
            return (false, $"Latitude {latitude} is out of valid range ({MIN_LATITUDE} to {MAX_LATITUDE})");
        
        return (true, null);
    }
    
    /// <summary>
    /// Validates longitude value
    /// </summary>
    public (bool isValid, string? error) ValidateLongitude(double longitude)
    {
        if (double.IsNaN(longitude))
            return (false, "Longitude is NaN");
        
        if (double.IsInfinity(longitude))
            return (false, "Longitude is Infinity");
        
        if (longitude < MIN_LONGITUDE || longitude > MAX_LONGITUDE)
            return (false, $"Longitude {longitude} is out of valid range ({MIN_LONGITUDE} to {MAX_LONGITUDE})");
        
        return (true, null);
    }
    
    /// <summary>
    /// Validates coordinate pair
    /// </summary>
    public (bool isValid, string? error) ValidateCoordinates(double longitude, double latitude)
    {
        var (latValid, latError) = ValidateLatitude(latitude);
        if (!latValid)
            return (false, latError);
        
        var (lonValid, lonError) = ValidateLongitude(longitude);
        if (!lonValid)
            return (false, lonError);
        
        return (true, null);
    }
    
    /// <summary>
    /// Normalizes longitude to -180 to 180 range
    /// Handles values like 270 → -90, 400 → 40
    /// </summary>
    public double NormalizeLongitude(double longitude)
    {
        // Handle wrapping around the date line
        while (longitude > MAX_LONGITUDE)
            longitude -= 360.0;
        while (longitude < MIN_LONGITUDE)
            longitude += 360.0;
        
        return longitude;
    }
    
    /// <summary>
    /// Detects if lat/lon might be swapped
    /// Common error when importing data
    /// </summary>
    public bool AreCoordinatesLikelySwapped(double longitude, double latitude)
    {
        // If "latitude" is in longitude range but outside latitude range,
        // and "longitude" is in latitude range, they're likely swapped
        bool latInLonRange = latitude >= MIN_LONGITUDE && latitude <= MAX_LONGITUDE;
        bool latOutsideLat = latitude < MIN_LATITUDE || latitude > MAX_LATITUDE;
        bool lonInLatRange = longitude >= MIN_LATITUDE && longitude <= MAX_LATITUDE;
        bool lonOutsideLon = longitude < MIN_LONGITUDE || longitude > MAX_LONGITUDE;
        
        return latInLonRange && latOutsideLat && lonInLatRange;
    }
    
    /// <summary>
    /// Attempts to fix common coordinate issues
    /// </summary>
    public (double longitude, double latitude, bool wasFixed, string? fixApplied) TryFixCoordinates(
        double longitude, double latitude)
    {
        string? fixApplied = null;
        bool wasFixed = false;
        
        // Check if coordinates are swapped
        if (AreCoordinatesLikelySwapped(longitude, latitude))
        {
            (longitude, latitude) = (latitude, longitude);
            wasFixed = true;
            fixApplied = "Swapped lat/lon";
        }
        
        // Normalize longitude if out of range but wrappable
        if (longitude > MAX_LONGITUDE || longitude < MIN_LONGITUDE)
        {
            var normalized = NormalizeLongitude(longitude);
            if (normalized != longitude)
            {
                longitude = normalized;
                wasFixed = true;
                fixApplied = fixApplied == null ? "Normalized longitude" : fixApplied + "; Normalized longitude";
            }
        }
        
        return (longitude, latitude, wasFixed, fixApplied);
    }
    
    /// <summary>
    /// Validates coordinate precision (warns if suspiciously low)
    /// </summary>
    public (bool hasWarning, string? warning) CheckCoordinatePrecision(double longitude, double latitude)
    {
        // Check decimal places
        var lonStr = longitude.ToString("G17");
        var latStr = latitude.ToString("G17");
        
        int lonDecimals = lonStr.Contains('.') ? lonStr.Split('.')[1].Length : 0;
        int latDecimals = latStr.Contains('.') ? latStr.Split('.')[1].Length : 0;
        
        // Less than 2 decimal places = ~1km precision (too coarse for wells)
        if (lonDecimals < 2 || latDecimals < 2)
        {
            return (true, $"Low coordinate precision ({lonDecimals}, {latDecimals} decimals). May be inaccurate.");
        }
        
        // Exactly 0 or 0.0 is suspicious
        if ((longitude == 0 && latitude == 0) || 
            (Math.Abs(longitude) < 0.001 && Math.Abs(latitude) < 0.001))
        {
            return (true, "Coordinates near (0,0) - likely null island error");
        }
        
        return (false, null);
    }
}
