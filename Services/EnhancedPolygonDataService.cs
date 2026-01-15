using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using StrataVelyx.Models;
using StrataVelyx.Services.DataValidation;
using System.Diagnostics;
using System.Text.Json;
using NTSPolygon = NetTopologySuite.Geometries.Polygon;

namespace StrataVelyx.Services;

/// <summary>
/// Enhanced polygon data service with validation and error handling
/// Supports GeoJSON, WKT, and WKB formats
/// </summary>
public class EnhancedPolygonDataService
{
    private readonly WKTReader _wktReader = new();
    private readonly WKTWriter _wktWriter = new();
    private readonly GeoJsonReader _geoJsonReader = new();
    private readonly GeoJsonWriter _geoJsonWriter = new();
    private readonly SpatialEngine _spatialEngine;
    
    public EnhancedPolygonDataService()
    {
        _spatialEngine = new SpatialEngine();
    }
    
    /// <summary>
    /// Imports polygons from GeoJSON with validation
    /// BAD FEATURES DON'T KILL THE IMPORT
    /// </summary>
    public async Task<ImportResult<FieldPolygon>> ImportPolygonsFromGeoJsonAsync(string filePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult<FieldPolygon>();
        var validationResult = new ValidationResult { IsValid = true };
        
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var featureCollection = _geoJsonReader.Read<NetTopologySuite.Features.FeatureCollection>(json);
            
            int featureNumber = 0;
            
            foreach (var feat in featureCollection)
            {
                featureNumber++;
                result.TotalRows++;
                
                // Validate and convert feature
                var feature = feat as NetTopologySuite.Features.Feature;
                if (feature == null)
                {
                    result.FailedRows++;
                    validationResult.AddError("Feature", "Invalid feature type", featureNumber);
                    continue;
                }
                
                var (polygon, featureValidation) = ValidateAndConvertFeature(feature, featureNumber);
                
                if (featureValidation.IsValid && polygon != null)
                {
                    result.ImportedItems.Add(polygon);
                    result.SuccessfulRows++;
                    
                    // Add warnings
                    foreach (var warning in featureValidation.Warnings)
                    {
                        validationResult.AddWarning(warning.Field, warning.Message, warning.RowNumber);
                    }
                }
                else
                {
                    result.FailedRows++;
                    if (polygon != null)
                    {
                        result.FailedItems.Add(polygon);
                    }
                    
                    foreach (var error in featureValidation.Errors)
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
            validationResult.AddError("File", $"GeoJSON import failed: {ex.Message}");
            result.ValidationResult = validationResult;
            return result;
        }
    }
    
    /// <summary>
    /// Validates and converts a GeoJSON feature to FieldPolygon
    /// </summary>
    private (FieldPolygon? polygon, ValidationResult validation) ValidateAndConvertFeature(
        NetTopologySuite.Features.Feature feature, 
        int featureNumber)
    {
        var validation = new ValidationResult { IsValid = true };
        
        // Check geometry type
        if (feature.Geometry == null)
        {
            validation.AddError("Geometry", "Feature has no geometry", featureNumber);
            return (null, validation);
        }
        
        if (feature.Geometry is not NTSPolygon polygon)
        {
            validation.AddError("Geometry", 
                $"Expected Polygon, got {feature.Geometry.GeometryType}", 
                featureNumber);
            return (null, validation);
        }
        
        // Validate geometry  
        if (!polygon.IsValid)
        {
            // Try to fix
            var fixedGeometry = _spatialEngine.FixGeometry(polygon);
            if (fixedGeometry is NTSPolygon fixedPolygon)
            {
                polygon = fixedPolygon;
                validation.AddWarning("Geometry", 
                    "Geometry was invalid, auto-fixed", 
                    featureNumber);
            }
            else
            {
                validation.AddError("Geometry", 
                    "Invalid geometry could not be fixed", 
                    featureNumber);
                return (null, validation);
            }
        }
        
        // Extract attributes
        var attributes = feature.Attributes;
        string id, name, type;
        
        if (attributes != null)
        {
            id = attributes["id"]?.ToString() ?? Guid.NewGuid().ToString();
            name = attributes["name"]?.ToString() ?? $"Feature_{featureNumber}";
            type = attributes["type"]?.ToString() ?? "Polygon";
            
            if (attributes["id"] == null)
            {
                validation.AddWarning("Attributes", 
                    $"Feature missing 'id', generated: {id}", 
                    featureNumber);
            }
            
            if (attributes["name"] == null)
            {
                validation.AddWarning("Attributes", 
                    $"Feature missing 'name', using: {name}", 
                    featureNumber);
            }
        }
        else
        {
            id = Guid.NewGuid().ToString();
            name = $"Feature_{featureNumber}";
            type = "Polygon";
            validation.AddWarning("Attributes", "Feature has no attributes", featureNumber);
        }
        
        // Create FieldPolygon
        var fieldPolygon = new FieldPolygon
        {
            Id = id,
            Name = name,
            Type = type,
            Geometry = polygon
        };
        
        // Copy all attributes to properties
        if (attributes != null)
        {
            foreach (var attrName in attributes.GetNames())
            {
                var value = attributes[attrName];
                if (value != null)
                {
                    fieldPolygon.Properties[attrName] = value.ToString() ?? string.Empty;
                }
            }
        }
        
        return (fieldPolygon, validation);
    }
    
    /// <summary>
    /// Exports polygons to GeoJSON
    /// </summary>
    public async Task ExportPolygonsToGeoJsonAsync(List<FieldPolygon> polygons, string filePath)
    {
        var featureCollection = new NetTopologySuite.Features.FeatureCollection();
        
        foreach (var polygon in polygons)
        {
            var attributes = new NetTopologySuite.Features.AttributesTable
            {
                { "id", polygon.Id },
                { "name", polygon.Name },
                { "type", polygon.Type }
            };
            
            // Add all properties
            foreach (var prop in polygon.Properties)
            {
                if (!attributes.Exists(prop.Key))
                {
                    attributes.Add(prop.Key, prop.Value);
                }
            }
            
            var feature = new NetTopologySuite.Features.Feature(polygon.Geometry, attributes);
            featureCollection.Add(feature);
        }
        
        var geoJson = _geoJsonWriter.Write(featureCollection);
        await File.WriteAllTextAsync(filePath, geoJson);
    }
    
    /// <summary>
    /// Imports polygon from WKT string
    /// </summary>
    public (FieldPolygon? polygon, ValidationResult validation) ImportPolygonFromWkt(
        string wkt, 
        string id, 
        string name, 
        string type)
    {
        var validation = new ValidationResult { IsValid = true };
        
        try
        {
            var geometry = _wktReader.Read(wkt);
            
            if (geometry is not NTSPolygon polygon)
            {
                validation.AddError("Geometry", 
                    $"WKT does not represent a polygon, got: {geometry.GeometryType}");
                return (null, validation);
            }
            
            // Validate geometry
            if (!polygon.IsValid)
            {
                var fixedGeometry = _spatialEngine.FixGeometry(polygon);
                if (fixedGeometry is NTSPolygon fixedPolygon)
                {
                    polygon = fixedPolygon;
                    validation.AddWarning("Geometry", "Geometry was invalid, auto-fixed");
                }
                else
                {
                    validation.AddError("Geometry", "Invalid geometry could not be fixed");
                    return (null, validation);
                }
            }
            
            var fieldPolygon = new FieldPolygon
            {
                Id = id,
                Name = name,
                Type = type,
                Geometry = polygon
            };
            
            return (fieldPolygon, validation);
        }
        catch (Exception ex)
        {
            validation.AddError("WKT", $"Failed to parse WKT: {ex.Message}");
            return (null, validation);
        }
    }
    
    /// <summary>
    /// Exports polygon to WKT string
    /// </summary>
    public string ExportPolygonToWkt(FieldPolygon polygon)
    {
        return _wktWriter.Write(polygon.Geometry);
    }
    
    /// <summary>
    /// Validates a list of polygons
    /// </summary>
    public ValidationResult ValidatePolygons(List<FieldPolygon> polygons)
    {
        var validation = new ValidationResult { IsValid = true };
        
        for (int i = 0; i < polygons.Count; i++)
        {
            var polygon = polygons[i];
            
            // Check for duplicate IDs
            if (polygons.Count(p => p.Id == polygon.Id) > 1)
            {
                validation.AddError("Id", $"Duplicate polygon ID: {polygon.Id}", i + 1);
            }
            
            // Validate geometry
            if (!polygon.Geometry.IsValid)
            {
                validation.AddError("Geometry", $"{polygon.Name}: Invalid geometry", i + 1);
            }
            
            // Check area
            var areaMeters = _spatialEngine.CalculateAreaSquareMeters(polygon.Geometry);
            if (areaMeters < 100) // Less than 100 m²
            {
                validation.AddWarning("Geometry", 
                    $"{polygon.Name}: Very small area ({areaMeters:F0} m²)", 
                    i + 1);
            }
        }
        
        return validation;
    }
}
