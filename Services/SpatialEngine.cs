using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using StrataVelyx.Models;
using NTSPoint = NetTopologySuite.Geometries.Point;

namespace StrataVelyx.Services;

/// <summary>
/// Core spatial engine for StrataVelyx GIS operations
/// Handles all geometry operations, coordinate transformations, and spatial queries
/// </summary>
public class SpatialEngine
{
    private readonly GeometryFactory _geometryFactory;
    private const int WGS84_SRID = 4326; // WGS84 - standard lat/lon
    
    public SpatialEngine()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), WGS84_SRID);
    }
    
    #region Geometry Creation
    
    /// <summary>
    /// Creates a Point geometry from latitude and longitude (WGS84)
    /// </summary>
    public NTSPoint CreatePoint(double longitude, double latitude)
    {
        return _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
    }
    
    /// <summary>
    /// Creates a Polygon from a list of coordinates
    /// First and last coordinate must be the same (closed ring)
    /// </summary>
    public Polygon CreatePolygon(List<Coordinate> coordinates)
    {
        if (coordinates.Count < 4)
            throw new ArgumentException("Polygon requires at least 4 coordinates (3 unique + closing point)");
        
        // Ensure ring is closed
        if (!coordinates.First().Equals2D(coordinates.Last()))
        {
            coordinates.Add(new Coordinate(coordinates.First()));
        }
        
        var linearRing = _geometryFactory.CreateLinearRing(coordinates.ToArray());
        return _geometryFactory.CreatePolygon(linearRing);
    }
    
    /// <summary>
    /// Creates a Polygon from lon/lat pairs
    /// </summary>
    public Polygon CreatePolygonFromLonLat(List<(double lon, double lat)> points)
    {
        var coordinates = points.Select(p => new Coordinate(p.lon, p.lat)).ToList();
        return CreatePolygon(coordinates);
    }
    
    /// <summary>
    /// Creates a LineString from coordinates
    /// </summary>
    public LineString CreateLineString(List<Coordinate> coordinates)
    {
        if (coordinates.Count < 2)
            throw new ArgumentException("LineString requires at least 2 coordinates");
        
        return _geometryFactory.CreateLineString(coordinates.ToArray());
    }
    
    /// <summary>
    /// Creates a LineString from lon/lat pairs (for pipelines, faults)
    /// </summary>
    public LineString CreateLineStringFromLonLat(List<(double lon, double lat)> points)
    {
        var coordinates = points.Select(p => new Coordinate(p.lon, p.lat)).ToArray();
        return _geometryFactory.CreateLineString(coordinates);
    }
    
    #endregion
    
    #region Spatial Operations - Contains/Within
    
    /// <summary>
    /// Tests if a point is within a polygon
    /// </summary>
    public bool IsPointWithinPolygon(NTSPoint point, Polygon polygon)
    {
        return polygon.Contains(point);
    }
    
    /// <summary>
    /// Tests if a well is within a polygon
    /// </summary>
    public bool IsWellWithinPolygon(Well well, Polygon polygon)
    {
        var point = CreatePoint(well.Longitude, well.Latitude);
        return polygon.Contains(point);
    }
    
    /// <summary>
    /// Returns all wells within a polygon
    /// </summary>
    public List<Well> SelectWellsWithinPolygon(List<Well> wells, Polygon polygon)
    {
        return wells.Where(w => IsWellWithinPolygon(w, polygon)).ToList();
    }
    
    /// <summary>
    /// Returns all wells within a field polygon
    /// </summary>
    public List<Well> SelectWellsWithinField(List<Well> wells, FieldPolygon field)
    {
        return SelectWellsWithinPolygon(wells, field.Geometry);
    }
    
    #endregion
    
    #region Spatial Operations - Intersects
    
    /// <summary>
    /// Tests if two geometries intersect
    /// </summary>
    public bool Intersects(Geometry geom1, Geometry geom2)
    {
        return geom1.Intersects(geom2);
    }
    
    /// <summary>
    /// Tests if a polygon intersects another polygon
    /// </summary>
    public bool PolygonsIntersect(Polygon poly1, Polygon poly2)
    {
        return poly1.Intersects(poly2);
    }
    
    /// <summary>
    /// Returns intersection geometry of two polygons
    /// </summary>
    public Geometry? GetIntersection(Polygon poly1, Polygon poly2)
    {
        var intersection = poly1.Intersection(poly2);
        return intersection.IsEmpty ? null : intersection;
    }
    
    /// <summary>
    /// Tests if a line intersects a polygon (e.g., pipeline crossing field boundary)
    /// </summary>
    public bool LineIntersectsPolygon(LineString line, Polygon polygon)
    {
        return line.Intersects(polygon);
    }
    
    #endregion
    
    #region Spatial Operations - Buffer
    
    /// <summary>
    /// Creates a buffer around a point
    /// Distance in degrees (approximate, for WGS84)
    /// For precise meter-based buffers, project to UTM first
    /// </summary>
    public Polygon BufferPoint(NTSPoint point, double distanceDegrees)
    {
        var buffer = point.Buffer(distanceDegrees);
        return buffer as Polygon ?? throw new InvalidOperationException("Buffer operation did not produce a polygon");
    }
    
    /// <summary>
    /// Creates a buffer around a well
    /// Distance in degrees (~111km per degree at equator)
    /// </summary>
    public Polygon BufferWell(Well well, double distanceDegrees)
    {
        var point = CreatePoint(well.Longitude, well.Latitude);
        return BufferPoint(point, distanceDegrees);
    }
    
    /// <summary>
    /// Creates a buffer in meters (approximate conversion for WGS84)
    /// More accurate near equator
    /// </summary>
    public Polygon BufferWellMeters(Well well, double meters)
    {
        // Approximate conversion: 1 degree ≈ 111,000 meters at equator
        // Adjust for latitude
        var latRadians = well.Latitude * Math.PI / 180.0;
        var metersPerDegreeLat = 111132.954 - 559.822 * Math.Cos(2 * latRadians);
        var metersPerDegreeLon = 111132.954 * Math.Cos(latRadians);
        
        // Use average for simplicity
        var avgMetersPerDegree = (metersPerDegreeLat + metersPerDegreeLon) / 2.0;
        var distanceDegrees = meters / avgMetersPerDegree;
        
        return BufferWell(well, distanceDegrees);
    }
    
    /// <summary>
    /// Creates a buffer around any geometry
    /// </summary>
    public Geometry Buffer(Geometry geometry, double distance)
    {
        return geometry.Buffer(distance);
    }
    
    #endregion
    
    #region Spatial Operations - Distance
    
    /// <summary>
    /// Calculates distance between two points in degrees
    /// </summary>
    public double Distance(NTSPoint point1, NTSPoint point2)
    {
        return point1.Distance(point2);
    }
    
    /// <summary>
    /// Calculates distance between two wells in degrees
    /// </summary>
    public double DistanceBetweenWells(Well well1, Well well2)
    {
        var point1 = CreatePoint(well1.Longitude, well1.Latitude);
        var point2 = CreatePoint(well2.Longitude, well2.Latitude);
        return Distance(point1, point2);
    }
    
    /// <summary>
    /// Calculates approximate distance in meters between two wells
    /// Uses Haversine formula for accuracy
    /// </summary>
    public double DistanceBetweenWellsMeters(Well well1, Well well2)
    {
        return HaversineDistance(well1.Latitude, well1.Longitude, well2.Latitude, well2.Longitude);
    }
    
    /// <summary>
    /// Haversine formula for calculating distance on a sphere
    /// Returns distance in meters
    /// </summary>
    private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters
        
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
    
    private double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    
    /// <summary>
    /// Finds all wells within a specified distance from a target well
    /// </summary>
    public List<Well> FindWellsWithinDistance(List<Well> wells, Well targetWell, double meters)
    {
        return wells
            .Where(w => w.Id != targetWell.Id && DistanceBetweenWellsMeters(w, targetWell) <= meters)
            .ToList();
    }
    
    /// <summary>
    /// Calculates distance from a point to a geometry
    /// </summary>
    public double DistanceToGeometry(NTSPoint point, Geometry geometry)
    {
        return point.Distance(geometry);
    }
    
    #endregion
    
    #region Bounding Boxes
    
    /// <summary>
    /// Gets the bounding box (envelope) of a geometry
    /// </summary>
    public Envelope GetBoundingBox(Geometry geometry)
    {
        return geometry.EnvelopeInternal;
    }
    
    /// <summary>
    /// Gets the bounding box for a list of wells
    /// </summary>
    public Envelope GetWellsBoundingBox(List<Well> wells)
    {
        if (wells.Count == 0)
            throw new ArgumentException("Cannot calculate bounding box for empty well list");
        
        var envelope = new Envelope();
        foreach (var well in wells)
        {
            envelope.ExpandToInclude(well.Longitude, well.Latitude);
        }
        return envelope;
    }
    
    /// <summary>
    /// Gets the bounding box for a list of polygons
    /// </summary>
    public Envelope GetPolygonsBoundingBox(List<FieldPolygon> polygons)
    {
        if (polygons.Count == 0)
            throw new ArgumentException("Cannot calculate bounding box for empty polygon list");
        
        var envelope = new Envelope();
        foreach (var polygon in polygons)
        {
            envelope.ExpandToInclude(polygon.Geometry.EnvelopeInternal);
        }
        return envelope;
    }
    
    #endregion
    
    #region Geometry Validation
    
    /// <summary>
    /// Validates if a geometry is topologically valid
    /// </summary>
    public bool IsValid(Geometry geometry)
    {
        return geometry.IsValid;
    }
    
    /// <summary>
    /// Gets validation error for invalid geometry
    /// </summary>
    public string? GetValidationError(Geometry geometry)
    {
        if (geometry.IsValid) return null;
        
        var validator = new NetTopologySuite.Operation.Valid.IsValidOp(geometry);
        var error = validator.ValidationError;
        return error?.Message;
    }
    
    /// <summary>
    /// Validates a polygon
    /// </summary>
    public (bool isValid, string? error) ValidatePolygon(Polygon polygon)
    {
        if (!polygon.IsValid)
        {
            return (false, GetValidationError(polygon));
        }
        
        if (polygon.IsEmpty)
        {
            return (false, "Polygon is empty");
        }
        
        if (polygon.Area == 0)
        {
            return (false, "Polygon has zero area");
        }
        
        return (true, null);
    }
    
    /// <summary>
    /// Attempts to fix invalid geometries
    /// </summary>
    public Geometry? FixGeometry(Geometry geometry)
    {
        if (geometry.IsValid) return geometry;
        
        try
        {
            return geometry.Buffer(0); // Buffer(0) often fixes topology issues
        }
        catch
        {
            return null;
        }
    }
    
    #endregion
    
    #region Coordinate System Info
    
    /// <summary>
    /// Gets the SRID (Spatial Reference ID) of a geometry
    /// </summary>
    public int GetSRID(Geometry geometry)
    {
        return geometry.SRID;
    }
    
    /// <summary>
    /// Checks if geometry is in WGS84 (lat/lon)
    /// </summary>
    public bool IsWGS84(Geometry geometry)
    {
        return geometry.SRID == WGS84_SRID || geometry.SRID == 0;
    }
    
    #endregion
    
    #region Area and Length Calculations
    
    /// <summary>
    /// Calculates polygon area in square degrees
    /// For accurate area in m², project to appropriate UTM zone
    /// </summary>
    public double CalculateArea(Polygon polygon)
    {
        return polygon.Area;
    }
    
    /// <summary>
    /// Calculates approximate area in square meters for small polygons
    /// Uses simple projection approximation
    /// </summary>
    public double CalculateAreaSquareMeters(Polygon polygon)
    {
        var centroid = polygon.Centroid;
        var latRadians = centroid.Y * Math.PI / 180.0;
        
        // Meters per degree at this latitude
        var metersPerDegreeLat = 111132.954 - 559.822 * Math.Cos(2 * latRadians);
        var metersPerDegreeLon = 111132.954 * Math.Cos(latRadians);
        
        // Calculate area in square meters (approximate)
        var areaSquareDegrees = polygon.Area;
        return areaSquareDegrees * metersPerDegreeLat * metersPerDegreeLon;
    }
    
    /// <summary>
    /// Calculates line length in degrees
    /// </summary>
    public double CalculateLength(LineString line)
    {
        return line.Length;
    }
    
    /// <summary>
    /// Calculates approximate length in meters
    /// </summary>
    public double CalculateLengthMeters(LineString line)
    {
        double totalMeters = 0;
        var coords = line.Coordinates;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            totalMeters += HaversineDistance(
                coords[i].Y, coords[i].X,
                coords[i + 1].Y, coords[i + 1].X
            );
        }
        
        return totalMeters;
    }
    
    #endregion
}
