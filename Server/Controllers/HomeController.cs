using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Server.Models;

namespace Server.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private static IHubContext<GameHub>? _hubContext;

    public HomeController(ILogger<HomeController> logger, IHubContext<GameHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Initialize the hub context at startup so it's available for game callbacks
    /// </summary>
    public static void InitializeHubContext(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public static IHubContext<GameHub> GetGameHubContext()
    {
        if (_hubContext == null)
        {
            throw new InvalidOperationException("HubContext not initialized. Make sure InitializeHubContext is called at startup.");
        }
        return _hubContext;
    }

    public IActionResult Index()
    {
        ViewBag.TotalGames = GameHub.GetTotalGamesCount();
        ViewBag.ActiveGames = GameHub.GetActiveGamesCount();
        ViewBag.WaitingGames = GameHub.GetWaitingGamesCount();
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
