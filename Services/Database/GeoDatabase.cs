using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using StrataVelyx.Models;
using System.Data;
using NTSPolygon = NetTopologySuite.Geometries.Polygon;

namespace StrataVelyx.Services.Database;

/// <summary>
/// SQLite database with spatial support for StrataVelyx
/// Stores wells (points) and polygons with geometry in WKT format
/// </summary>
public class GeoDatabase : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly WKTWriter _wktWriter = new();
    private readonly WKTReader _wktReader = new();
    
    public GeoDatabase(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }
    
    /// <summary>
    /// Opens database connection and creates tables if they don't exist
    /// </summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();
        
        await CreateTablesAsync();
    }
    
    /// <summary>
    /// Creates database schema
    /// </summary>
    private async Task CreateTablesAsync()
    {
        var createWellsTable = @"
            CREATE TABLE IF NOT EXISTS wells (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                geometry TEXT NOT NULL,  -- WKT: POINT(lon lat)
                status TEXT,
                oil_rate REAL DEFAULT 0,
                water_rate REAL DEFAULT 0,
                gas_rate REAL DEFAULT 0,
                watercut REAL DEFAULT 0,
                field TEXT,
                last_update TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            );
        ";
        
        var createPolygonsTable = @"
            CREATE TABLE IF NOT EXISTS polygons (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                type TEXT,  -- Block, Pad, Lease, Field
                geometry TEXT NOT NULL,  -- WKT: POLYGON(...)
                area_sqm REAL,  -- Pre-calculated area in square meters
                centroid_lon REAL,
                centroid_lat REAL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            );
        ";
        
        var createPropertiesTable = @"
            CREATE TABLE IF NOT EXISTS polygon_properties (
                polygon_id TEXT NOT NULL,
                key TEXT NOT NULL,
                value TEXT,
                PRIMARY KEY (polygon_id, key),
                FOREIGN KEY (polygon_id) REFERENCES polygons(id) ON DELETE CASCADE
            );
        ";
        
        // Create spatial index tables (bbox-based for simple queries)
        var createWellsSpatialIndex = @"
            CREATE TABLE IF NOT EXISTS wells_spatial_index (
                id TEXT PRIMARY KEY,
                min_lon REAL,
                min_lat REAL,
                max_lon REAL,
                max_lat REAL,
                FOREIGN KEY (id) REFERENCES wells(id) ON DELETE CASCADE
            );
        ";
        
        var createPolygonsSpatialIndex = @"
            CREATE TABLE IF NOT EXISTS polygons_spatial_index (
                id TEXT PRIMARY KEY,
                min_lon REAL,
                min_lat REAL,
                max_lon REAL,
                max_lat REAL,
                FOREIGN KEY (id) REFERENCES polygons(id) ON DELETE CASCADE
            );
        ";
        
        // Create indexes
        var createIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_wells_status ON wells(status);
            CREATE INDEX IF NOT EXISTS idx_wells_field ON wells(field);
            CREATE INDEX IF NOT EXISTS idx_polygons_type ON polygons(type);
            CREATE INDEX IF NOT EXISTS idx_wells_spatial_bbox ON wells_spatial_index(min_lon, min_lat, max_lon, max_lat);
            CREATE INDEX IF NOT EXISTS idx_polygons_spatial_bbox ON polygons_spatial_index(min_lon, min_lat, max_lon, max_lat);
        ";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = createWellsTable;
        await cmd.ExecuteNonQueryAsync();
        
        cmd.CommandText = createPolygonsTable;
        await cmd.ExecuteNonQueryAsync();
        
        cmd.CommandText = createPropertiesTable;
        await cmd.ExecuteNonQueryAsync();
        
        cmd.CommandText = createWellsSpatialIndex;
        await cmd.ExecuteNonQueryAsync();
        
        cmd.CommandText = createPolygonsSpatialIndex;
        await cmd.ExecuteNonQueryAsync();
        
        cmd.CommandText = createIndexes;
        await cmd.ExecuteNonQueryAsync();
    }
    
    #region Well Operations
    
    /// <summary>
    /// Inserts or updates a well
    /// </summary>
    public async Task SaveWellAsync(Well well)
    {
        var engine = new SpatialEngine();
        var point = engine.CreatePoint(well.Longitude, well.Latitude);
        var wkt = _wktWriter.Write(point);
        
        var sql = @"
            INSERT OR REPLACE INTO wells (
                id, name, latitude, longitude, geometry, status,
                oil_rate, water_rate, gas_rate, watercut, field, last_update, updated_at
            ) VALUES (
                @id, @name, @lat, @lon, @geom, @status,
                @oil, @water, @gas, @watercut, @field, @lastUpdate, CURRENT_TIMESTAMP
            );
        ";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", well.Id);
        cmd.Parameters.AddWithValue("@name", well.Name);
        cmd.Parameters.AddWithValue("@lat", well.Latitude);
        cmd.Parameters.AddWithValue("@lon", well.Longitude);
        cmd.Parameters.AddWithValue("@geom", wkt);
        cmd.Parameters.AddWithValue("@status", well.Status);
        cmd.Parameters.AddWithValue("@oil", well.OilRate);
        cmd.Parameters.AddWithValue("@water", well.WaterRate);
        cmd.Parameters.AddWithValue("@gas", well.GasRate);
        cmd.Parameters.AddWithValue("@watercut", well.Watercut);
        cmd.Parameters.AddWithValue("@field", well.Field ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lastUpdate", well.LastUpdate?.ToString("o") ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        
        // Update spatial index
        await UpdateWellSpatialIndexAsync(well.Id, well.Longitude, well.Latitude);
    }
    
    /// <summary>
    /// Bulk insert wells (faster for large datasets)
    /// </summary>
    public async Task SaveWellsBulkAsync(List<Well> wells)
    {
        var engine = new SpatialEngine();
        
        using var transaction = _connection!.BeginTransaction();
        
        try
        {
            foreach (var well in wells)
            {
                await SaveWellAsync(well);
            }
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    /// <summary>
    /// Gets a well by ID
    /// </summary>
    public async Task<Well?> GetWellAsync(string id)
    {
        var sql = "SELECT * FROM wells WHERE id = @id";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadWellFromReader(reader);
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all wells
    /// </summary>
    public async Task<List<Well>> GetAllWellsAsync()
    {
        var wells = new List<Well>();
        var sql = "SELECT * FROM wells ORDER BY name";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            wells.Add(ReadWellFromReader(reader));
        }
        
        return wells;
    }
    
    /// <summary>
    /// Gets wells by status
    /// </summary>
    public async Task<List<Well>> GetWellsByStatusAsync(string status)
    {
        var wells = new List<Well>();
        var sql = "SELECT * FROM wells WHERE status = @status ORDER BY name";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@status", status);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            wells.Add(ReadWellFromReader(reader));
        }
        
        return wells;
    }
    
    /// <summary>
    /// Gets wells within bounding box (using spatial index)
    /// </summary>
    public async Task<List<Well>> GetWellsInBoundingBoxAsync(
        double minLon, double minLat, double maxLon, double maxLat)
    {
        var wells = new List<Well>();
        var sql = @"
            SELECT w.* 
            FROM wells w
            INNER JOIN wells_spatial_index si ON w.id = si.id
            WHERE si.max_lon >= @minLon 
              AND si.min_lon <= @maxLon
              AND si.max_lat >= @minLat 
              AND si.min_lat <= @maxLat
            ORDER BY w.name
        ";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@minLon", minLon);
        cmd.Parameters.AddWithValue("@minLat", minLat);
        cmd.Parameters.AddWithValue("@maxLon", maxLon);
        cmd.Parameters.AddWithValue("@maxLat", maxLat);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            wells.Add(ReadWellFromReader(reader));
        }
        
        return wells;
    }
    
    private Well ReadWellFromReader(SqliteDataReader reader)
    {
        return new Well
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Latitude = reader.GetDouble(reader.GetOrdinal("latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("longitude")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            OilRate = reader.GetDouble(reader.GetOrdinal("oil_rate")),
            WaterRate = reader.GetDouble(reader.GetOrdinal("water_rate")),
            GasRate = reader.GetDouble(reader.GetOrdinal("gas_rate")),
            Watercut = reader.GetDouble(reader.GetOrdinal("watercut")),
            Field = reader.IsDBNull(reader.GetOrdinal("field")) ? string.Empty : reader.GetString(reader.GetOrdinal("field")),
            LastUpdate = reader.IsDBNull(reader.GetOrdinal("last_update")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_update")))
        };
    }
    
    private async Task UpdateWellSpatialIndexAsync(string id, double lon, double lat)
    {
        var sql = @"
            INSERT OR REPLACE INTO wells_spatial_index (id, min_lon, min_lat, max_lon, max_lat)
            VALUES (@id, @lon, @lat, @lon, @lat);
        ";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@lon", lon);
        cmd.Parameters.AddWithValue("@lat", lat);
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    #endregion
    
    #region Polygon Operations
    
    /// <summary>
    /// Saves a polygon with its properties
    /// </summary>
    public async Task SavePolygonAsync(FieldPolygon polygon)
    {
        var engine = new SpatialEngine();
        var wkt = _wktWriter.Write(polygon.Geometry);
        var centroid = polygon.Geometry.Centroid;
        var areaMeters = engine.CalculateAreaSquareMeters(polygon.Geometry);
        
        using var transaction = _connection!.BeginTransaction();
        
        try
        {
            // Insert/update polygon
            var sql = @"
                INSERT OR REPLACE INTO polygons (
                    id, name, type, geometry, area_sqm, centroid_lon, centroid_lat, updated_at
                ) VALUES (
                    @id, @name, @type, @geom, @area, @clon, @clat, CURRENT_TIMESTAMP
                );
            ";
            
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", polygon.Id);
            cmd.Parameters.AddWithValue("@name", polygon.Name);
            cmd.Parameters.AddWithValue("@type", polygon.Type);
            cmd.Parameters.AddWithValue("@geom", wkt);
            cmd.Parameters.AddWithValue("@area", areaMeters);
            cmd.Parameters.AddWithValue("@clon", centroid.X);
            cmd.Parameters.AddWithValue("@clat", centroid.Y);
            
            await cmd.ExecuteNonQueryAsync();
            
            // Delete old properties
            cmd.CommandText = "DELETE FROM polygon_properties WHERE polygon_id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", polygon.Id);
            await cmd.ExecuteNonQueryAsync();
            
            // Insert properties
            foreach (var prop in polygon.Properties)
            {
                cmd.CommandText = @"
                    INSERT INTO polygon_properties (polygon_id, key, value)
                    VALUES (@id, @key, @value)
                ";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", polygon.Id);
                cmd.Parameters.AddWithValue("@key", prop.Key);
                cmd.Parameters.AddWithValue("@value", prop.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            
            // Update spatial index
            var bbox = polygon.Geometry.EnvelopeInternal;
            await UpdatePolygonSpatialIndexAsync(polygon.Id, bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    /// <summary>
    /// Gets a polygon by ID with its properties
    /// </summary>
    public async Task<FieldPolygon?> GetPolygonAsync(string id)
    {
        var sql = "SELECT * FROM polygons WHERE id = @id";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var polygon = ReadPolygonFromReader(reader);
            
            // Load properties
            polygon.Properties = await GetPolygonPropertiesAsync(id);
            
            return polygon;
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all polygons
    /// </summary>
    public async Task<List<FieldPolygon>> GetAllPolygonsAsync()
    {
        var polygons = new List<FieldPolygon>();
        var sql = "SELECT * FROM polygons ORDER BY name";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var polygon = ReadPolygonFromReader(reader);
            polygon.Properties = await GetPolygonPropertiesAsync(polygon.Id);
            polygons.Add(polygon);
        }
        
        return polygons;
    }
    
    private FieldPolygon ReadPolygonFromReader(SqliteDataReader reader)
    {
        var wkt = reader.GetString(reader.GetOrdinal("geometry"));
        var geometry = _wktReader.Read(wkt) as NTSPolygon;
        
        return new FieldPolygon
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Geometry = geometry!
        };
    }
    
    private async Task<Dictionary<string, string>> GetPolygonPropertiesAsync(string polygonId)
    {
        var properties = new Dictionary<string, string>();
        var sql = "SELECT key, value FROM polygon_properties WHERE polygon_id = @id";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", polygonId);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            properties[reader.GetString(0)] = reader.GetString(1);
        }
        
        return properties;
    }
    
    private async Task UpdatePolygonSpatialIndexAsync(
        string id, double minLon, double minLat, double maxLon, double maxLat)
    {
        var sql = @"
            INSERT OR REPLACE INTO polygons_spatial_index (id, min_lon, min_lat, max_lon, max_lat)
            VALUES (@id, @minLon, @minLat, @maxLon, @maxLat);
        ";
        
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@minLon", minLon);
        cmd.Parameters.AddWithValue("@minLat", minLat);
        cmd.Parameters.AddWithValue("@maxLon", maxLon);
        cmd.Parameters.AddWithValue("@maxLat", maxLat);
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    #endregion
    
    #region Utility
    
    /// <summary>
    /// Gets database statistics
    /// </summary>
    public async Task<DatabaseStats> GetStatsAsync()
    {
        var stats = new DatabaseStats();
        
        using var cmd = _connection!.CreateCommand();
        
        cmd.CommandText = "SELECT COUNT(*) FROM wells";
        stats.WellCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        
        cmd.CommandText = "SELECT COUNT(*) FROM polygons";
        stats.PolygonCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        
        return stats;
    }
    
    #endregion
    
    public void Dispose()
    {
        _connection?.Dispose();
    }
}

public class DatabaseStats
{
    public int WellCount { get; set; }
    public int PolygonCount { get; set; }
}
