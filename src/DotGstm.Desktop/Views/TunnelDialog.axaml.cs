using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.ViewModels;
using DotGstm.Desktop.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DotGstm.Desktop.Views;

public partial class TunnelDialog : Window
{
    private TunnelDialogViewModel ViewModel => (TunnelDialogViewModel)DataContext!;

    public TunnelDialog()
    {
        InitializeComponent();
        DataContext = new TunnelDialogViewModel();

        // 訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += OnWindowClosed;

        // 初始化 UI 文字
        UpdateUIText();
    }

    public TunnelDialog(PortRedirection existing)
    {
        InitializeComponent();
        DataContext = new TunnelDialogViewModel(existing);

        // 訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += OnWindowClosed;

        // 初始化 UI 文字
        UpdateUIText();
    }

    /// <summary>
    /// 語言變更事件處理器
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Console.WriteLine("[TunnelDialog] 語言已變更，更新 UI 文字");
        UpdateUIText();
    }

    /// <summary>
    /// 視窗關閉事件處理器
    /// </summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // 取消訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// 更新 UI 文字
    /// </summary>
    private void UpdateUIText()
    {
        var loc = LocalizationService.Instance;

        // 更新視窗標題
        Title = loc.GetString("Tunnel_Title");

        // 更新標籤文字
        TxtTypeLabel.Text = loc.GetString("Tunnel_Type");
        TxtBindPortLabel.Text = loc.GetString("Tunnel_BindPort");
        TxtToHostLabel.Text = loc.GetString("Tunnel_ToHost");
        TxtToPortLabel.Text = loc.GetString("Tunnel_ToPort");

        // 更新按鈕文字
        TxtCancelLabel.Text = loc.GetString("Btn_Cancel");
        TxtOkLabel.Text = loc.GetString("Btn_OK");
    }

    private async void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[TunnelDialog] 點擊 OK");
        Console.WriteLine($"[TunnelDialog] 資料: Type={ViewModel.SelectedType}, BindPort={ViewModel.BindPort}, ToHost={ViewModel.ToHost}, ToPort={ViewModel.ToPort}");

        // 驗證輸入（遵循原始 gSTM 邏輯）
        var type = ViewModel.SelectedType.ToLower();

        var loc = LocalizationService.Instance;

        // 1. 檢查 Port（bind_address:port）
        if (string.IsNullOrWhiteSpace(ViewModel.BindPort))
        {
            Console.WriteLine("[TunnelDialog] 驗證失敗：Port 是空的");
            var box = MessageBoxManager.GetMessageBoxStandard(
                loc.GetString("Error_Title"),
                loc.GetString("Tunnel_ErrorPortEmpty"));
            await box.ShowAsync();
            return;
        }

        // 2. 如果不是 dynamic，檢查 Host 和 Port2
        if (type != "dynamic")
        {
            if (string.IsNullOrWhiteSpace(ViewModel.ToHost))
            {
                Console.WriteLine("[TunnelDialog] 驗證失敗：To Host 是空的");
                var box = MessageBoxManager.GetMessageBoxStandard(
                    loc.GetString("Error_Title"),
                    loc.GetString("Tunnel_ErrorHostEmpty"));
                await box.ShowAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.ToPort))
            {
                Console.WriteLine("[TunnelDialog] 驗證失敗：To Port 是空的");
                var box = MessageBoxManager.GetMessageBoxStandard(
                    loc.GetString("Error_Title"),
                    loc.GetString("Tunnel_ErrorPort2Empty"));
                await box.ShowAsync();
                return;
            }
        }

        // 手動構建 Result（跟 ViewModel.OnOk() 的邏輯一樣）
        var result = new PortRedirection
        {
            Type = type,
            Port1 = ViewModel.BindPort,
            Host = ViewModel.ToHost,
            Port2 = ViewModel.ToPort
        };

        Console.WriteLine($"[TunnelDialog] 驗證通過，已建立 Result: Type={result.Type}, Port1={result.Port1}, Host={result.Host}, Port2={result.Port2}");
        Close(result);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[TunnelDialog] 點擊 Cancel");
        Close(null);
    }
}
