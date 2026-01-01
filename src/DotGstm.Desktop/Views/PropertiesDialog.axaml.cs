using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.Services;
using DotGstm.Desktop.ViewModels;

namespace DotGstm.Desktop.Views;

public partial class PropertiesDialog : Window
{
    private PropertiesDialogViewModel ViewModel => (PropertiesDialogViewModel)DataContext!;
    private string? _existingFileName; // 保存原始檔名，用於編輯模式

    public PropertiesDialog()
    {
        InitializeComponent();
        DataContext = new PropertiesDialogViewModel();
        SetupViewModel();

        // 訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        // 初始化 UI 文字
        UpdateUIText();
    }

    public PropertiesDialog(SshTunnel existing)
    {
        InitializeComponent();
        DataContext = new PropertiesDialogViewModel(existing);
        _existingFileName = existing.FileName; // 保存原始檔名
        SetupViewModel();

        // 訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        // 初始化 UI 文字
        UpdateUIText();
    }

    /// <summary>
    /// 設定 ViewModel 的委託處理器和事件
    /// </summary>
    private void SetupViewModel()
    {
        // 設定檔案選擇器處理器
        ViewModel.OpenFilePickerHandler = async () => await OpenPrivateKeyFilePicker();

        // 設定 TunnelDialog 處理器
        ViewModel.OpenTunnelDialogHandler = async (existing) => await OpenTunnelDialog(existing);

        // 訂閱視窗關閉事件以清理資源
        Closed += (s, e) => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// 語言變更事件處理器
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateUIText();
    }

    /// <summary>
    /// 更新所有 UI 文字為當前語言
    /// </summary>
    private void UpdateUIText()
    {
        var loc = LocalizationService.Instance;

        // 視窗標題
        Title = loc.GetString("Properties_Title");

        // 左欄標籤
        var lblName = this.FindControl<TextBlock>("LblName");
        if (lblName == null)
        {
            // 如果沒有命名，直接查找 Grid 中的 TextBlock
            var nameGrid = TxtName.Parent as Grid;
            if (nameGrid != null)
            {
                var nameLabel = nameGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (nameLabel != null) nameLabel.Text = loc.GetString("Properties_Name");
            }
        }

        var lblPreset = this.FindControl<TextBlock>("LblPreset");
        if (lblPreset == null)
        {
            var presetGrid = CmbPreset.Parent as Grid;
            if (presetGrid != null)
            {
                var presetLabel = presetGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (presetLabel != null) presetLabel.Text = loc.GetString("Properties_Preset");
            }
        }

        // Preset 說明文字
        var presetHintBlock = this.Find<TextBlock>("PresetHint");
        if (presetHintBlock == null)
        {
            // 在左欄 StackPanel 中查找 FontSize=10 的 TextBlock
            var leftPanel = TxtName.Parent?.Parent as StackPanel;
            if (leftPanel != null)
            {
                var hintBlock = leftPanel.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.FontSize == 10);
                if (hintBlock != null) hintBlock.Text = loc.GetString("Properties_PresetHint");
            }
        }

        // 中欄標籤
        var lblUser = this.FindControl<TextBlock>("LblUser");
        if (lblUser == null)
        {
            var userGrid = TxtUser.Parent as Grid;
            if (userGrid != null)
            {
                var userLabel = userGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (userLabel != null) userLabel.Text = loc.GetString("Properties_User");
            }
        }

        var lblHost = this.FindControl<TextBlock>("LblHost");
        if (lblHost == null)
        {
            var hostGrid = TxtHost.Parent as Grid;
            if (hostGrid != null)
            {
                var hostLabel = hostGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (hostLabel != null) hostLabel.Text = loc.GetString("Properties_Host");
            }
        }

        var lblPort = this.FindControl<TextBlock>("LblPort");
        if (lblPort == null)
        {
            var portGrid = TxtPort.Parent as Grid;
            if (portGrid != null)
            {
                var portLabel = portGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (portLabel != null) portLabel.Text = loc.GetString("Properties_Port");
            }
        }

        var lblPrivkey = this.FindControl<TextBlock>("LblPrivkey");
        if (lblPrivkey == null)
        {
            var privkeyGrid = TxtPrivkey.Parent as Grid;
            if (privkeyGrid != null)
            {
                var privkeyLabel = privkeyGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (privkeyLabel != null) privkeyLabel.Text = loc.GetString("Properties_Privkey");
            }
        }

