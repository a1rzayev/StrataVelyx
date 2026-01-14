namespace StrataVelyx.Services.DataValidation;

/// <summary>
/// Result of data validation with detailed error information
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    
    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
    
    public void AddError(string field, string message, int? rowNumber = null)
    {
        Errors.Add(new ValidationError
        {
            Field = field,
            Message = message,
            RowNumber = rowNumber
        });
        IsValid = false;
    }
    
    public void AddWarning(string field, string message, int? rowNumber = null)
    {
        Warnings.Add(new ValidationWarning
        {
            Field = field,
            Message = message,
            RowNumber = rowNumber
        });
    }
    
    public string GetSummary()
    {
        var summary = $"Validation: {(IsValid ? "PASSED" : "FAILED")}\n";
        if (HasErrors)
        {
            summary += $"Errors: {Errors.Count}\n";
            foreach (var error in Errors.Take(10))
            {
                summary += $"  - Row {error.RowNumber}: {error.Field} - {error.Message}\n";
            }
            if (Errors.Count > 10)
                summary += $"  ... and {Errors.Count - 10} more errors\n";
        }
        if (HasWarnings)
        {
            summary += $"Warnings: {Warnings.Count}\n";
        }
        return summary;
    }
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RowNumber { get; set; }
}

public class ValidationWarning
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RowNumber { get; set; }
}

/// <summary>
/// Result of a data import operation
/// </summary>
public class ImportResult<T>
{
    public bool Success { get; set; }
    public List<T> ImportedItems { get; set; } = new();
    public List<T> FailedItems { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public TimeSpan Duration { get; set; }
    
    public string GetSummary()
    {
        return $"Import completed in {Duration.TotalSeconds:F2}s\n" +
               $"Total rows: {TotalRows}\n" +
               $"Successful: {SuccessfulRows}\n" +
               $"Failed: {FailedRows}\n" +
               $"{ValidationResult.GetSummary()}";
    }
}
