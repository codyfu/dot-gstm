using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using DotGstm.Desktop.Views;
using DotGstm.Desktop.Services;
using DotGstm.Desktop.Utils;

namespace DotGstm.Desktop.Services;

/// <summary>
/// 系統托盤服務 (參照 gSTM notarea.c, fniface.c:gstm_create_dockletmenu)
///
/// 功能：
/// - 建立系統托盤圖示
/// - 左鍵點擊：顯示/隱藏主視窗
/// - 右鍵點擊：顯示托盤選單（包含動態隧道清單）
/// - 雙擊：顯示/隱藏主視窗
/// </summary>
public class TrayService : IDisposable
{
    private TrayIcon? _trayIcon;
    private NativeMenu? _trayMenu = null;  // For macOS compatibility - only set once
    private readonly Window _mainWindow;
    private readonly ObservableCollection<TunnelItem> _tunnels;
    private readonly Action<string> _onTunnelToggle;
    private readonly Action _onShowAbout;
    private bool _isDisposed = false;

    public TrayService(Window mainWindow, ObservableCollection<TunnelItem> tunnels,
                       Action<string> onTunnelToggle, Action onShowAbout)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _tunnels = tunnels ?? throw new ArgumentNullException(nameof(tunnels));
        _onTunnelToggle = onTunnelToggle ?? throw new ArgumentNullException(nameof(onTunnelToggle));
        _onShowAbout = onShowAbout ?? throw new ArgumentNullException(nameof(onShowAbout));

        // 監聽隧道清單變化，自動更新選單
        _tunnels.CollectionChanged += (s, e) => UpdateMenu();

        // 訂閱語言變更事件
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// 建立系統托盤圖示 (參照 gSTM notarea.c:docklet_x11_create)
    /// </summary>
    public void Create()
    {
        if (_trayIcon != null)
        {
            DebugLogger.Log("[TrayService] 托盤圖示已存在，先銷毀");
            Destroy();
        }

        DebugLogger.Log("[TrayService] 建立系統托盤圖示");

        _trayIcon = new TrayIcon();

        // 設定圖示 (從嵌入資源載入)
        try
        {
            var assets = AssetLoader.Open(new Uri("avares://dstm/Assets/Images/gSTM.png"));
            var bitmap = new Bitmap(assets);
            _trayIcon.Icon = new Avalonia.Controls.WindowIcon(bitmap);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[TrayService] 載入圖示失敗: {ex.Message}");
        }

        // 設定工具提示
        _trayIcon.ToolTipText = "SSH Tunnel Manager";

        // 建立托盤選單
        UpdateMenu();

        // 左鍵點擊事件 (參照 gSTM callbacks.c:docklet_clicked case 1)
        _trayIcon.Clicked += (s, e) => ToggleMainWindow();

        // 顯示托盤圖示
        _trayIcon.IsVisible = true;

        DebugLogger.Log("[TrayService] 系統托盤圖示已建立");
    }