        // Locate Private Key 按鈕
        BtnLocateKey.Content = loc.GetString("Properties_LocateKey");

        // 右欄 CheckBoxes
        ChkAutoStart.Content = loc.GetString("Properties_AutoStart");
        ChkRestart.Content = loc.GetString("Properties_AutoRestart");
        ChkNotify.Content = loc.GetString("Properties_Notify");

        // Max restarts 標籤
        var lblMaxRestarts = this.FindControl<TextBlock>("LblMaxRestarts");
        if (lblMaxRestarts == null)
        {
            var maxRestartsGrid = TxtMaxRestarts.Parent as Grid;
            if (maxRestartsGrid != null)
            {
                var maxRestartsLabel = maxRestartsGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (maxRestartsLabel != null) maxRestartsLabel.Text = loc.GetString("Properties_MaxRestarts");
            }
        }

        // Port Redirections 按鈕
        UpdateButtonText(BtnAdd, loc.GetString("Properties_BtnAdd"));
        UpdateButtonText(BtnEdit, loc.GetString("Properties_BtnEdit"));
        UpdateButtonText(BtnDelete, loc.GetString("Properties_BtnDelete"));

        // DataGrid 列標題
        if (RedirList.Columns.Count >= 4)
        {
            RedirList.Columns[0].Header = loc.GetString("Properties_ColType");
            RedirList.Columns[1].Header = loc.GetString("Properties_ColPort");
            RedirList.Columns[2].Header = loc.GetString("Properties_ColToHost");
            RedirList.Columns[3].Header = loc.GetString("Properties_ColToPort");
        }

        // 底部說明文字
        UpdateHelpText();

