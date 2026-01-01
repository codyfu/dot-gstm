using Avalonia;
using System;
using System.Linq;
using DotGstm.Desktop.Utils;

namespace DotGstm.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Parse --debug argument
        // 解析 --debug 參數
        if (args.Contains("--debug"))
        {
            DebugLogger.Enabled = true;
            DebugLogger.Log("[Program] Debug mode enabled");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
