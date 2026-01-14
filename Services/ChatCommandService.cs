using StrataVelyx.Models;
using System.Text.RegularExpressions;

namespace StrataVelyx.Services;

public class ChatCommandService
{
    public ChatCommand ParseCommand(string input)
    {
        input = input.Trim().ToLower();
        
        // Command 1: Import wells from file
        if (input.Contains("import") && input.Contains("well"))
        {
            return new ChatCommand
            {
                Type = CommandType.ImportWells,
                Message = "Please select a CSV file to import wells",
                Parameters = new Dictionary<string, string>()
            };
        }
        
        // Command 2: Show/filter by status (injectors, producers, etc.)
        if (input.Contains("show") || input.Contains("display") || input.Contains("filter"))
        {
            if (input.Contains("injector"))
            {
                return new ChatCommand
                {
                    Type = CommandType.FilterByStatus,
                    Message = "Filtering wells: showing injectors only",
                    Parameters = new Dictionary<string, string> { { "status", "Injector" } }
                };
            }
            if (input.Contains("producer"))
            {
                return new ChatCommand
                {
                    Type = CommandType.FilterByStatus,
                    Message = "Filtering wells: showing producers only",
                    Parameters = new Dictionary<string, string> { { "status", "Producer" } }
                };
            }
            if (input.Contains("active"))
            {
                return new ChatCommand
                {
                    Type = CommandType.FilterByStatus,
                    Message = "Filtering wells: showing active wells only",
                    Parameters = new Dictionary<string, string> { { "status", "Active" } }
                };
            }
        }
        
        // Command 3: Filter by watercut
        var watercutMatch = Regex.Match(input, @"watercut\s*([><=]+)\s*(\d+(?:\.\d+)?)");
        if (watercutMatch.Success)
        {
            var operator_ = watercutMatch.Groups[1].Value;
            var value = watercutMatch.Groups[2].Value;
            
            return new ChatCommand
            {
                Type = CommandType.FilterByWatercut,
                Message = $"Filtering wells where watercut {operator_} {value}%",
                Parameters = new Dictionary<string, string> 
                { 
                    { "operator", operator_ },
                    { "value", value }
                }
            };
        }
        
        // Command 4: Select wells within polygon
        if (input.Contains("select") && input.Contains("polygon"))
        {
            return new ChatCommand
            {
                Type = CommandType.SelectInPolygon,
                Message = "Draw a polygon on the map to select wells",
                Parameters = new Dictionary<string, string>()
            };
        }
        
        // Command 5: Export selected wells
        if (input.Contains("export"))
        {
            return new ChatCommand
            {
                Type = CommandType.ExportWells,
                Message = "Exporting selected wells to CSV",
                Parameters = new Dictionary<string, string>()
            };
        }
        
        // Show all wells
        if (input.Contains("show all") || input.Contains("display all") || input.Contains("reset"))
        {
            return new ChatCommand
            {
                Type = CommandType.ShowAll,
                Message = "Showing all wells",
                Parameters = new Dictionary<string, string>()
            };
        }
        
        // Unknown command
        return new ChatCommand
        {
            Type = CommandType.Unknown,
            Message = GetHelpMessage(),
            Parameters = new Dictionary<string, string>()
        };
    }
    
    public string GetHelpMessage()
    {
        return @"Available commands:
• Import wells from file [filename]
• Show injectors only
• Show producers only
• Filter wells where watercut > [value]
• Select wells within polygon
• Export selected wells to CSV
• Show all wells";
    }
}

public class ChatCommand
{
    public CommandType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public enum CommandType
{
    Unknown,
    ImportWells,
    FilterByStatus,
    FilterByWatercut,
    SelectInPolygon,
    ExportWells,
    ShowAll
}
