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

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapHub<GameHub>("/gamehub");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
