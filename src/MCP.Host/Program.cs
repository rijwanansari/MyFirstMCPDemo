using System.Text.Json;
using MCP.Client;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCP.Host;

/// <summary>
/// Host application that demonstrates connecting to an MCP server and using its tools.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           MCP Demo Host - Model Context Protocol             ║");
        Console.WriteLine("║                  .NET 10 Implementation                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Determine server path
        var serverPath = GetServerPath(args);
        
        if (string.IsNullOrEmpty(serverPath))
        {
            Console.WriteLine("Usage: MCP.Host [server-path]");
            Console.WriteLine();
            Console.WriteLine("If no server path is provided, it will look for MCP.Server in the default location.");
            Console.WriteLine();
            ShowDemoWithoutServer();
            return;
        }

        await RunDemoWithServerAsync(serverPath);
    }

    private static string? GetServerPath(string[] args)
    {
        if (args.Length > 0 && File.Exists(args[0]))
        {
            return args[0];
        }

        // Try to find the server in common locations
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "MCP.Server.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "MCP.Server", "MCP.Server.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MCP.Server", "bin", "Debug", "net10.0", "MCP.Server.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MCP.Server", "bin", "Release", "net10.0", "MCP.Server.dll")
        };

        foreach (var path in possiblePaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (File.Exists(normalizedPath))
            {
                return normalizedPath;
            }
        }

        return null;
    }

    private static async Task RunDemoWithServerAsync(string serverPath)
    {
        Console.WriteLine($"🔌 Connecting to MCP Server: {serverPath}");
        Console.WriteLine();

        try
        {
            // Create client connection to server
            var transportOptions = new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [serverPath],
                Name = "MCP Demo Host"
            };

            var transport = new StdioClientTransport(transportOptions);

            var client = await McpClientFactory.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new Implementation
                    {
                        Name = "MCP Demo Host",
                        Version = "1.0.0"
                    }
                });

            Console.WriteLine("✅ Connected to MCP Server successfully!");
            Console.WriteLine();

            // List available tools
            await ListToolsAsync(client);

            // Run conversational interactive demo loop
            await RunInteractiveDemoAsync(client);

            // Cleanup
            await client.DisposeAsync();
            Console.WriteLine("👋 Disconnected from server. Goodbye!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error connecting to server: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Make sure the MCP.Server project is built:");
            Console.WriteLine("  dotnet build src/MCP.Server/MCP.Server.csproj");
            Console.WriteLine();
            ShowDemoWithoutServer();
        }
    }

    private static async Task ListToolsAsync(IMcpClient client)
    {
        Console.WriteLine("📋 Available Tools:");
        Console.WriteLine(new string('-', 60));

        var tools = await client.ListToolsAsync();
        foreach (var tool in tools)
        {
            Console.WriteLine($"  • {tool.Name}");
            if (!string.IsNullOrEmpty(tool.Description))
            {
                Console.WriteLine($"    {tool.Description}");
            }
        }

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"Total: {tools.Count} tools available");
        Console.WriteLine();
    }

    private static async Task RunInteractiveDemoAsync(IMcpClient client)
    {
        Console.WriteLine("🎮 Interactive Demo");
        Console.WriteLine("Type one of: weather, calculator, todo, textutility");
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("👉 Which tool would you like to use? ");
            var choice = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(choice))
            {
                Console.WriteLine("  ⚠️ Please enter a tool name or 'exit'.");
                continue;
            }

            if (choice is "exit" or "quit" or "q")
            {
                Console.WriteLine("🛑 Exiting interactive demo...");
                break;
            }

            try
            {
                switch (choice)
                {
                    case "weather":
                    case "weathertool":
                        await InteractiveWeatherAsync(client);
                        break;

                    case "calculator":
                    case "calculatortool":
                        await InteractiveCalculatorAsync(client);
                        break;

                    case "todo":
                    case "todotool":
                        await InteractiveTodoAsync(client);
                        break;

                    case "textutility":
                    case "textutilitytool":
                        await InteractiveTextUtilityAsync(client);
                        break;

                    default:
                        Console.WriteLine("  ⚠️ Unknown tool. Try: weather, calculator, todo, textutility, or 'exit'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ Tool error: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("🔁 Choose the next tool (weather, calculator, todo, textutility) or 'exit' to quit.");
        }
    }

    private static async Task InteractiveWeatherAsync(IMcpClient client)
    {
        Console.WriteLine("🌤️  Weather Tool");
        Console.Write("  Location: ");
        var location = (Console.ReadLine() ?? "Tokyo").Trim();
        if (string.IsNullOrWhiteSpace(location)) location = "Tokyo";

        Console.Write("  Unit (celsius/fahrenheit) [celsius]: ");
        var unit = (Console.ReadLine() ?? "celsius").Trim().ToLowerInvariant();
        if (unit != "celsius" && unit != "fahrenheit") unit = "celsius";

        var result = await client.CallToolAsync("GetWeather", new Dictionary<string, object?>
        {
            ["location"] = location,
            ["unit"] = unit
        });

        Console.WriteLine($"  Weather for {location} ({unit}):");
        PrintToolResult(result);
    }

    private static async Task InteractiveCalculatorAsync(IMcpClient client)
    {
        Console.WriteLine("🔢 Calculator Tool");
        Console.WriteLine("  Operations: add, subtract, multiply, divide, power, squareroot, percentage, modulo");
        Console.Write("  Operation: ");
        var op = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

        static double ReadDouble(string prompt, double def)
        {
            Console.Write($"  {prompt} [{def}]: ");
            var s = Console.ReadLine();
            return double.TryParse(s, out var v) ? v : def;
        }

        Dictionary<string, object?> args;
        string toolName;

        switch (op)
        {
            case "add":
                toolName = "Add";
                args = new Dictionary<string, object?>
                {
                    ["a"] = ReadDouble("a", 10),
                    ["b"] = ReadDouble("b", 5)
                };
                break;

            case "subtract":
                toolName = "Subtract";
                args = new Dictionary<string, object?>
                {
                    ["a"] = ReadDouble("a", 10),
                    ["b"] = ReadDouble("b", 5)
                };
                break;

            case "multiply":
                toolName = "Multiply";
                args = new Dictionary<string, object?>
                {
                    ["a"] = ReadDouble("a", 10),
                    ["b"] = ReadDouble("b", 5)
                };
                break;

            case "divide":
                toolName = "Divide";
                args = new Dictionary<string, object?>
                {
                    ["a"] = ReadDouble("a", 10),
                    ["b"] = ReadDouble("b", 2)
                };
                break;

            case "power":
                toolName = "Power";
                args = new Dictionary<string, object?>
                {
                    ["base"] = ReadDouble("base", 2),
                    ["exponent"] = ReadDouble("exponent", 8)
                };
                break;

            case "squareroot":
            case "sqrt":
                toolName = "SquareRoot";
                args = new Dictionary<string, object?>
                {
                    ["number"] = ReadDouble("number", 144)
                };
                break;

            case "percentage":
                toolName = "Percentage";
                args = new Dictionary<string, object?>
                {
                    ["value"] = ReadDouble("value", 75),
                    ["percent"] = ReadDouble("percent", 10)
                };
                break;

            case "modulo":
                toolName = "Modulo";
                args = new Dictionary<string, object?>
                {
                    ["a"] = ReadDouble("a", 10),
                    ["b"] = ReadDouble("b", 3)
                };
                break;

            default:
                Console.WriteLine("  ⚠️ Unknown operation.");
                return;
        }

        var result = await client.CallToolAsync(toolName, args);
        Console.WriteLine($"  Result ({toolName}):");
        PrintToolResult(result);
    }

    private static async Task InteractiveTodoAsync(IMcpClient client)
    {
        Console.WriteLine("📝 Todo Tool");
        Console.WriteLine("  Actions: create, list, stats, complete, update, delete, clearcompleted");
        Console.Write("  Action: ");
        var action = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

        Dictionary<string, object?> args;
        string toolName;

        switch (action)
        {
            case "create":
                toolName = "CreateTodo";
                Console.Write("  Title: ");
                var title = (Console.ReadLine() ?? "New Task").Trim();
                Console.Write("  Description: ");
                var desc = (Console.ReadLine() ?? string.Empty).Trim();
                Console.Write("  Priority (Low/Medium/High) [Medium]: ");
                var prio = (Console.ReadLine() ?? "Medium").Trim();
                args = new Dictionary<string, object?>
                {
                    ["title"] = string.IsNullOrWhiteSpace(title) ? "New Task" : title,
                    ["description"] = desc,
                    ["priority"] = string.IsNullOrWhiteSpace(prio) ? "Medium" : prio
                };
                break;

            case "list":
                toolName = "GetTodos";
                Console.Write("  Status filter (All/Pending/Completed) [All]: ");
                var status = (Console.ReadLine() ?? "All").Trim();
                args = new Dictionary<string, object?>
                {
                    ["status"] = string.IsNullOrWhiteSpace(status) ? "All" : status
                };
                break;

            case "stats":
                toolName = "GetTodoStats";
                args = new Dictionary<string, object?>();
                break;

            case "complete":
                toolName = "CompleteTodo";
                Console.Write("  Todo Id: ");
                var completeId = (Console.ReadLine() ?? string.Empty).Trim();
                args = new Dictionary<string, object?>
                {
                    ["id"] = completeId
                };
                break;

            case "update":
                toolName = "UpdateTodo";
                Console.Write("  Todo Id: ");
                var updateId = (Console.ReadLine() ?? string.Empty).Trim();
                Console.Write("  New Title (optional): ");
                var newTitle = (Console.ReadLine() ?? string.Empty).Trim();
                Console.Write("  New Description (optional): ");
                var newDesc = (Console.ReadLine() ?? string.Empty).Trim();
                Console.Write("  New Priority (Low/Medium/High) (optional): ");
                var newPrio = (Console.ReadLine() ?? string.Empty).Trim();
                args = new Dictionary<string, object?>
                {
                    ["id"] = updateId,
                    ["title"] = string.IsNullOrWhiteSpace(newTitle) ? null : newTitle,
                    ["description"] = string.IsNullOrWhiteSpace(newDesc) ? null : newDesc,
                    ["priority"] = string.IsNullOrWhiteSpace(newPrio) ? null : newPrio
                };
                break;

            case "delete":
                toolName = "DeleteTodo";
                Console.Write("  Todo Id: ");
                var deleteId = (Console.ReadLine() ?? string.Empty).Trim();
                args = new Dictionary<string, object?>
                {
                    ["id"] = deleteId
                };
                break;

            case "clearcompleted":
                toolName = "ClearCompleted";
                args = new Dictionary<string, object?>();
                break;

            default:
                Console.WriteLine("  ⚠️ Unknown action.");
                return;
        }

        var result = await client.CallToolAsync(toolName, args);
        Console.WriteLine($"  Todo Response ({toolName}):");
        PrintToolResult(result);
    }

    private static async Task InteractiveTextUtilityAsync(IMcpClient client)
    {
        Console.WriteLine("📄 Text Utility Tool");
        Console.WriteLine("  Actions: analyze, convertcase, reverse, deduplicate, extractemails, extracturls, slugify, truncate");
        Console.Write("  Action: ");
        var action = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

        Dictionary<string, object?> args;
        string toolName;

        string ReadText(string prompt, string def)
        {
            Console.Write($"  {prompt} ");
            var t = Console.ReadLine();
            t = string.IsNullOrWhiteSpace(t) ? def : t!;
            return t;
        }

        switch (action)
        {
            case "analyze":
                toolName = "AnalyzeText";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "Hello World! This is sample text.")
                };
                break;

            case "convertcase":
                toolName = "ConvertCase";
                Console.Write("  Mode (upper/lower/title/sentence/toggle) [upper]: ");
                var mode = (Console.ReadLine() ?? "upper").Trim().ToLowerInvariant();
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "Convert Me"),
                    ["mode"] = string.IsNullOrWhiteSpace(mode) ? "upper" : mode
                };
                break;

            case "reverse":
                toolName = "ReverseText";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "Reverse this")
                };
                break;

            case "deduplicate":
                toolName = "RemoveDuplicateLines";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text (multi-line):", "a\na\nb\nc\nc")
                };
                break;

            case "extractemails":
                toolName = "ExtractEmails";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "Contact us at info@example.com and support@example.org")
                };
                break;

            case "extracturls":
                toolName = "ExtractUrls";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "Visit https://example.com and http://example.org")
                };
                break;

            case "slugify":
                toolName = "Slugify";
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "My Awesome Blog Post Title!")
                };
                break;

            case "truncate":
                toolName = "Truncate";
                Console.Write("  Max length [20]: ");
                var sLen = Console.ReadLine();
                var maxLen = int.TryParse(sLen, out var l) ? l : 20;
                args = new Dictionary<string, object?>
                {
                    ["text"] = ReadText("Text:", "This is a long text that will be truncated"),
                    ["maxLength"] = maxLen
                };
                break;

            default:
                Console.WriteLine("  ⚠️ Unknown action.");
                return;
        }

        var result = await client.CallToolAsync(toolName, args);
        Console.WriteLine($"  Text Utility Response ({toolName}):");
        PrintToolResult(result);
    }

    private static void PrintToolResult(CallToolResponse result)
    {
        foreach (var content in result.Content)
        {
            if (content.Type == "text" && content.Text != null)
            {
                Console.WriteLine($"  {content.Text}");
            }
        }
    }

    private static void ShowDemoWithoutServer()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║               Demo Mode (No Server Connection)               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This demo shows the available MCP tools and their capabilities:");
        Console.WriteLine();

        Console.WriteLine("🌤️  WEATHER TOOL");
        Console.WriteLine("   • GetWeather - Get current weather for a location");
        Console.WriteLine("   • GetForecast - Get weather forecast for multiple days");
        Console.WriteLine();

        Console.WriteLine("🔢 CALCULATOR TOOL");
        Console.WriteLine("   • Add, Subtract, Multiply, Divide");
        Console.WriteLine("   • Power, SquareRoot");
        Console.WriteLine("   • Percentage, Modulo");
        Console.WriteLine();

        Console.WriteLine("📝 TODO TOOL");
        Console.WriteLine("   • CreateTodo - Create a new task");
        Console.WriteLine("   • GetTodos - List all tasks with filters");
        Console.WriteLine("   • CompleteTodo - Mark a task as done");
        Console.WriteLine("   • UpdateTodo - Modify existing tasks");
        Console.WriteLine("   • DeleteTodo - Remove a task");
        Console.WriteLine("   • GetTodoStats - View task statistics");
        Console.WriteLine("   • ClearCompleted - Remove finished tasks");
        Console.WriteLine();

        Console.WriteLine("📄 TEXT UTILITY TOOL");
        Console.WriteLine("   • AnalyzeText - Word/character/sentence count");
        Console.WriteLine("   • ConvertCase - upper/lower/title/sentence/toggle");
        Console.WriteLine("   • ReverseText - Reverse characters");
        Console.WriteLine("   • RemoveDuplicateLines - Deduplicate text");
        Console.WriteLine("   • ExtractEmails - Find email addresses");
        Console.WriteLine("   • ExtractUrls - Find URLs");
        Console.WriteLine("   • Slugify - Create URL-friendly strings");
        Console.WriteLine("   • Truncate - Shorten text with ellipsis");
        Console.WriteLine();

        Console.WriteLine("To run the full demo with a live server:");
        Console.WriteLine("  1. Build the solution: dotnet build");
        Console.WriteLine("  2. Run: dotnet run --project src/MCP.Host");
        Console.WriteLine();
    }
}
