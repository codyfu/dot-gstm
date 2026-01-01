using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.Services;
using DotGstm.Desktop.Utils;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.ComponentModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DotGstm.Desktop.Views;

public partial class MainWindow : Window
{
    private ObservableCollection<TunnelItem> _tunnels = new();
    private readonly ConfigService _configService = new();
    private TrayService? _trayService;

    // GstmTunnels wrapper class (corresponds to gSTM's struct sshtunnel **gSTMtunnels)
    // GstmTunnels 封裝類別（對應 gSTM 的 struct sshtunnel **gSTMtunnels）
    private readonly GstmTunnels _gstmTunnels = new();

    // SshService with GstmTunnels injected via constructor
    // SshService 透過建構函式注入 GstmTunnels
    private readonly SshService _sshService;

    // NativeMenu instances (for macOS compatibility - only set once)
    // NativeMenu 實例（為了 macOS 相容性 - 只設定一次）
    private NativeMenu? _menuBar = null;
    private NativeMenuItem? _helpMenuItem = null;
    private NativeMenuItem? _aboutMenuItem = null;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize SshService (dependency injection)
        // 初始化 SshService（依賴注入）
        _sshService = new SshService(_gstmTunnels);

        // Load tunnel configurations
        // 載入隧道設定
        _ = LoadTunnelsAsync();

        // Listen to selection changed event
        // 監聽選取變更事件
        TunnelList.SelectionChanged += TunnelList_SelectionChanged;

        // Register button events
        // 註冊按鈕事件
        BtnAdd.Click += BtnAdd_Click;
        BtnProperties.Click += BtnProperties_Click;
        BtnDelete.Click += BtnDelete_Click;
        BtnCopy.Click += BtnCopy_Click;
        BtnStart.Click += BtnStart_Click;
        BtnStop.Click += BtnStop_Click;
        BtnClose.Click += BtnClose_Click;

        // Create system tray (reference: gSTM GTK3 systray.c:gstm_docklet_create)
        // 建立系統托盤 (參照 gSTM GTK3 systray.c:gstm_docklet_create)
        _trayService = new TrayService(this, _tunnels, ToggleTunnelFromTray, ShowAboutFromTray);
        _trayService.Create();
        Console.WriteLine("[MainWindow] 系統托盤已初始化");

        // Window closing event: minimize to tray instead of exit (reference: gSTM callbacks.c:docklet_clicked)
        // 視窗關閉事件：最小化到托盤而非退出 (參照 gSTM callbacks.c:docklet_clicked)
        Closing += MainWindow_Closing;

        // Create menu bar
        // 建立選單列
        CreateMenuBar();

        // Initialize language selection
        // 初始化語言選擇
        InitializeLanguageComboBox();

        // Subscribe to language change event
        // 訂閱語言切換事件
        LocalizationService.Instance.LanguageChanged += (s, e) => UpdateUIText();

