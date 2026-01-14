using System.Globalization;

namespace StrataVelyx.Services.DataValidation;

/// <summary>
/// Validates and normalizes attribute data with type conversion
/// </summary>
public class AttributeValidator
{
    /// <summary>
    /// Tries to parse string to double, handling common formats
    /// </summary>
    public (bool success, double value, string? error) TryParseDouble(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, 0, $"{fieldName} is empty");
        
        input = input.Trim();
        
        // Handle common numeric suffixes
        input = input.Replace(",", ""); // Remove thousand separators
        
        // Try standard parse
        if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            return (true, value, null);
        
        // Try with different culture
        if (double.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
            return (true, value, null);
        
        return (false, 0, $"{fieldName} '{input}' is not a valid number");
    }
    
    /// <summary>
    /// Tries to parse string to int
    /// </summary>
    public (bool success, int value, string? error) TryParseInt(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, 0, $"{fieldName} is empty");
        
        input = input.Trim().Replace(",", "");
        
        if (int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out int value))
            return (true, value, null);
        
        return (false, 0, $"{fieldName} '{input}' is not a valid integer");
    }
    
    /// <summary>
    /// Validates and normalizes string field
    /// </summary>
    public (string value, bool hasWarning, string? warning) ValidateString(
        string? input, 
        string fieldName, 
        bool required = false, 
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            if (required)
                return (string.Empty, true, $"{fieldName} is required but empty");
            return (string.Empty, false, null);
        }
        
        var value = input.Trim();
        
        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            return (value.Substring(0, maxLength.Value), 
                    true, 
                    $"{fieldName} exceeds max length ({value.Length} > {maxLength.Value}), truncated");
        }
        
        return (value, false, null);
    }
    
    /// <summary>
    /// Validates a well status field
    /// </summary>
    public (string status, bool hasWarning, string? warning) ValidateWellStatus(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ("Unknown", true, "Well status is empty, defaulting to 'Unknown'");
        
        var status = input.Trim();
        var statusLower = status.ToLower();
        
        // Normalize common variations
        var normalized = statusLower switch
        {
            "prod" or "producing" or "production" => "Producer",
            "inj" or "inject" or "injecting" or "injection" => "Injector",
            "act" or "active" => "Active",
            "inact" or "inactive" or "shut-in" or "shutin" => "Inactive",
            "aband" or "abandoned" or "p&a" or "pa" => "Abandoned",
            _ => status
        };
        
        // Warn if not a known status
        var knownStatuses = new[] { "Producer", "Injector", "Active", "Inactive", "Abandoned", "Unknown" };
        if (!knownStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return (normalized, true, $"Unusual well status: '{normalized}'");
        }
        
        return (normalized, false, null);
    }
    
    /// <summary>
    /// Validates numeric value is within reasonable range
    /// </summary>
    public (bool isValid, string? warning) ValidateNumericRange(
        double value, 
        string fieldName, 
        double? min = null, 
        double? max = null)
    {
        if (double.IsNaN(value))
            return (false, $"{fieldName} is NaN");
        
        if (double.IsInfinity(value))
            return (false, $"{fieldName} is Infinity");
        
        if (value < 0 && fieldName.Contains("Rate", StringComparison.OrdinalIgnoreCase))
            return (false, $"{fieldName} is negative ({value}), which is unusual for a rate");
        
        if (min.HasValue && value < min.Value)
            return (false, $"{fieldName} ({value}) is below minimum ({min.Value})");
        
        if (max.HasValue && value > max.Value)
            return (false, $"{fieldName} ({value}) exceeds maximum ({max.Value})");
        
        return (true, null);
    }
    
    /// <summary>
    /// Validates watercut percentage (0-100)
    /// </summary>
    public (double value, bool hasWarning, string? warning) ValidateWatercut(double watercut)
    {
        if (watercut < 0)
            return (0, true, $"Watercut is negative ({watercut}), clamping to 0");
        
        if (watercut > 100)
        {
            // Might be entered as decimal (0-1)
            if (watercut <= 1.0)
                return (watercut * 100, true, "Watercut appears to be decimal (0-1), converted to percentage");
            
            return (100, true, $"Watercut exceeds 100% ({watercut}), clamping to 100");
        }
        
        return (watercut, false, null);
    }
    
    /// <summary>
    /// Validates date field
    /// </summary>
    public (DateTime? value, bool hasWarning, string? warning) ValidateDate(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, false, null);
        
        if (DateTime.TryParse(input, out DateTime date))
        {
            // Check for unreasonable dates
            if (date.Year < 1900)
                return (null, true, $"{fieldName} year is before 1900 ({date.Year})");
            
            if (date > DateTime.Now.AddYears(1))
                return (null, true, $"{fieldName} is in the future ({date})");
            
            return (date, false, null);
        }
        
        return (null, true, $"{fieldName} '{input}' is not a valid date");
    }
}
