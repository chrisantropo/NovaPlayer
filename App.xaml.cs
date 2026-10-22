using System.Windows;
using LibVLCSharp.Shared;

namespace NovaPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Core.Initialize();
    }
}
