using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;
using DotGstm.Desktop.Services;

namespace DotGstm.Desktop.Views;

/// <summary>
/// About 對話框（遵循 gSTM GtkAboutDialog）
/// 實作 Credits 從下方滑出的動畫效果
/// </summary>
public partial class AboutDialog : Window
{
    private TranslateTransform? _creditsTransform;

    public AboutDialog()
    {
        InitializeComponent();

        // 建立 TranslateTransform（初始位置會在顯示時設定）
        _creditsTransform = new TranslateTransform(0, 0);
        CreditsPanel.RenderTransform = _creditsTransform;

        // 訂閱語言切換事件
        LocalizationService.Instance.LanguageChanged += (s, e) => UpdateUIText();

        // 初始化 UI 文字
        UpdateUIText();
    }

    /// <summary>
    /// 更新所有 UI 文字
    /// </summary>
    private void UpdateUIText()
    {
        var loc = LocalizationService.Instance;

        // 更新視窗標題
        Title = loc.GetString("About_Title");

        // 更新按鈕
        BtnCredits.Content = loc.GetString("About_Credits");
        BtnClose.Content = loc.GetString("About_Close");

        // 更新主頁面文字（使用命名控制項）
        TxtDescription.Text = loc.GetString("About_Description");
        TxtWebsite.Text = loc.GetString("About_Website");
        TxtLicense.Text = loc.GetString("About_License");

        // 更新 Credits 面板
        TxtDotNetPort.Text = loc.GetString("About_DotNetPort");

        // 更新 TxtDotNetImpl 的 3 個 Run
        RunPrefix.Text = loc.GetString("About_DotNetImplPrefix");
        RunGstmLink.Text = loc.GetString("About_DotNetImplLink");
        RunSuffix.Text = loc.GetString("About_DotNetImplSuffix");

        TxtDotNetAuthor.Text = loc.GetString("About_DotNetAuthor");

        Console.WriteLine($"[AboutDialog] UI 文字已更新為 {loc.CurrentLanguage}");
    }

    /// <summary>
    /// 銘謝按鈕：滑出/收回 Credits 面板（使用 ToggleButton）
    /// </summary>
    private void BtnCredits_Click(object? sender, RoutedEventArgs e)
    {
        if (_creditsTransform == null) return;

        // 使用 ToggleButton 的 IsChecked 狀態
        bool showingCredits = BtnCredits.IsChecked == true;

        if (showingCredits)
        {
            // 顯示 Credits：先顯示面板，等待佈局更新後再執行滑動動畫
            CreditsPanel.IsVisible = true;

            // 等待佈局更新後再執行動畫
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // 設置初始位置在下方
                _creditsTransform.Y = CreditsPanel.Bounds.Height;

                // 使用 Avalonia Transitions 動畫化 Y 屬性（600ms 慢速滑動）
                var transition = new Avalonia.Animation.DoubleTransition
                {
                    Property = TranslateTransform.YProperty,
                    Duration = TimeSpan.FromMilliseconds(600),
                    Easing = new CubicEaseOut()
                };

                _creditsTransform.Transitions = new Avalonia.Animation.Transitions { transition };
                _creditsTransform.Y = 0;
            }, Avalonia.Threading.DispatcherPriority.Render);
        }
        else
        {
            // 隱藏 Credits：滑下去然後隱藏面板（600ms 慢速滑動）
            var transition = new Avalonia.Animation.DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(600),
                Easing = new CubicEaseOut()
            };

            _creditsTransform.Transitions = new Avalonia.Animation.Transitions { transition };
            _creditsTransform.Y = CreditsPanel.Bounds.Height;

            // 動畫完成後隱藏面板
            Task.Delay(600).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CreditsPanel.IsVisible = false;
                });
            });
        }
    }

    /// <summary>
    /// 關閉按鈕
    /// </summary>
    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        // 關閉前，如果 Credits 面板正在顯示，先隱藏它
        if (BtnCredits.IsChecked == true)
        {
            BtnCredits.IsChecked = false;
            CreditsPanel.IsVisible = false;
        }

        Close();
    }

    /// <summary>
    /// Website 連結點擊事件
    /// </summary>
    private void TxtWebsite_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        try
        {
            // 開啟專案網站
            var url = "https://gitlab.mypc.tw/ai-gemini-claude/dot-gstm";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AboutDialog] 無法開啟網站: {ex.Message}");
        }
    }

    /// <summary>
    /// gSTM Gtk3 Edition 連結點擊事件
    /// </summary>
    private void RunGstmLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        try
        {
            // 開啟原始 gSTM 專案頁面
            var url = "https://github.com/dallenwilson/gstm";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AboutDialog] 無法開啟 gSTM 連結: {ex.Message}");
        }
    }
}
