using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace NovaPlayer;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NovaPlayer",
        "crash_log.txt"
    );

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Setup global unhandled exception trapping before any other code
        SetupExceptionHandling();

        // 2. Set Windows AppUserModelID for proper taskbar grouping
        try
        {
            SetCurrentProcessExplicitAppUserModelID("NovaPlayer.App");
        }
        catch { }

        // 3. Initialize native LibVLC runtime
        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            LogCrash("Core.Initialize", ex);
        }

        base.OnStartup(e);
    }

    private void SetupExceptionHandling()
    {
        // WPF UI thread unhandled exceptions
        this.DispatcherUnhandledException += (sender, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            // Mark handled to avoid abruptly killing the application on recoverable UI issues
            args.Handled = true;
        };

        // Non-UI and native interop unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogCrash("AppDomain.UnhandledException", ex);
            }
            else
            {
                LogCrashRaw("AppDomain.UnhandledException", args.ExceptionObject?.ToString() ?? "Unknown exception");
            }
        };

        // Background task unobserved exceptions
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            // Mark observed so the CLR doesn't terminate the process
            args.SetObserved();
        };
    }

    public static void LogCrash(string source, Exception ex)
    {
        try
        {
            string? dir = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}" +
                             $"{ex.StackTrace}{Environment.NewLine}";

            if (ex.InnerException != null)
            {
                logEntry += $"  Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}{Environment.NewLine}" +
                            $"  {ex.InnerException.StackTrace}{Environment.NewLine}";
            }
            logEntry += new string('-', 80) + Environment.NewLine;

            File.AppendAllText(CrashLogPath, logEntry);
        }
        catch { }
    }

    public static void LogCrashRaw(string source, string message)
    {
        try
        {
            string? dir = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}{Environment.NewLine}" +
                             new string('-', 80) + Environment.NewLine;

            File.AppendAllText(CrashLogPath, logEntry);
        }
        catch { }
    }
}
