using Avalonia;
using System;
using System.IO;

namespace OpenSourceToolkit.NET
{
    public sealed class EntryPoint
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize debug file logging if /log argument is present (DEBUG builds only)
            Services.DebugLogger.Initialize(args);
            
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                // Last resort exception handler - log to file before crashing
                LogFatalException(ex);
                throw; // Re-throw to allow normal crash behavior (error reporting, etc.)
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseWin32()
                .UseSkia()
                .UseHarfBuzz()
                .WithInterFont()
                .LogToTrace();

        private static void LogFatalException(Exception ex)
        {
            try
            {
                var msg = $"[FATAL] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                if (ex.InnerException != null)
                    msg += $"\n\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";

                System.Diagnostics.Debug.WriteLine(msg);
                Console.WriteLine(msg);

                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenSourceToolkit",
                    "crash.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                var logEntry = $"\n\n=== FATAL {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n{msg}";
                File.AppendAllText(logPath, logEntry);
                
                // Also log to DebugLogger if it's active
                if (Services.DebugLogger.IsEnabled)
                    Services.DebugLogger.Log("FATAL", msg);
            }
            catch
            {
                // If we can't log, there's nothing more we can do
            }
        }
    }
}
