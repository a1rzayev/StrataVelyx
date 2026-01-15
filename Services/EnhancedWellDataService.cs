using CsvHelper;
using CsvHelper.Configuration;
using StrataVelyx.Models;
using StrataVelyx.Services.DataValidation;
using System.Diagnostics;
using System.Globalization;

namespace StrataVelyx.Services;

/// <summary>
/// Enhanced well data service with validation, error handling, and normalization
/// Phase 3: Handles real-world messy data
/// </summary>
public class EnhancedWellDataService
{
    private readonly CoordinateValidator _coordinateValidator;
    private readonly AttributeValidator _attributeValidator;
    private readonly SpatialEngine _spatialEngine;
    
    public EnhancedWellDataService()
    {
        _coordinateValidator = new CoordinateValidator();
        _attributeValidator = new AttributeValidator();
        _spatialEngine = new SpatialEngine();
    }
    
    /// <summary>
    /// Imports wells from CSV with comprehensive validation and error handling
    /// BAD ROWS DON'T KILL THE IMPORT - they're logged and skipped
    /// </summary>
    public async Task<ImportResult<Well>> ImportWellsFromCsvAsync(string filePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult<Well>();
        var validationResult = new ValidationResult { IsValid = true };
        
        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null // Don't throw on bad data
            });
            
            int rowNumber = 0;
            
            await foreach (var record in csv.GetRecordsAsync<WellCsvRecord>())
            {
                rowNumber++;
                result.TotalRows++;
                
                // Validate and convert this row
                var (well, rowValidation) = ValidateAndConvertWellRecord(record, rowNumber);
                
                if (rowValidation.IsValid)
                {
                    result.ImportedItems.Add(well!);
                    result.SuccessfulRows++;
                    
                    // Add warnings to overall result
                    foreach (var warning in rowValidation.Warnings)
                    {
                        validationResult.AddWarning(warning.Field, warning.Message, warning.RowNumber);
                    }
                }
                else
                {
                    // Row failed validation - log errors but continue
                    result.FailedRows++;
                    if (well != null)
                    {
                        result.FailedItems.Add(well);
                    }
                    
                    foreach (var error in rowValidation.Errors)
                    {
                        validationResult.AddError(error.Field, error.Message, error.RowNumber);
                    }
                }
            }
            
            result.Success = result.SuccessfulRows > 0;
            result.ValidationResult = validationResult;
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Duration = stopwatch.Elapsed;
            validationResult.AddError("File", $"CSV import failed: {ex.Message}");
            result.ValidationResult = validationResult;
            return result;
        }
    }
    
    /// <summary>
    /// Validates and converts a single CSV record to a Well object
    /// Returns null if validation fails critically
    /// </summary>
    private (Well? well, ValidationResult validation) ValidateAndConvertWellRecord(
        WellCsvRecord record, 
        int rowNumber)
    {
        var validation = new ValidationResult { IsValid = true };
        
        // Required fields
        if (string.IsNullOrWhiteSpace(record.Id))
        {
            validation.AddError("Id", "Well ID is required", rowNumber);
            return (null, validation);
        }
        
        if (string.IsNullOrWhiteSpace(record.Name))
        {
            validation.AddError("Name", "Well Name is required", rowNumber);
            return (null, validation);
        }
        
        // Validate coordinates
        var (coordValid, coordError) = _coordinateValidator.ValidateCoordinates(
            record.Longitude, 
            record.Latitude);
        
        if (!coordValid)
        {
            validation.AddError("Coordinates", coordError!, rowNumber);
            return (null, validation);
        }
        
        // Try to fix common coordinate issues
        var (fixedLon, fixedLat, wasFixed, fixApplied) = _coordinateValidator.TryFixCoordinates(
            record.Longitude, 
            record.Latitude);
        
        if (wasFixed)
        {
            validation.AddWarning("Coordinates", 
                $"Coordinates auto-fixed: {fixApplied} (was: {record.Longitude}, {record.Latitude})", 
                rowNumber);
            record.Longitude = fixedLon;
            record.Latitude = fixedLat;
        }
        
        // Check coordinate precision
        var (precisionWarning, precisionMessage) = _coordinateValidator.CheckCoordinatePrecision(
            record.Longitude, 
            record.Latitude);
        
        if (precisionWarning)
        {
            validation.AddWarning("Coordinates", precisionMessage!, rowNumber);
        }
        
        // Validate status
        var (status, statusWarning, statusMessage) = _attributeValidator.ValidateWellStatus(record.Status);
        if (statusWarning)
        {
            validation.AddWarning("Status", statusMessage!, rowNumber);
        }
        
        // Validate rates (must be non-negative)
        var oilRate = Math.Max(0, record.OilRate);
        if (record.OilRate < 0)
        {
            validation.AddWarning("OilRate", 
                $"Negative oil rate ({record.OilRate}) set to 0", 
                rowNumber);
        }
        
        var waterRate = Math.Max(0, record.WaterRate);
        if (record.WaterRate < 0)
        {
            validation.AddWarning("WaterRate", 
                $"Negative water rate ({record.WaterRate}) set to 0", 
                rowNumber);
        }
        
        var gasRate = Math.Max(0, record.GasRate);
        if (record.GasRate < 0)
        {
            validation.AddWarning("GasRate", 
                $"Negative gas rate ({record.GasRate}) set to 0", 
                rowNumber);
        }
        
        // Validate watercut
        var (watercut, watercutWarning, watercutMessage) = _attributeValidator.ValidateWatercut(record.Watercut);
        if (watercutWarning)
        {
            validation.AddWarning("Watercut", watercutMessage!, rowNumber);
        }
        
        // Validate field name
        var (fieldName, fieldWarning, fieldMessage) = _attributeValidator.ValidateString(
            record.Field, 
            "Field", 
            required: false, 
            maxLength: 100);
        
        if (fieldWarning)
        {
            validation.AddWarning("Field", fieldMessage!, rowNumber);
        }
        
        // Create Well object
        var well = new Well
        {
            Id = record.Id.Trim(),
            Name = record.Name.Trim(),
            Latitude = fixedLat,
            Longitude = fixedLon,
            Status = status,
            OilRate = oilRate,
            WaterRate = waterRate,
            GasRate = gasRate,
            Watercut = watercut,
            Field = fieldName,
            LastUpdate = DateTime.Now
        };
        
        return (well, validation);
    }
    
    /// <summary>
    /// Exports wells to CSV
    /// </summary>
    public async Task ExportWellsToCsvAsync(List<Well> wells, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        await csv.WriteRecordsAsync(wells.Select(w => new WellCsvRecord
        {
            Id = w.Id,
            Name = w.Name,
            Latitude = w.Latitude,
            Longitude = w.Longitude,
            Status = w.Status,
            OilRate = w.OilRate,
            WaterRate = w.WaterRate,
            GasRate = w.GasRate,
            Watercut = w.Watercut,
            Field = w.Field
        }));
    }
    
    /// <summary>
    /// Filters wells using spatial and attribute criteria
    /// </summary>
    public List<Well> FilterWells(List<Well> wells, Func<Well, bool> predicate)
    {
        return wells.Where(predicate).ToList();
    }
    
    /// <summary>
    /// Validates a list of wells and returns validation report
    /// </summary>
    public ValidationResult ValidateWells(List<Well> wells)
    {
        var validation = new ValidationResult { IsValid = true };
        
        for (int i = 0; i < wells.Count; i++)
        {
            var well = wells[i];
            
            // Check for duplicate IDs
            if (wells.Count(w => w.Id == well.Id) > 1)
            {
                validation.AddError("Id", $"Duplicate well ID: {well.Id}", i + 1);
            }
            
            // Validate coordinates
            var (coordValid, coordError) = _coordinateValidator.ValidateCoordinates(
                well.Longitude, 
                well.Latitude);
            
            if (!coordValid)
            {
                validation.AddError("Coordinates", $"{well.Name}: {coordError}", i + 1);
            }
        }
        
        return validation;
    }
}

/// <summary>
/// CSV record structure for well import
/// </summary>
public class WellCsvRecord
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Status { get; set; }
    public double OilRate { get; set; }
    public double WaterRate { get; set; }
    public double GasRate { get; set; }
    public double Watercut { get; set; }
    public string? Field { get; set; }
}
