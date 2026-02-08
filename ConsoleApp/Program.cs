using ProcedureCore.Core;
using ProcedureCore.LangRenSha;
using Microsoft.AspNetCore.SignalR.Client;

var hubUrl = args.Length > 0 ? args[0] : "http://localhost:5269/gamehub";

var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect()
    .Build();

bool gameRunning = true;
int registeredPlayerId = 0;

connection.On<int, int>("Registered", (gameId, playerId) =>
{
    Console.WriteLine($"Registered in Game {gameId} as player {playerId}");
    registeredPlayerId = playerId;
});

connection.On<string>("GameStateUpdate", (stateDiff) =>
{
    Console.WriteLine($"Game state updated:" + stateDiff);
});

Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("Shutting down...");
};

await connection.StartAsync();
Console.WriteLine("Connected to server.");

Console.WriteLine("Enter game id:");
var line = Console.ReadLine();
if (line == null || !int.TryParse(line.Trim(), out int gameId))
{
    Console.WriteLine("Invalid game id, exiting.");
    return 1;
}

Console.WriteLine("Enter player id:");
line = Console.ReadLine();
if (line == null || !int.TryParse(line.Trim(), out int playerId))
{
    Console.WriteLine("Invalid player id, exiting.");
    return 1;
}

await connection.InvokeAsync("Register", gameId, playerId);
Console.WriteLine("Starting game...");
await connection.InvokeAsync("StartGame", gameId);
Console.WriteLine("Game started.");

while (gameRunning)
{
    Console.WriteLine("Enter targets (comma separated):");
    line = Console.ReadLine();
    if (line == null)
    {
        Console.WriteLine("Input error.");
        continue;
    }

    var targets = new List<int>();
    foreach (var part in line.Split(','))
    {
        if (int.TryParse(part.Trim(), out int target))
        {
            targets.Add(target);
        }
        else
        {
            Console.WriteLine($"Invalid target: {part}, skipping.");
        }
    }
    Console.WriteLine(
        $"Submitting targets: {string.Join(", ", targets)}");
    await connection.InvokeAsync("UserAction", gameId, registeredPlayerId, targets);
}

await connection.StopAsync();
return 0;

// Local proxy of the server DTO (keeps client decoupled from server implementation file)
internal static class GameHubProxy
{
    public class TargetsDto
    {
        public bool DoInput { get; set; }
        public List<int> Targets { get; set; } = new();
        public int TargetsCount { get; set; }
    }
}