        // 底部按鈕
        UpdateButtonText(BtnCancel, loc.GetString("Btn_Cancel"));
        UpdateButtonText(BtnOk, loc.GetString("Btn_OK"));
    }

    /// <summary>
    /// 更新按鈕內的文字（按鈕包含 StackPanel 和多個 TextBlock）
    /// </summary>
    private void UpdateButtonText(Button button, string text)
    {
        if (button.Content is StackPanel sp)
        {
            // 找到第二個 TextBlock（第一個是圖標）
            var textBlocks = sp.Children.OfType<TextBlock>().ToList();
            if (textBlocks.Count >= 2)
            {
                textBlocks[1].Text = text;
            }
            else if (textBlocks.Count == 1)
            {
                textBlocks[0].Text = text;
            }
        }
    }

    /// <summary>
    /// 更新說明文字
    /// </summary>
    private void UpdateHelpText()
    {
        var loc = LocalizationService.Instance;

        // 找到說明文字的 TextBlock
        // 它在右下角的 StackPanel 中，FontSize=10.5
        var rightPanel = BtnAdd.Parent as StackPanel;
        if (rightPanel != null)
        {
            var helpTextBlock = rightPanel.Children.OfType<TextBlock>()
                .FirstOrDefault(tb => tb.FontSize == 10.5);
            if (helpTextBlock != null)
            {
                helpTextBlock.Text = loc.GetString("Properties_HelpText");
            }
        }
    }

    // === 按鈕事件 ===

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[PropertiesDialog] BtnOk_Click 開始");
        Console.WriteLine($"[PropertiesDialog] Name={ViewModel.Name}, Host={ViewModel.Host}");
        Console.WriteLine($"[PropertiesDialog] PortRedirections.Count={ViewModel.PortRedirections.Count}");
        Console.WriteLine($"[PropertiesDialog] _existingFileName={_existingFileName ?? "null (新增模式)"}");

        // 建立結果（手動構建 SshTunnel）
        var result = new SshTunnel
        {
            Name = ViewModel.Name,
            Host = ViewModel.Host,
            Port = ViewModel.Port,
            Login = ViewModel.User,
            PrivateKeyPath = ViewModel.PrivateKey,
            AutoStart = ViewModel.AutoStart,
            Restart = ViewModel.AutoRestart,
            Notify = ViewModel.Notify,
            MaxRestarts = ViewModel.MaxRestarts,
            Preset = ViewModel.IsPresetMode,
            FileName = _existingFileName // 保留原始檔名（編輯模式）或 null（新增模式）
        };

        Console.WriteLine($"[PropertiesDialog] result.FileName={result.FileName ?? "null"}");

        // 轉換端口轉發列表
        foreach (var vm in ViewModel.PortRedirections)
        {
            var redir = vm.ToModel();
            Console.WriteLine($"[PropertiesDialog] 轉發: Type={redir.Type}, Port1={redir.Port1}, Host={redir.Host}, Port2={redir.Port2}");
            result.PortRedirections.Add(redir);
        }

        Console.WriteLine($"[PropertiesDialog] 最終 PortRedirections.Count={result.PortRedirections.Count}");
        Console.WriteLine($"[PropertiesDialog] 完成，關閉對話框");
        Close(result);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void BtnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        // 防止重複開啟：禁用按鈕
        BtnLocateKey.IsEnabled = false;

        try
        {
            var path = await OpenPrivateKeyFilePicker();
            if (path != null)
            {
                ViewModel.PrivateKey = path;
            }
        }
        finally
        {
            // 恢復按鈕狀態
            BtnLocateKey.IsEnabled = true;
        }
    }

    private async void BtnAddRedir_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[PropertiesDialog] 點擊 Add Port Redirection");

        // 防止重複開啟對話框
        BtnAdd.IsEnabled = false;

        try
        {
            Console.WriteLine("[PropertiesDialog] 開啟 TunnelDialog");
            var result = await OpenTunnelDialog(null);

            Console.WriteLine($"[PropertiesDialog] TunnelDialog 關閉，result={(result == null ? "null" : $"Type={result.Type}, Port1={result.Port1}")}");

            if (result != null)
            {
                var vm = new PortRedirectionViewModel(result);
                ViewModel.PortRedirections.Add(vm);
                Console.WriteLine($"[PropertiesDialog] 已加入，目前共 {ViewModel.PortRedirections.Count} 個");
            }
            else
            {
                Console.WriteLine("[PropertiesDialog] 使用者取消");
            }
        }
        finally
        {
            BtnAdd.IsEnabled = true;
        }
    }

    private async void BtnEditRedir_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedRedirection == null)
            return;

        // 防止重複開啟對話框
        BtnEdit.IsEnabled = false;

        try
        {
            var existing = ViewModel.SelectedRedirection.ToModel();
            var result = await OpenTunnelDialog(existing);
            if (result != null)
            {
                var index = ViewModel.PortRedirections.IndexOf(ViewModel.SelectedRedirection);
                ViewModel.PortRedirections[index] = new PortRedirectionViewModel(result);
            }
        }
        finally
        {
            BtnEdit.IsEnabled = true;
        }
    }

    private void BtnDeleteRedir_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedRedirection != null)
        {
            ViewModel.PortRedirections.Remove(ViewModel.SelectedRedirection);
        }
    }

    /// <summary>
    /// 開啟私鑰檔案選擇器
    /// </summary>
    private async Task<string?> OpenPrivateKeyFilePicker()
    {
        var storage = StorageProvider;
        if (storage == null)
            return null;

        var options = new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance.GetString("Properties_SelectPrivateKey"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" }
                }
            }
        };

        // 設定建議的起始資料夾（~/.ssh/）
        var homeDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var sshDir = System.IO.Path.Combine(homeDir, ".ssh");
        if (System.IO.Directory.Exists(sshDir))
        {
            var folder = await storage.TryGetFolderFromPathAsync(sshDir);
            if (folder != null)
                options.SuggestedStartLocation = folder;
        }

        var result = await storage.OpenFilePickerAsync(options);
        if (result != null && result.Count > 0)
        {
            return result[0].Path.LocalPath;
        }

        return null;
    }

    /// <summary>
    /// 開啟 TunnelDialog
    /// </summary>
    private async Task<PortRedirection?> OpenTunnelDialog(PortRedirection? existing)
    {
        TunnelDialog dialog;

        if (existing != null)
        {
            // 編輯模式
            dialog = new TunnelDialog(existing);
        }
        else
        {
            // 新增模式
            dialog = new TunnelDialog();
        }

        var result = await dialog.ShowDialog<PortRedirection?>(this);
        return result;
    }
}
