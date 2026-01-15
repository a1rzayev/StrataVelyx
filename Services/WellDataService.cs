using CsvHelper;
using CsvHelper.Configuration;
using StrataVelyx.Models;
using System.Globalization;

namespace StrataVelyx.Services;

public class WellDataService
{
    public async Task<List<Well>> ImportWellsFromCsvAsync(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });
        
        var wells = new List<Well>();
        await foreach (var record in csv.GetRecordsAsync<WellCsvRecord>())
        {
            wells.Add(new Well
            {
                Id = record.Id ?? string.Empty,
                Name = record.Name ?? string.Empty,
                Latitude = record.Latitude,
                Longitude = record.Longitude,
                Status = record.Status ?? "Unknown",
                OilRate = record.OilRate,
                WaterRate = record.WaterRate,
                GasRate = record.GasRate,
                Watercut = record.Watercut,
                Field = record.Field ?? string.Empty
            });
        }
        
        return wells;
    }
    
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
    
    public List<Well> FilterWells(List<Well> wells, Func<Well, bool> predicate)
    {
        return wells.Where(predicate).ToList();
    }
}
