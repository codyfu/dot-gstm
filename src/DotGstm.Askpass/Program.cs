using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DotGstm.Askpass;
using System;

namespace DotGstm.Askpass;

class Program
{
    /// <summary>
    /// 主程式進入點（對應 gAskpass main.c:17）
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // 取得提示訊息（對應 gAskpass main.c:29-32）
        string prompt = args.Length > 0
            ? args[0]
            : "Please enter your password:";

        BuildAvaloniaApp()
            .AfterSetup(builder =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var window = new AskpassWindow();
                    window.SetPrompt(prompt);
                    desktop.MainWindow = window;
                }
            })
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// 建立 Avalonia 應用程式
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

/// <summary>
/// Avalonia 應用程式類別
/// </summary>
public class App : Application
{
    public override void Initialize()
    {
        // 使用 Fluent 主題
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }
}