        // Initialize UI text
        // 初始化 UI 文字
        UpdateUIText();
    }

    /// <summary>
    /// Initialize language selection ComboBox
    /// 初始化語言選擇 ComboBox
    /// </summary>
    private void InitializeLanguageComboBox()
    {
        // 設定當前語言為選中項
        var currentLang = LocalizationService.Instance.CurrentLanguage;
        for (int i = 0; i < LanguageComboBox.ItemCount; i++)
        {
            if (LanguageComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == currentLang)
            {
                LanguageComboBox.SelectedIndex = i;
                break;
            }
        }

        // 訂閱語言切換事件
        LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
    }

    /// <summary>
    /// Language selection changed event
    /// 語言選擇變更事件
    /// </summary>
    private void LanguageComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            LocalizationService.Instance.ChangeLanguage(lang);
            // UpdateUIText() 會透過訂閱的 LanguageChanged 事件自動呼叫
        }
    }

    /// <summary>
    /// Update all UI text
    /// 更新所有 UI 文字
    /// </summary>
    private void UpdateUIText()
    {
        var loc = LocalizationService.Instance;

        // 更新視窗標題
        Title = loc.GetString("App_Title");

        // 更新按鈕文字
        if (BtnStart.Content is StackPanel startPanel && startPanel.Children[1] is TextBlock startText)
            startText.Text = loc.GetString("Btn_Start");

        if (BtnStop.Content is StackPanel stopPanel && stopPanel.Children[1] is TextBlock stopText)
            stopText.Text = loc.GetString("Btn_Stop");

        if (BtnAdd.Content is StackPanel addPanel && addPanel.Children[1] is TextBlock addText)
            addText.Text = loc.GetString("Btn_Add");

        if (BtnDelete.Content is StackPanel deletePanel && deletePanel.Children[1] is TextBlock deleteText)
            deleteText.Text = loc.GetString("Btn_Delete");

        if (BtnProperties.Content is StackPanel propPanel && propPanel.Children[1] is TextBlock propText)
            propText.Text = loc.GetString("Btn_Properties");

        if (BtnCopy.Content is StackPanel copyPanel && copyPanel.Children[1] is TextBlock copyText)
            copyText.Text = loc.GetString("Btn_Copy");

        if (BtnClose.Content is StackPanel closePanel && closePanel.Children[1] is TextBlock closeText)
            closeText.Text = loc.GetString("Btn_Close");

        // 更新 DataGrid 列標題
        if (TunnelList.Columns.Count > 1)
        {
            TunnelList.Columns[0].Header = loc.GetString("Column_Active");
            TunnelList.Columns[1].Header = loc.GetString("Column_Name");
        }

        // 更新選單
        UpdateMenuBar();

        // 更新狀態列文字
        if (TunnelList.SelectedItem == null)
        {
            CommandTextBox.Text = loc.GetString("Status_Ready");
        }

        Console.WriteLine($"[MainWindow] UI 文字已更新為 {loc.CurrentLanguage}");
    }

    /// <summary>
    /// Create menu bar (Help → About)
    /// 建立選單列 (Help → About)
    /// </summary>
    private void CreateMenuBar()
    {
        // Prevent duplicate creation
        // 防止重複建立
        if (_menuBar != null) return;

        var loc = LocalizationService.Instance;

        // Create main menu bar
        // 建立主選單列
        _menuBar = new NativeMenu();

        // Create Help menu item
        // 建立 Help 選單項目
        _helpMenuItem = new NativeMenuItem(loc.GetString("Menu_Help"));
        var helpSubMenu = new NativeMenu();

        // Create About menu item
        // 建立 About 選單項目
        _aboutMenuItem = new NativeMenuItem(loc.GetString("Menu_About"));
        _aboutMenuItem.Click += MenuAbout_Click;
        helpSubMenu.Items.Add(_aboutMenuItem);

        // Assemble menu structure
        // 組裝選單結構
        _helpMenuItem.Menu = helpSubMenu;
        _menuBar.Items.Add(_helpMenuItem);

        // Set menu to window (only once for macOS compatibility)
        // 設定選單到視窗（為了 macOS 相容性只執行一次）
        NativeMenu.SetMenu(this, _menuBar);
    }

    /// <summary>
    /// Update menu bar text
    /// 更新選單列文字
    /// </summary>
    private void UpdateMenuBar()
    {
        // If menu hasn't been created yet, create it first
        // 如果選單尚未建立，先建立
        if (_menuBar == null)
        {
            CreateMenuBar();
            return;
        }

        // Only update text, don't recreate menu (for macOS compatibility)
        // 只更新文字，不重新建立選單（為了 macOS 相容性）
        var loc = LocalizationService.Instance;
        _helpMenuItem!.Header = loc.GetString("Menu_Help");
        _aboutMenuItem!.Header = loc.GetString("Menu_About");

        // Do NOT call NativeMenu.SetMenu() again!
        // 不再調用 NativeMenu.SetMenu()！
    }

    private void TunnelList_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (TunnelList.SelectedItem is TunnelItem selectedItem)
        {
            // 有選取項目，顯示 SSH 指令
            if (_gstmTunnels.TryGet(selectedItem.Name, out var tunnel) && tunnel != null)
            {
                CommandTextBox.Text = BuildSshCommandDisplay(tunnel);
            }
            else
            {
                CommandTextBox.Text = "(無可用的隧道設定)";
            }
        }
        else
        {
            // No selection, show default message
            // 沒有選取，顯示預設訊息
            CommandTextBox.Text = "Ready for action.";
        }

        // Update button states (reference: gSTM fniface.c:gstm_interface_rowactivity)
        // 更新按鈕狀態 (參照 gSTM fniface.c:gstm_interface_rowactivity)
        UpdateButtonStates();
    }

    /// <summary>
    /// Update button states
    /// Reference: gSTM fniface.c:gstm_interface_disablebuttons / gstm_interface_enablebuttons
    /// 更新按鈕狀態
    /// 參照 gSTM fniface.c:gstm_interface_disablebuttons / gstm_interface_enablebuttons
    /// </summary>
    private void UpdateButtonStates()
    {
        if (TunnelList.SelectedItem is TunnelItem selectedItem)
        {
            // Tunnel selected - enable most buttons (reference: gstm_interface_enablebuttons)
            // 有選取 Tunnel - 啟用大部分按鈕 (參照 gstm_interface_enablebuttons)
            BtnDelete.IsEnabled = true;
            BtnProperties.IsEnabled = true;
            BtnCopy.IsEnabled = true;

            // Start/Stop based on tunnel state
            // Start/Stop 根據隧道狀態
            BtnStart.IsEnabled = !selectedItem.Active;  // Can press Start when tunnel is not active | Tunnel 未啟動時可按 Start
            BtnStop.IsEnabled = selectedItem.Active;     // Can press Stop when tunnel is active | Tunnel 啟動中時可按 Stop
        }
        else
        {
            // No tunnel selected - disable all buttons (reference: gstm_interface_disablebuttons)
            // 沒有選取 Tunnel - 停用所有按鈕 (參照 gstm_interface_disablebuttons)
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = false;
            BtnDelete.IsEnabled = false;
            BtnProperties.IsEnabled = false;
            BtnCopy.IsEnabled = false;
            // Note: Add button always stays enabled, not set here
            // 注意：Add 按鈕永遠保持啟用狀態，不在這裡設定
        }
    }

    /// <summary>
    /// Build SSH command string for display
    /// 建立要顯示的 SSH 指令字串
    /// </summary>
    private string BuildSshCommandDisplay(SshTunnel tunnel)
    {
        var parts = new List<string> { "ssh", tunnel.Host };

        parts.Add("-p");
        parts.Add(tunnel.Port);

        if (!string.IsNullOrWhiteSpace(tunnel.PrivateKeyPath) && tunnel.PrivateKeyPath.Length > 1)
        {
            parts.Add("-i");
            parts.Add(tunnel.PrivateKeyPath);
        }

        parts.Add("-l");
        parts.Add(tunnel.Login);

        parts.Add("-nN");

        foreach (var redir in tunnel.PortRedirections)
        {
            var type = redir.Type.ToLower();
            if (type == "local")
            {
                parts.Add($"-L{redir.Port1}:{redir.Host}:{redir.Port2}");
            }
            else if (type == "remote")
            {
                parts.Add($"-R{redir.Port1}:{redir.Host}:{redir.Port2}");
            }
            else if (type == "dynamic")
            {
                parts.Add($"-D{redir.Port1}");
            }
        }

        parts.Add("-o");
        parts.Add("ConnectTimeout=5");
        parts.Add("-o");
        parts.Add("NumberOfPasswordPrompts=1");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Load all tunnels from configuration files
    /// Follows gSTM main.c:gstm_populate_treeview
    /// 從設定檔載入所有隧道
    /// 遵循 gSTM main.c:gstm_populate_treeview
    /// </summary>
    private async Task LoadTunnelsAsync()
    {
        Console.WriteLine("[MainWindow] 載入隧道設定");

        try
        {
            // Load all tunnels from ConfigService
            // 從 ConfigService 載入所有隧道
            var tunnels = await _configService.LoadTunnelsAsync();

            // Clear existing data
            // 清空現有資料
            _tunnels.Clear();

            // Batch set to GstmTunnels (atomic operation)
            // 批次設定到 GstmTunnels（原子操作）
            _gstmTunnels.SetBatch(tunnels);

            // Add loaded tunnels to UI collection
            // 將載入的隧道加入到 UI 集合
            foreach (var tunnel in tunnels)
            {
                _tunnels.Add(new TunnelItem { Name = tunnel.Name, Active = false });
                Console.WriteLine($"[MainWindow] 已載入: {tunnel.Name}");
            }

            // Set DataGrid data source
            // 設定 DataGrid 資料來源
            if (TunnelList != null)
            {
                TunnelList.ItemsSource = _tunnels;
            }

            Console.WriteLine($"[MainWindow] 共載入 {_tunnels.Count} 個隧道");

            // Process Auto-start tunnels (reference: gSTM main.c:gstm_process_autostart)
            // 處理 Auto-start 隧道 (參照 gSTM main.c:gstm_process_autostart)
            await ProcessAutoStartTunnelsAsync();

            // Initialize button states (disable related buttons when no item selected)
            // 初始化按鈕狀態（沒有選取項目時停用相關按鈕）
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 載入隧道失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// Process Auto-start tunnels
    /// Reference: gSTM main.c:gstm_process_autostart
    /// 處理 Auto-start 隧道
    /// 參照 gSTM main.c:gstm_process_autostart
    /// </summary>
    private async Task ProcessAutoStartTunnelsAsync()
    {
        Console.WriteLine("[MainWindow] 開始處理 Auto-start 隧道");
        int autoStartCount = 0;

        // 取得所有 tunnel 的快照
        var tunnelsSnapshot = _gstmTunnels.GetSnapshot();

        foreach (var tunnel in tunnelsSnapshot)
        {
            if (tunnel.AutoStart)
            {
                Console.WriteLine($"[MainWindow] 自動啟動隧道: {tunnel.Name}");

                // 找到對應的 TunnelItem
                var tunnelItem = _tunnels.FirstOrDefault(t => t.Name == tunnel.Name);
                if (tunnelItem != null)
                {
                    // 啟動隧道（改為同步方法）
                    _sshService.StartTunnel(tunnel.Name);

                    // 等待一小段時間讓 helper thread 啟動
                    await Task.Delay(100);

                    // 檢查是否成功啟動
                    bool isActive = _gstmTunnels.GetActive(tunnel.Name);

                    if (isActive)
                    {
                        tunnelItem.Active = true;
                        autoStartCount++;
                        Console.WriteLine($"[MainWindow] 已自動啟動: {tunnel.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"[MainWindow] 自動啟動失敗: {tunnel.Name}");
                    }
                }
            }
        }

        // 觸發 UI 更新
        if (autoStartCount > 0 && TunnelList != null)
        {
            TunnelList.ItemsSource = null;
            TunnelList.ItemsSource = _tunnels;
            // 更新托盤選單以反映自動啟動的隧道狀態
            _trayService?.UpdateMenu();
        }

        Console.WriteLine($"[MainWindow] 共自動啟動 {autoStartCount} 個隧道");
    }

    // === Button event handlers ===
    // === 按鈕事件處理 ===

    /// <summary>
    /// Add button click event
    /// Follows gSTM callbacks.c:btn_add_clicked_cb
    /// 加入按鈕點擊事件
    /// 遵循 gSTM callbacks.c:btn_add_clicked_cb
    /// </summary>
    private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[MainWindow] 點擊「加入」按鈕");

        // 1. Ask for new tunnel name (follows gSTM fniface.c:gstm_interface_asknewname)
        // 1. 詢問新隧道名稱（遵循 gSTM fniface.c:gstm_interface_asknewname）
        var nameDialog = new NameDialog(_gstmTunnels.GetNames());
        var nameResult = await nameDialog.ShowDialog<bool?>(this);

        if (nameResult != true || string.IsNullOrEmpty(nameDialog.TunnelName))
        {
            Console.WriteLine("[MainWindow] 使用者取消了或名稱無效");
            return;
        }

        var tunnelName = nameDialog.TunnelName;
        Console.WriteLine($"[MainWindow] 新隧道名稱: {tunnelName}");

        // 2. Create new SSH tunnel (follows gSTM callbacks.c:89)
        // 2. 建立新的 SSH 隧道（遵循 gSTM callbacks.c:89）
        var newTunnel = new SshTunnel
        {
            Name = tunnelName,
            Host = "",
            Port = "22",
            Login = "",
            PrivateKeyPath = "",
            AutoStart = false,
            Restart = true,
            Notify = true,
            MaxRestarts = "9",
            Preset = false
        };

        // Add to list and configuration first
        // 先加入到清單和設定中
        _gstmTunnels.Set(newTunnel.Name, newTunnel);
        _tunnels.Add(new TunnelItem { Name = newTunnel.Name, Active = false });

        // 3. Auto-open Properties dialog (follows gSTM callbacks.c:93)
        // 3. 自動開啟 Properties 對話框（遵循 gSTM callbacks.c:93）
        var propertiesDialog = new PropertiesDialog(newTunnel);
        Console.WriteLine("[MainWindow] 開啟 PropertiesDialog");
        var result = await propertiesDialog.ShowDialog<SshTunnel?>(this);

        if (result != null)
        {
            // Update configuration
            // 更新設定
            _gstmTunnels.Set(result.Name, result);
            Console.WriteLine($"已新增隧道: {result.Name}, 共 {result.PortRedirections.Count} 個端口轉發");

            // Save to configuration file (follows gSTM conffile.c:gstm_tunnel_add)
            // 儲存到設定檔（遵循 gSTM conffile.c:gstm_tunnel_add）
            var saved = await _configService.SaveTunnelAsync(result);
            if (saved)
            {
                Console.WriteLine($"[MainWindow] 已儲存隧道設定: {result.Name}");
            }
            else
            {
                Console.WriteLine($"[MainWindow] 儲存隧道設定失敗: {result.Name}");
            }
        }
        else
        {
            // User cancelled in Properties dialog, remove just-added tunnel
            // 使用者在 Properties 對話框中取消了，移除剛才加入的隧道
            Console.WriteLine("[MainWindow] 使用者在 Properties 對話框中取消了，移除隧道");
            _gstmTunnels.Remove(tunnelName);
            var itemToRemove = _tunnels.FirstOrDefault(t => t.Name == tunnelName);
            if (itemToRemove != null)
            {
                _tunnels.Remove(itemToRemove);
            }
        }
    }

    private async void BtnProperties_Click(object? sender, RoutedEventArgs e)
    {
        if (TunnelList.SelectedItem is not TunnelItem selectedItem)
        {
            Console.WriteLine("請先選擇一個隧道");
            return;
        }

        if (!_gstmTunnels.TryGet(selectedItem.Name, out var tunnel) || tunnel == null)
        {
            Console.WriteLine($"找不到隧道設定: {selectedItem.Name}");
            return;
        }

        // 開啟屬性對話框
        var dialog = new PropertiesDialog(tunnel);
        var result = await dialog.ShowDialog<SshTunnel?>(this);

        if (result != null)
        {
            // 使用者按了 OK，更新隧道資訊
            var oldName = selectedItem.Name;
            var oldFileName = tunnel.FileName;
            selectedItem.Name = result.Name;

            // 更新設定字典
            if (oldName != result.Name)
            {
                _gstmTunnels.Remove(oldName);
            }
            _gstmTunnels.Set(result.Name, result);

            // 儲存到設定檔
            var saved = await _configService.SaveTunnelAsync(result);
            if (saved)
            {
                Console.WriteLine($"[MainWindow] 已儲存隧道設定: {result.Name}");

                // 如果名稱改變，刪除舊檔案
                if (oldName != result.Name && !string.IsNullOrEmpty(oldFileName))
                {
                    await _configService.DeleteTunnelAsync(oldFileName);
                    Console.WriteLine($"[MainWindow] 已刪除舊設定檔: {oldFileName}");
                }
            }
            else
            {
                Console.WriteLine($"[MainWindow] 儲存隧道設定失敗: {result.Name}");
            }

            // 觸發 UI 更新
            TunnelList.ItemsSource = null;
            TunnelList.ItemsSource = _tunnels;

            Console.WriteLine($"已更新隧道: {result.Name}");
        }
    }

    /// <summary>
    /// Delete button click event
    /// Follows gSTM callbacks.c:btn_delete_clicked_cb
    /// 刪除按鈕點擊事件
    /// 遵循 gSTM callbacks.c:btn_delete_clicked_cb
    /// </summary>
    private async void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (TunnelList.SelectedItem is not TunnelItem selectedItem)
        {
            Console.WriteLine("請先選擇一個隧道");
            return;
        }

        var loc = LocalizationService.Instance;

        // 顯示確認對話框（遵循 gSTM callbacks.c:102-120）
        var messageBox = MessageBoxManager
            .GetMessageBoxStandard(
                loc.GetString("Dialog_DeleteTitle"),
                loc.GetString("Dialog_DeleteMessage", selectedItem.Name),
                ButtonEnum.YesNo);

        var result = await messageBox.ShowWindowDialogAsync(this);

        // 只有點擊 Yes 才執行刪除
        if (result != ButtonResult.Yes)
        {
            Console.WriteLine($"[MainWindow] 取消刪除隧道: {selectedItem.Name}");
            return;
        }

        Console.WriteLine($"[MainWindow] 確認刪除隧道: {selectedItem.Name}");

        // 如果隧道正在執行，先停止
        if (selectedItem.Active)
        {
            _sshService.StopTunnel(selectedItem.Name);
        }

        // 從設定檔刪除（遵循 gSTM conffile.c:gstm_tunnel_del）
        if (_gstmTunnels.TryGet(selectedItem.Name, out var tunnel) &&
            tunnel != null &&
            !string.IsNullOrEmpty(tunnel.FileName))
        {
            var deleted = await _configService.DeleteTunnelAsync(tunnel.FileName);
            if (deleted)
            {
                Console.WriteLine($"[MainWindow] 已刪除設定檔: {tunnel.FileName}");
            }
            else
            {
                Console.WriteLine($"[MainWindow] 刪除設定檔失敗: {tunnel.FileName}");
            }
        }

        _tunnels.Remove(selectedItem);
        _gstmTunnels.Remove(selectedItem.Name);
        Console.WriteLine($"[MainWindow] 已刪除隧道: {selectedItem.Name}");
    }

    /// <summary>
    /// Copy button click event
    /// Follows gSTM callbacks.c:btn_copy_clicked_cb
    /// 複製按鈕點擊事件
    /// 遵循 gSTM callbacks.c:btn_copy_clicked_cb
    /// </summary>
    private async void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (TunnelList.SelectedItem is not TunnelItem selectedItem)
        {
            Console.WriteLine("請先選擇一個隧道");
            return;
        }

        if (!_gstmTunnels.TryGet(selectedItem.Name, out var originalTunnel) || originalTunnel == null)
        {
            Console.WriteLine($"找不到隧道設定: {selectedItem.Name}");
            return;
        }

        // 1. 詢問新隧道名稱（遵循 gSTM callbacks.c:157）
        var nameDialog = new NameDialog(_gstmTunnels.GetNames());
        var nameResult = await nameDialog.ShowDialog<bool?>(this);

        if (nameResult != true || string.IsNullOrEmpty(nameDialog.TunnelName))
        {
            Console.WriteLine("[MainWindow] 使用者取消了複製");
            return;
        }

        var tunnelName = nameDialog.TunnelName;
        Console.WriteLine($"[MainWindow] 複製隧道 '{selectedItem.Name}' 為 '{tunnelName}'");

        // 2. 深度複製隧道設定（遵循 gSTM callbacks.c:169-183）
        var copiedTunnel = new SshTunnel
        {
            Name = tunnelName,
            Host = originalTunnel.Host,
            Port = originalTunnel.Port,
            Login = originalTunnel.Login,
            PrivateKeyPath = originalTunnel.PrivateKeyPath,
            AutoStart = originalTunnel.AutoStart,
            Restart = originalTunnel.Restart,
            Notify = originalTunnel.Notify,
            MaxRestarts = originalTunnel.MaxRestarts,
            Preset = originalTunnel.Preset
        };

        // 複製端口轉發
        foreach (var redir in originalTunnel.PortRedirections)
        {
            copiedTunnel.PortRedirections.Add(new PortRedirection
            {
                Type = redir.Type,
                Port1 = redir.Port1,
                Host = redir.Host,
                Port2 = redir.Port2
            });
        }

        // 3. 加入到清單
        var copiedItem = new TunnelItem
        {
            Name = copiedTunnel.Name,
            Active = false
        };

        _gstmTunnels.Set(copiedTunnel.Name, copiedTunnel);
        _tunnels.Add(copiedItem);
        Console.WriteLine($"[MainWindow] 已複製隧道: {copiedItem.Name}");

        // 4. 儲存到設定檔
        var saved = await _configService.SaveTunnelAsync(copiedTunnel);
        if (saved)
        {
            Console.WriteLine($"[MainWindow] 已儲存複製的隧道設定: {copiedTunnel.Name}");
        }
        else
        {
            Console.WriteLine($"[MainWindow] 儲存複製的隧道設定失敗: {copiedTunnel.Name}");
        }
    }

    private void BtnStart_Click(object? sender, RoutedEventArgs e)
    {
        if (TunnelList.SelectedItem is not TunnelItem selectedItem)
        {
            Console.WriteLine("請先選擇一個隧道");
            return;
        }

        if (selectedItem.Active)
        {
            Console.WriteLine($"隧道 '{selectedItem.Name}' 已經在執行中");
            return;
        }

        if (!_gstmTunnels.TryGet(selectedItem.Name, out var tunnel))
        {
            Console.WriteLine($"找不到隧道設定: {selectedItem.Name}");
            return;
        }

        Console.WriteLine($"正在啟動隧道: {selectedItem.Name}");

        // 使用 SshService 啟動隧道（改為同步方法）
        _sshService.StartTunnel(selectedItem.Name);

        // 更新 UI 狀態
        selectedItem.Active = true;

        // 觸發 UI 更新
        TunnelList.ItemsSource = null;
        TunnelList.ItemsSource = _tunnels;

        // 更新托盤選單
        _trayService?.UpdateMenu();

        // 更新按鈕狀態
        UpdateButtonStates();

        Console.WriteLine($"已啟動隧道: {selectedItem.Name}");
    }

    private void BtnStop_Click(object? sender, RoutedEventArgs e)
    {
        if (TunnelList.SelectedItem is not TunnelItem selectedItem)
        {
            Console.WriteLine("請先選擇一個隧道");
            return;
        }

        if (!selectedItem.Active)
        {
            Console.WriteLine($"隧道 '{selectedItem.Name}' 並未執行");
            return;
        }

        Console.WriteLine($"正在停止隧道: {selectedItem.Name}");

        // 使用 SshService 停止隧道
        _sshService.StopTunnel(selectedItem.Name);

        selectedItem.Active = false;
        // 觸發 UI 更新
        TunnelList.ItemsSource = null;
        TunnelList.ItemsSource = _tunnels;
        // 更新托盤選單
        _trayService?.UpdateMenu();
        // 更新按鈕狀態
        UpdateButtonStates();
        Console.WriteLine($"已停止隧道: {selectedItem.Name}");
    }

    /// <summary>
    /// Toggle tunnel state from tray menu
    /// Reference: gSTM callbacks.c:on_dockletmenu_tunnel_activate
    /// 從托盤選單切換隧道狀態
    /// 參照 gSTM callbacks.c:on_dockletmenu_tunnel_activate
    /// </summary>
    private void ToggleTunnelFromTray(string tunnelName)
    {
        Console.WriteLine($"[MainWindow] 從托盤切換隧道: {tunnelName}");

        var tunnelItem = _tunnels.FirstOrDefault(t => t.Name == tunnelName);
        if (tunnelItem == null)
        {
            Console.WriteLine($"[MainWindow] 找不到隧道: {tunnelName}");
            return;
        }

        if (!_gstmTunnels.TryGet(tunnelName, out var tunnel) || tunnel == null)
        {
            Console.WriteLine($"[MainWindow] 找不到隧道設定: {tunnelName}");
            return;
        }

        if (tunnelItem.Active)
        {
            // 停止隧道
            _sshService.StopTunnel(tunnelName);
            tunnelItem.Active = false;
            Console.WriteLine($"[MainWindow] 已從托盤停止隧道: {tunnelName}");
        }
        else
        {
            // 啟動隧道（改為同步方法）
            _sshService.StartTunnel(tunnelName);
            tunnelItem.Active = true;
            Console.WriteLine($"[MainWindow] 已從托盤啟動隧道: {tunnelName}");
        }

        // 觸發 UI 更新
        TunnelList.ItemsSource = null;
        TunnelList.ItemsSource = _tunnels;

        // 更新托盤選單
        _trayService?.UpdateMenu();

        // 如果當前選取的是被切換的隧道，更新按鈕狀態
        if (TunnelList.SelectedItem is TunnelItem selected && selected.Name == tunnelName)
        {
            UpdateButtonStates();
        }
    }

    /// <summary>
    /// Show About dialog from tray menu
    /// Reference: gSTM GTK3 callbacks.c:on_dockletmenu_about_activate
    /// 從托盤選單開啟 About 對話框
    /// 參照 gSTM GTK3 callbacks.c:on_dockletmenu_about_activate
    /// </summary>
    private async void ShowAboutFromTray()
    {
        Console.WriteLine("[MainWindow] 從托盤開啟 About 對話框");
        var aboutDialog = new AboutDialog();
        await aboutDialog.ShowDialog(this);
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("關閉應用程式");
        Close();
    }

    /// <summary>
    /// Window closing event: confirm quit if there are active tunnels
    /// Reference: gSTM callbacks.c:33-53 (gstm_terminate function)
    /// 視窗關閉事件：如有活動隧道則確認是否退出
    /// 參照 gSTM callbacks.c:33-53 (gstm_terminate 函式)
    /// </summary>
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // 檢查是否有活動的隧道
        int activeCount = _tunnels.Count(t => t.Active);

        if (activeCount > 0)
        {
            // 取消預設關閉行為（遵循 gSTM callbacks.c:35-48）
            // Cancel default closing behavior (follows gSTM callbacks.c:35-48)
            e.Cancel = true;

            var loc = LocalizationService.Instance;

            // 顯示確認對話框（遵循 gSTM callbacks.c:35-48）
            // Show confirmation dialog (follows gSTM callbacks.c:35-48)
            DebugLogger.Log($"[MainWindow] 有 {activeCount} 個活動隧道，顯示退出確認對話框");

            var messageBox = MessageBoxManager
                .GetMessageBoxStandard(
                    loc.GetString("Dialog_QuitTitle"),  // "Really quit?"
                    loc.GetString("Dialog_QuitMessage", activeCount),
                    // "There are {0} active tunnels. If you quit, all tunnels will be closed."
                    ButtonEnum.YesNo);

            var result = await messageBox.ShowWindowDialogAsync(this);

            // 如果使用者選擇 No，不執行任何操作（視窗保持開啟）
            // If user chooses No, do nothing (window stays open)
            if (result != ButtonResult.Yes)
            {
                DebugLogger.Log($"[MainWindow] 使用者取消退出");
                return;
            }

            DebugLogger.Log($"[MainWindow] 使用者確認退出，停止所有隧道");
        }

        // 停止所有隧道（遵循 gSTM callbacks.c:49）
        // Stop all tunnels (follows gSTM callbacks.c:49)
        DebugLogger.Log("[MainWindow] 停止所有隧道");
        foreach (var tunnel in _tunnels.Where(t => t.Active).ToList())
        {
            _sshService.StopTunnel(tunnel.Name);
        }

        // 銷毀系統托盤
        // Dispose system tray
        _trayService?.Dispose();

        DebugLogger.Log("[MainWindow] 視窗正在關閉");

        // 強制關閉（避免遞迴）
        // Force close (avoid recursion)
        Closing -= MainWindow_Closing;
        Close();
    }

    /// <summary>
    /// Menu: Help → About
    /// 選單：說明 → 關於
    /// </summary>
    private async void MenuAbout_Click(object? sender, EventArgs e)
    {
        Console.WriteLine("[MainWindow] 開啟 About 對話框");
        var aboutDialog = new AboutDialog();
        await aboutDialog.ShowDialog(this);
    }

    /// <summary>
    /// Banner image click event: open About dialog
    /// Banner 圖片點擊事件：開啟 About 對話框
    /// </summary>
    private async void BannerImage_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        Console.WriteLine("[MainWindow] 點擊 Banner 圖片，開啟 About 對話框");
        var aboutDialog = new AboutDialog();
        await aboutDialog.ShowDialog(this);
    }
}

