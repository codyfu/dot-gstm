using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using DotGstm.Desktop.Models;
using ReactiveUI;

namespace DotGstm.Desktop.ViewModels;

/// <summary>
/// 隧道屬性對話框的 ViewModel
/// 對應 PropertiesDialog.axaml
/// </summary>
public class PropertiesDialogViewModel : ViewModelBase
{
    // ===== 基本屬性 =====

    private string _name = "";
    /// <summary>
    /// 隧道名稱
    /// </summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    // ===== SSH Preset =====

    private ObservableCollection<string> _presets = new() { "No presets available" };
    /// <summary>
    /// SSH Preset 選項列表（從 ~/.ssh/config 讀取）
    /// </summary>
    public ObservableCollection<string> Presets
    {
        get => _presets;
        set => this.RaiseAndSetIfChanged(ref _presets, value);
    }

    private int _selectedPresetIndex = 0;
    /// <summary>
    /// 選取的 Preset 索引
    /// </summary>
    public int SelectedPresetIndex
    {
        get => _selectedPresetIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPresetIndex, value);
            OnPresetChanged();
        }
    }

    private bool _isPresetMode = false;
    /// <summary>
    /// 是否使用 Preset 模式
    /// </summary>
    public bool IsPresetMode
    {
        get => _isPresetMode;
        set => this.RaiseAndSetIfChanged(ref _isPresetMode, value);
    }

    // ===== SSH 連線設定 =====

    private string _user = "";
    /// <summary>
    /// SSH 使用者名稱
    /// </summary>
    public string User
    {
        get => _user;
        set => this.RaiseAndSetIfChanged(ref _user, value);
    }

    private string _host = "";
    /// <summary>
    /// SSH 主機位址
    /// </summary>
    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    private string _port = "22";
    /// <summary>
    /// SSH 端口
    /// </summary>
    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private string _privateKey = "";
    /// <summary>
    /// 私鑰檔案路徑
    /// </summary>
    public string PrivateKey
    {
        get => _privateKey;
        set => this.RaiseAndSetIfChanged(ref _privateKey, value);
    }

    private bool _isConnectionFieldsEnabled = true;
    /// <summary>
    /// User/Host/Port/PrivateKey 欄位是否啟用
    /// （選擇 Preset 時禁用）
    /// </summary>
    public bool IsConnectionFieldsEnabled
    {
        get => _isConnectionFieldsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isConnectionFieldsEnabled, value);
    }

    // ===== 選項 =====

    private bool _autoStart = false;
    /// <summary>
    /// 自動啟動
    /// </summary>
    public bool AutoStart
    {
        get => _autoStart;
        set => this.RaiseAndSetIfChanged(ref _autoStart, value);
    }

    private bool _autoRestart = false;
    /// <summary>
    /// 自動重連
    /// </summary>
    public bool AutoRestart
    {
        get => _autoRestart;
        set => this.RaiseAndSetIfChanged(ref _autoRestart, value);
    }

    private bool _notify = true;
    /// <summary>
    /// 顯示通知
    /// </summary>
    public bool Notify
    {
        get => _notify;
        set => this.RaiseAndSetIfChanged(ref _notify, value);
    }

    private string _maxRestarts = "9";
    /// <summary>
    /// 最大重連次數
    /// </summary>
    public string MaxRestarts
    {
        get => _maxRestarts;
        set => this.RaiseAndSetIfChanged(ref _maxRestarts, value);
    }

    // ===== Port Redirections =====

    private ObservableCollection<PortRedirectionViewModel> _portRedirections = new();
    /// <summary>
    /// 端口轉發列表
    /// </summary>
    public ObservableCollection<PortRedirectionViewModel> PortRedirections
    {
        get => _portRedirections;
        set => this.RaiseAndSetIfChanged(ref _portRedirections, value);
    }

    private PortRedirectionViewModel? _selectedRedirection;
    /// <summary>
    /// 選取的端口轉發項目
    /// </summary>
    public PortRedirectionViewModel? SelectedRedirection
    {
        get => _selectedRedirection;
        set => this.RaiseAndSetIfChanged(ref _selectedRedirection, value);
    }

    // ===== 對話框結果 =====

    /// <summary>
    /// 對話框是否以 OK 關閉
    /// </summary>
    public bool DialogResult { get; private set; }

    /// <summary>
    /// 編輯後的 SSH 隧道設定
    /// </summary>
    public SshTunnel? Result { get; private set; }

    // ===== 命令 =====

    /// <summary>
    /// 瀏覽私鑰檔案命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> BrowsePrivateKeyCommand { get; }

    /// <summary>
    /// 新增端口轉發命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> AddRedirectionCommand { get; }

    /// <summary>
    /// 編輯端口轉發命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditRedirectionCommand { get; }

    /// <summary>
    /// 刪除端口轉發命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> DeleteRedirectionCommand { get; }

    /// <summary>
    /// 確定命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> OkCommand { get; }

    /// <summary>
    /// 取消命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    // ===== 對話框互動委託 =====

    /// <summary>
    /// 開啟檔案選擇器的委託（由 View 提供）
    /// </summary>
    public Func<Task<string?>>? OpenFilePickerHandler { get; set; }

    /// <summary>
    /// 開啟 TunnelDialog 的委託（由 View 提供）
    /// 參數：現有的 PortRedirection（編輯模式）或 null（新增模式）
    /// 回傳：編輯後的 PortRedirection 或 null（取消）
    /// </summary>
    public Func<PortRedirection?, Task<PortRedirection?>>? OpenTunnelDialogHandler { get; set; }

    // ===== 建構函式 =====

    /// <summary>
    /// 建構函式 - 新增隧道
    /// </summary>
    public PropertiesDialogViewModel()
    {
        // 初始化命令
        BrowsePrivateKeyCommand = ReactiveCommand.CreateFromTask(OnBrowsePrivateKey);
        AddRedirectionCommand = ReactiveCommand.CreateFromTask(OnAddRedirection);
        EditRedirectionCommand = ReactiveCommand.CreateFromTask(OnEditRedirection);
        DeleteRedirectionCommand = ReactiveCommand.Create(OnDeleteRedirection);
        OkCommand = ReactiveCommand.Create(OnOk);
        CancelCommand = ReactiveCommand.Create(OnCancel);

        // 載入 SSH Presets
        LoadSshPresets();
    }

    /// <summary>
    /// 建構函式 - 編輯現有隧道
    /// </summary>
    /// <param name="existing">現有的 SSH 隧道設定</param>
    public PropertiesDialogViewModel(SshTunnel existing) : this()
    {
        // 載入現有資料
        _name = existing.Name;
        _user = existing.Login;
        _host = existing.Host;
        _port = existing.Port;
        _privateKey = existing.PrivateKeyPath;
        _autoStart = existing.AutoStart;
        _autoRestart = existing.Restart;
        _notify = existing.Notify;
        _maxRestarts = existing.MaxRestarts;
        _isPresetMode = existing.Preset;

        // 載入端口轉發列表
        foreach (var redir in existing.PortRedirections)
        {
            _portRedirections.Add(new PortRedirectionViewModel(redir));
        }

        // 通知屬性變更
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(User));
        this.RaisePropertyChanged(nameof(Host));
        this.RaisePropertyChanged(nameof(Port));
        this.RaisePropertyChanged(nameof(PrivateKey));
        this.RaisePropertyChanged(nameof(AutoStart));
        this.RaisePropertyChanged(nameof(AutoRestart));
        this.RaisePropertyChanged(nameof(Notify));
        this.RaisePropertyChanged(nameof(MaxRestarts));

        // 根據 Preset 狀態設定欄位啟用狀態
        IsConnectionFieldsEnabled = !_isPresetMode;
    }

    // ===== 私有方法 =====

    /// <summary>
    /// 載入 SSH Presets（從 ~/.ssh/config）
    /// </summary>
    private void LoadSshPresets()
    {
        try
        {
            var sshConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh",
                "config"
            );

            if (!File.Exists(sshConfigPath))
            {
                Presets = new ObservableCollection<string> { "No presets available" };
                return;
            }

            var hosts = ParseSshConfig(sshConfigPath);
            if (hosts.Count == 0)
            {
                Presets = new ObservableCollection<string> { "No presets available" };
                return;
            }

            // 第一項為 "No preset selected"，後面是實際的 Host 名稱
            var presetList = new ObservableCollection<string> { "No preset selected" };
            foreach (var host in hosts)
            {
                presetList.Add(host);
            }
            Presets = presetList;
        }
        catch
        {
            // 解析失敗，使用預設值
            Presets = new ObservableCollection<string> { "No presets available" };
        }
    }

    /// <summary>
    /// 解析 SSH config 檔案，提取 Host 名稱
    /// 忽略萬用字元 Host (*)
    /// </summary>
    private System.Collections.Generic.List<string> ParseSshConfig(string configPath)
    {
        var hosts = new System.Collections.Generic.List<string>();

        foreach (var line in File.ReadAllLines(configPath))
        {
            var trimmed = line.Trim();

            // 尋找 "Host" 開頭的行
            if (trimmed.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                var hostName = trimmed.Substring(5).Trim();

                // 忽略萬用字元
                if (hostName != "*" && !hostName.Contains("*"))
                {
                    hosts.Add(hostName);
                }
            }
        }

        return hosts;
    }

    /// <summary>
    /// 當 Preset 選擇變更時
    /// </summary>
    private void OnPresetChanged()
    {
        // 如果選擇的是 "No preset selected" 或 "No presets available"
        if (_selectedPresetIndex == 0)
        {
            IsPresetMode = false;
            IsConnectionFieldsEnabled = true;
        }
        else
        {
            // 選擇了實際的 Preset
            IsPresetMode = true;
            IsConnectionFieldsEnabled = false;

            // TODO: 可以進一步解析 SSH config 來填入對應的設定
            // 目前只設定 Host 名稱
            var selectedHost = Presets[_selectedPresetIndex];
            Host = selectedHost;
        }
    }

    /// <summary>
    /// 瀏覽私鑰檔案
    /// </summary>
    private async Task OnBrowsePrivateKey()
    {
        if (OpenFilePickerHandler == null)
            return;

        var selectedFile = await OpenFilePickerHandler();
        if (!string.IsNullOrEmpty(selectedFile))
        {
            PrivateKey = selectedFile;
        }
    }

    /// <summary>
    /// 新增端口轉發
    /// </summary>
    private async Task OnAddRedirection()
    {
        if (OpenTunnelDialogHandler == null)
            return;

        var result = await OpenTunnelDialogHandler(null);
        if (result != null)
        {
            PortRedirections.Add(new PortRedirectionViewModel(result));
        }
    }

    /// <summary>
    /// 編輯端口轉發
    /// </summary>
    private async Task OnEditRedirection()
    {
        if (SelectedRedirection == null || OpenTunnelDialogHandler == null)
            return;

        var existing = SelectedRedirection.ToModel();
        var result = await OpenTunnelDialogHandler(existing);

        if (result != null)
        {
            // 更新現有項目
            SelectedRedirection.Type = result.Type;
            SelectedRedirection.Port = result.Port1;
            SelectedRedirection.ToHost = result.Host;
            SelectedRedirection.ToPort = result.Port2;
        }
    }

    /// <summary>
    /// 刪除端口轉發
    /// </summary>
    private void OnDeleteRedirection()
    {
        if (SelectedRedirection != null)
        {
            PortRedirections.Remove(SelectedRedirection);
        }
    }

    /// <summary>
    /// 確定按鈕處理
    /// </summary>
    private void OnOk()
    {
        // 驗證輸入
        if (!ValidateInput())
            return;

        // 建立結果
        Result = new SshTunnel
        {
            Name = _name,
            Host = _host,
            Port = _port,
            Login = _user,
            PrivateKeyPath = _privateKey,
            AutoStart = _autoStart,
            Restart = _autoRestart,
            Notify = _notify,
            MaxRestarts = _maxRestarts,
            Preset = _isPresetMode
        };

        // 轉換端口轉發列表
        foreach (var vm in _portRedirections)
        {
            Result.PortRedirections.Add(vm.ToModel());
        }

        DialogResult = true;
    }

    /// <summary>
    /// 取消按鈕處理
    /// </summary>
    private void OnCancel()
    {
        DialogResult = false;
        Result = null;
    }

    /// <summary>
    /// 驗證輸入資料
    /// </summary>
    private bool ValidateInput()
    {
        // 名稱不能為空
        if (string.IsNullOrWhiteSpace(_name))
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        // 如果不是 Preset 模式，需要驗證 Host 和 Port
        if (!_isPresetMode)
        {
            if (string.IsNullOrWhiteSpace(_host))
            {
                // TODO: 顯示錯誤訊息
                return false;
            }

            if (!int.TryParse(_port, out int portNum) || portNum <= 0 || portNum > 65535)
            {
                // TODO: 顯示錯誤訊息
                return false;
            }
        }

        // 驗證 MaxRestarts 是有效數字
        if (!int.TryParse(_maxRestarts, out int maxRestarts) || maxRestarts < 0)
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        return true;
    }
}

/// <summary>
/// PortRedirection 的 ViewModel 包裝
/// 用於 DataGrid 綁定
/// </summary>
public class PortRedirectionViewModel : ViewModelBase
{
    private string _type = "local";
    public string Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    private string _port = "";
    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private string _toHost = "";
    public string ToHost
    {
        get => _toHost;
        set => this.RaiseAndSetIfChanged(ref _toHost, value);
    }

    private string _toPort = "";
    public string ToPort
    {
        get => _toPort;
        set => this.RaiseAndSetIfChanged(ref _toPort, value);
    }

    public PortRedirectionViewModel()
    {
    }

    public PortRedirectionViewModel(PortRedirection model)
    {
        _type = model.Type;
        _port = model.Port1;
        _toHost = model.Host;
        _toPort = model.Port2;
    }

    public PortRedirection ToModel()
    {
        return new PortRedirection
        {
            Type = _type,
            Port1 = _port,
            Host = _toHost,
            Port2 = _toPort
        };
    }
}
