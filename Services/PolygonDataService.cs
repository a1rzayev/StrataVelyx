using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using StrataVelyx.Models;
using System.Text.Json;

namespace StrataVelyx.Services;

public class PolygonDataService
{
    private readonly WKTReader _wktReader = new();
    private readonly GeoJsonReader _geoJsonReader = new();
    
    public async Task<List<FieldPolygon>> ImportPolygonsFromGeoJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var featureCollection = _geoJsonReader.Read<NetTopologySuite.Features.FeatureCollection>(json);
        
        var polygons = new List<FieldPolygon>();
        
        foreach (var feature in featureCollection)
        {
            if (feature.Geometry is Polygon polygon && feature.Attributes != null)
            {
                var fieldPolygon = new FieldPolygon
                {
                    Id = feature.Attributes["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Name = feature.Attributes["name"]?.ToString() ?? "Unnamed",
                    Type = feature.Attributes["type"]?.ToString() ?? "Block",
                    Geometry = polygon
                };
                
                // Copy all attributes
                foreach (var attr in feature.Attributes.GetNames())
                {
                    var value = feature.Attributes[attr];
                    if (value != null)
                    {
                        fieldPolygon.Properties[attr] = value.ToString() ?? string.Empty;
                    }
                }
                
                polygons.Add(fieldPolygon);
            }
        }
        
        return polygons;
    }
    
    public FieldPolygon ParseWktPolygon(string wkt, string id, string name, string type)
    {
        var geometry = _wktReader.Read(wkt);
        
        if (geometry is not Polygon polygon)
        {
            throw new ArgumentException("WKT does not represent a valid polygon");
        }
        
        return new FieldPolygon
        {
            Id = id,
            Name = name,
            Type = type,
            Geometry = polygon
        };
    }
    
    public List<Well> SelectWellsInPolygon(List<Well> wells, FieldPolygon polygon)
    {
        var geometryFactory = new GeometryFactory();
        var selectedWells = new List<Well>();
        
        foreach (var well in wells)
        {
            var point = geometryFactory.CreatePoint(new Coordinate(well.Longitude, well.Latitude));
            if (polygon.Geometry.Contains(point))
            {
                selectedWells.Add(well);
            }
        }
        
        return selectedWells;
    }
}