/// <summary>
/// Design-time sample data
/// 設計時範例資料
/// </summary>
public class TunnelItem
{
    private bool _active;

    public string Name { get; set; } = "";

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            IsInactive = !value;
        }
    }

    public bool IsInactive { get; set; }
}

/// <summary>
/// Status image converter: Active = true shows green.png, false shows red.png
/// 狀態圖片轉換器：Active = true 顯示 green.png，false 顯示 red.png
/// </summary>
public class StatusImageConverter : IValueConverter
{
    public static readonly StatusImageConverter Instance = new();
    private static Bitmap? _greenBitmap;
    private static Bitmap? _redBitmap;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            try
            {
                if (isActive)
                {
                    if (_greenBitmap == null)
                    {
                        var uri = new Uri("avares://dstm/Assets/Images/green.png");
                        _greenBitmap = new Bitmap(AssetLoader.Open(uri));
                        Console.WriteLine("[StatusImageConverter] 已載入 green.png");
                    }
                    return _greenBitmap;
                }
                else
                {
                    if (_redBitmap == null)
                    {
                        var uri = new Uri("avares://dstm/Assets/Images/red.png");
                        _redBitmap = new Bitmap(AssetLoader.Open(uri));
                        Console.WriteLine("[StatusImageConverter] 已載入 red.png");
                    }
                    return _redBitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatusImageConverter] 載入圖示失敗: {ex.Message}");
                return null;
            }
        }

        Console.WriteLine("[StatusImageConverter] 值不是 bool 類型");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
