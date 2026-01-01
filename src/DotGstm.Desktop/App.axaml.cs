using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotGstm.Desktop.Views;

namespace DotGstm.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 主視窗
            desktop.MainWindow = new MainWindow();

            // 測試用：
            // desktop.MainWindow = new TunnelDialog();
            // desktop.MainWindow = new PropertiesDialog();
        }

        base.OnFrameworkInitializationCompleted();
    }
}