    /// <summary>
    /// 語言變更事件處理器
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DebugLogger.Log("[TrayService] 語言已變更，更新選單");
        UpdateMenu();
    }

    /// <summary>
    /// 更新托盤選單 (參照 gSTM GTK3 forked systray.c:gstm_docklet_menu_regen)
    /// </summary>
    public void UpdateMenu()
    {
        if (_trayIcon == null) return;

        // For macOS compatibility: create menu only once, then reuse it
        // 為了 macOS 相容性：只建立一次選單，之後重複使用
        if (_trayMenu == null)
        {
            _trayMenu = new NativeMenu();
            _trayIcon.Menu = _trayMenu;  // Set menu only once!
        }
        else
        {
            // Clear existing items and rebuild
            // 清空現有項目並重建
            _trayMenu.Items.Clear();
        }

        var menu = _trayMenu;

        // 1. Show/Hide gSTM (動態文字，參照 systray.c:138-153)
        var toggleText = _mainWindow.IsVisible
            ? $"🔑 {LocalizationService.Instance.GetString("Tray_HideGstm")}"
            : $"🔑 {LocalizationService.Instance.GetString("Tray_ShowGstm")}";
        var toggleItem = new NativeMenuItem(toggleText);
        toggleItem.Click += (s, e) => ToggleMainWindow();
        menu.Items.Add(toggleItem);

        // 2. 分隔線 1 (參照 systray.c:155-159)
        menu.Items.Add(new NativeMenuItemSeparator());

        // 3. 動態隧道清單 (參照 systray.c:161-174)
        foreach (var tunnel in _tunnels)
        {
            // GTK3 版本使用 "Connect: " 或 "Disconnect: " 前綴 (systray.c:106-109)
            // 注意：NativeMenuItem 不支援圖片，所以使用 🟢/🔴 emoji 表示狀態
            string itemText;
            if (tunnel.Active)
            {
                // 啟動中：顯示 "Disconnect: " (綠色圖示)
                itemText = $"🟢 {LocalizationService.Instance.GetString("Tray_Disconnect")}{tunnel.Name}";
            }
            else
            {
                // 未啟動：顯示 "Connect: " (紅色圖示)
                itemText = $"🔴 {LocalizationService.Instance.GetString("Tray_Connect")}{tunnel.Name}";
            }

            var tunnelItem = new NativeMenuItem(itemText);

            // 點擊切換隧道狀態 (參照 callbacks.c:on_dockletmenu_tunnel_activate)
            var tunnelName = tunnel.Name; // 捕獲變數
            tunnelItem.Click += (s, e) => _onTunnelToggle(tunnelName);

            menu.Items.Add(tunnelItem);
        }

        // 4. 分隔線 2 (參照 systray.c:176-180)
        menu.Items.Add(new NativeMenuItemSeparator());

        // 5. About (參照 systray.c:182-195)
        var aboutItem = new NativeMenuItem($"⭐ {LocalizationService.Instance.GetString("Tray_About")}");
        aboutItem.Click += (s, e) => ShowAboutDialog();
        menu.Items.Add(aboutItem);

        // 6. Quit (參照 systray.c:197-209)
        var quitItem = new NativeMenuItem($"🚪 {LocalizationService.Instance.GetString("Tray_Quit")}");
        quitItem.Click += (s, e) => QuitApplication();
        menu.Items.Add(quitItem);

        // Don't set _trayIcon.Menu again - already set on first creation for macOS compatibility
        // 不再設定 _trayIcon.Menu - 為了 macOS 相容性已在第一次建立時設定

        DebugLogger.Log($"[TrayService] 托盤選單已更新，包含 {_tunnels.Count} 個隧道");
    }

    /// <summary>
    /// 銷毀系統托盤圖示 (參照 gSTM notarea.c:docklet_x11_destroy)
    /// </summary>
    public void Destroy()
    {
        if (_trayIcon != null)
        {
            DebugLogger.Log("[TrayService] 銷毀系統托盤圖示");
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    /// <summary>
    /// 切換主視窗顯示/隱藏 (參照 gSTM callbacks.c:docklet_clicked case 1)
    /// </summary>
    private void ToggleMainWindow()
    {
        if (_mainWindow.IsVisible)
        {
            DebugLogger.Log("[TrayService] 隱藏主視窗");
            _mainWindow.Hide();
        }
        else
        {
            DebugLogger.Log("[TrayService] 顯示主視窗");
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        // 更新選單文字 (Show gSTM <-> Hide gSTM)
        UpdateMenu();
    }

    /// <summary>
    /// 顯示 About 對話框 (參照 gSTM GTK3 systray.c:182-195)
    /// </summary>
    private void ShowAboutDialog()
    {
        DebugLogger.Log("[TrayService] 從托盤開啟 About 對話框");
        _onShowAbout();
    }

    /// <summary>
    /// 退出應用程式 (參照 gSTM GTK3 systray.c:197-209)
    /// </summary>
    private void QuitApplication()
    {
        DebugLogger.Log("[TrayService] 從托盤退出應用程式");
        _mainWindow.Close();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // 取消訂閱語言變更事件
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

            Destroy();
            _isDisposed = true;
        }
    }
}
