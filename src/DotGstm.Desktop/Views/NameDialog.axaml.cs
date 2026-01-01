using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using DotGstm.Desktop.Services;

namespace DotGstm.Desktop.Views;

/// <summary>
/// 詢問新隧道名稱對話框（遵循 gSTM fniface.c:gstm_interface_asknewname）
/// </summary>
public partial class NameDialog : Window
{
    private readonly HashSet<string> _existingNames;
    public string? TunnelName { get; private set; }

    // Avalonia XAML 編譯器需要的無參數建構子
    public NameDialog() : this(Enumerable.Empty<string>())
    {
    }

    public NameDialog(IEnumerable<string> existingNames)
    {
        InitializeComponent();
        _existingNames = new HashSet<string>(existingNames);

        // 對話框顯示時聚焦到文字框
        Opened += (s, e) => NameTextBox.Focus();

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
        Title = loc.GetString("Dialog_NameTitle");

        // 更新提示文字
        if (Content is Grid grid && grid.Children.Count > 0 && grid.Children[0] is StackPanel sp &&
            sp.Children.Count > 1 && sp.Children[1] is TextBlock promptText)
            promptText.Text = loc.GetString("Dialog_NamePrompt");

        // 更新文字框提示
        NameTextBox.Watermark = loc.GetString("Dialog_NameWatermark");

        // 更新按鈕文字
        TxtCancelLabel.Text = loc.GetString("Btn_Cancel");
        TxtOkLabel.Text = loc.GetString("Btn_OK");

        Console.WriteLine($"[NameDialog] UI 文字已更新為 {loc.CurrentLanguage}");
    }

    private async void BtnOK_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        var loc = LocalizationService.Instance;

        // 驗證 1：名稱不能為空（遵循 fniface.c:537-542）
        if (string.IsNullOrWhiteSpace(name))
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                loc.GetString("Error_Title"),
                loc.GetString("Dialog_ErrorEmptyName"),
                ButtonEnum.Ok);
            await msgBox.ShowWindowDialogAsync(this);
            NameTextBox.Focus();
            return;
        }

        // 驗證 2：名稱不能重複（遵循 fniface.c:545-559）
        if (_existingNames.Contains(name))
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                loc.GetString("Error_Title"),
                loc.GetString("Dialog_ErrorDuplicateName"),
                ButtonEnum.Ok);
            await msgBox.ShowWindowDialogAsync(this);
            NameTextBox.SelectAll();
            NameTextBox.Focus();
            return;
        }

        // 驗證通過，設定結果並關閉
        TunnelName = name;
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        TunnelName = null;
        Close(false);
    }
}
