using System.Text;

namespace PotatoVillage.Services
{
    /// <summary>
    /// Writes unhandled-exception details to files under AppData/CrashLogs so
    /// that "random" crashes in the field leave an actionable stack trace.
    /// The app currently has no client-side crash capture at all - a WinUI
    /// stowed exception (e.g. the 0xc000027b / E_OUTOFMEMORY crashes seen in
    /// Windows Error Reporting) terminates the process silently.
    /// </summary>
    public static class CrashLogService
    {
        private static bool initialized;

        public static string LogDirectory =>
            Path.Combine(FileSystem.AppDataDirectory, "CrashLogs");

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Write("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            // Diagnostic tripwire for the 0xc000027b/E_OUTOFMEMORY crashes:
            // XAML-internal stowed exceptions never reach the handlers above,
            // but the originating managed exception passes through here at
            // throw time with a usable stack. Rate-limited so an exception
            // storm can't grind the app to a halt writing files.
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
                var ex = e.Exception;
                if (ex is OutOfMemoryException || ex.HResult == E_OUTOFMEMORY)
                {
                    var now = Environment.TickCount64;
                    if (now - lastFirstChanceLog > 2000)
                    {
                        lastFirstChanceLog = now;
                        Write("FirstChance(E_OUTOFMEMORY)", ex);
                    }
                }
            };
        }

        private static long lastFirstChanceLog = -10000;

        public static void Write(string source, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                // Keep only the newest 20 logs.
                var old = Directory.GetFiles(LogDirectory, "crash_*.log")
                    .OrderByDescending(f => f)
                    .Skip(19);
                foreach (var f in old)
                {
                    try { File.Delete(f); } catch { }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Time (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine($"App version: {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})");
                sb.AppendLine($"Platform: {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
                sb.AppendLine();

                var cur = ex;
                int depth = 0;
                while (cur != null && depth < 10)
                {
                    sb.AppendLine($"[{depth}] {cur.GetType().FullName}: {cur.Message}");
                    sb.AppendLine(cur.StackTrace ?? "(no stack trace)");
                    sb.AppendLine();
                    cur = cur.InnerException;
                    depth++;
                }
                if (ex == null)
                {
                    sb.AppendLine("(no exception object)");
                }

                var file = Path.Combine(LogDirectory, $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.log");
                File.WriteAllText(file, sb.ToString());
            }
            catch
            {
                // Never let crash logging itself crash anything.
            }
        }
    }
}
