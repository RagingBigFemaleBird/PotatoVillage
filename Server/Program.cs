using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server;
using ProcedureCore.LangRenSha;

// Set up global exception handler to log crashes to file
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var exception = args.ExceptionObject as Exception;
    LogCrash(exception, "UnhandledException");
};

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    LogCrash(args.Exception, "UnobservedTaskException");
    args.SetObserved();
};

static void LogCrash(Exception? exception, string source)
{
    try
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLogs");
        Directory.CreateDirectory(logDir);

        var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var logContent = $"""
            ========================================
            Crash Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            Source: {source}
            ========================================

            Exception Type: {exception?.GetType().FullName}
            Message: {exception?.Message}

            Stack Trace:
            {exception?.StackTrace}

            Inner Exception:
            {exception?.InnerException?.Message}
            {exception?.InnerException?.StackTrace}
            ========================================
            """;

        File.WriteAllText(logFile, logContent);
        Console.WriteLine($"Crash logged to: {logFile}");
    }
    catch
    {
        // Ignore logging errors
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all interfaces for Azure
var port = Environment.GetEnvironmentVariable("PORT") ?? Environment.GetEnvironmentVariable("WEBSITES_PORT") ?? "8080";
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Add CORS for SignalR - must allow credentials for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow any origin
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

// Add UDP discovery service for local network discovery (only in development)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<Server.DiscoveryService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// CORS must be before routing and endpoints
app.UseCors();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
