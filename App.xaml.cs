using System.Runtime.InteropServices;
using System.Windows;
using LibVLCSharp.Shared;

namespace NovaPlayer;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            SetCurrentProcessExplicitAppUserModelID("NovaPlayer.App");
        }
        catch { }
        Core.Initialize();
    }
}
