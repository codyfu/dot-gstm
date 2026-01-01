using System;
using System.Reactive;
using DotGstm.Desktop.Models;
using ReactiveUI;

namespace DotGstm.Desktop.ViewModels;

/// <summary>
/// 端口轉發編輯對話框的 ViewModel
/// 對應 TunnelDialog.axaml
/// </summary>
public class TunnelDialogViewModel : ViewModelBase
{
    // ===== 屬性 =====

    private string _selectedType = "local";
    /// <summary>
    /// 轉發類型：local、remote、dynamic
    /// </summary>
    public string SelectedType
    {
        get => _selectedType;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedType, value);
            OnTypeChanged();
        }
    }

    private string _bindPort = "127.0.0.1:";
    /// <summary>
    /// [bind_address]:Port
    /// </summary>
    public string BindPort
    {
        get => _bindPort;
        set => this.RaiseAndSetIfChanged(ref _bindPort, value);
    }

    private string _toHost = "";
    /// <summary>
    /// To Host
    /// </summary>
    public string ToHost
    {
        get => _toHost;
        set => this.RaiseAndSetIfChanged(ref _toHost, value);
    }

    private string _toPort = "";
    /// <summary>
    /// To Port
    /// </summary>
    public string ToPort
    {
        get => _toPort;
        set => this.RaiseAndSetIfChanged(ref _toPort, value);
    }

    private bool _isToHostEnabled = true;
    /// <summary>
    /// To Host 欄位是否啟用
    /// </summary>
    public bool IsToHostEnabled
    {
        get => _isToHostEnabled;
        set => this.RaiseAndSetIfChanged(ref _isToHostEnabled, value);
    }

    private bool _isToPortEnabled = true;
    /// <summary>
    /// To Port 欄位是否啟用
    /// </summary>
    public bool IsToPortEnabled
    {
        get => _isToPortEnabled;
        set => this.RaiseAndSetIfChanged(ref _isToPortEnabled, value);
    }

    // ===== 對話框結果 =====

    /// <summary>
    /// 對話框是否以 OK 關閉
    /// </summary>
    public bool DialogResult { get; private set; }

    /// <summary>
    /// 編輯後的端口轉發設定
    /// </summary>
    public PortRedirection? Result { get; private set; }

    // ===== 命令 =====

    /// <summary>
    /// 確定命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> OkCommand { get; }

    /// <summary>
    /// 取消命令
    /// </summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    // ===== 建構函式 =====

    /// <summary>
    /// 建構函式 - 新增端口轉發
    /// </summary>
    public TunnelDialogViewModel()
    {
        // 初始化命令
        OkCommand = ReactiveCommand.Create(OnOk);
        CancelCommand = ReactiveCommand.Create(OnCancel);

        // 設定初始值（預設為 local）
        OnTypeChanged();
    }

    /// <summary>
    /// 建構函式 - 編輯現有端口轉發
    /// </summary>
    /// <param name="existing">現有的端口轉發設定</param>
    public TunnelDialogViewModel(PortRedirection existing) : this()
    {
        // 載入現有資料
        _selectedType = existing.Type.ToLower();
        _bindPort = existing.Port1;
        _toHost = existing.Host;
        _toPort = existing.Port2;

        // 通知屬性變更
        this.RaisePropertyChanged(nameof(SelectedType));
        this.RaisePropertyChanged(nameof(BindPort));
        this.RaisePropertyChanged(nameof(ToHost));
        this.RaisePropertyChanged(nameof(ToPort));

        // 根據類型設定欄位狀態（不設定預設值，保留載入的資料）
        OnTypeChanged(setDefaults: false);
    }

    // ===== 私有方法 =====

    /// <summary>
    /// 當 Type 變更時的處理邏輯
    /// 對應 TunnelDialog.axaml.cs 的 UpdateFieldsBasedOnType()
    /// </summary>
    /// <param name="setDefaults">是否設定預設值（新增模式用，編輯模式不應設定）</param>
    private void OnTypeChanged(bool setDefaults = true)
    {
        switch (_selectedType.ToLower())
        {
            case "dynamic":
                // dynamic 類型：Host 和 Port 設為 "n/a" 並禁用
                if (setDefaults)
                {
                    ToHost = "n/a";
                    ToPort = "n/a";
                }
                IsToHostEnabled = false;
                IsToPortEnabled = false;
                break;

            case "local":
                // local 類型：啟用所有欄位
                IsToHostEnabled = true;
                IsToPortEnabled = true;

                // 如果目前是 "n/a"，清空欄位
                if (ToHost == "n/a")
                    ToHost = "";
                if (ToPort == "n/a")
                    ToPort = "";

                // 設定預設綁定位址（僅在新增模式）
                if (setDefaults)
                    BindPort = "127.0.0.1:";
                break;

            case "remote":
                // remote 類型：啟用所有欄位
                IsToHostEnabled = true;
                IsToPortEnabled = true;

                // 如果目前是 "n/a"，清空欄位
                if (ToHost == "n/a")
                    ToHost = "";
                if (ToPort == "n/a")
                    ToPort = "";

                // 清空綁定位址（僅在新增模式）
                if (setDefaults)
                    BindPort = "";
                break;
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
        Result = new PortRedirection
        {
            Type = _selectedType.ToLower(),
            Port1 = _bindPort,
            Host = _toHost,
            Port2 = _toPort
        };

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
        // BindPort 不能為空
        if (string.IsNullOrWhiteSpace(_bindPort))
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        // dynamic 類型只需要 BindPort
        if (_selectedType.ToLower() == "dynamic")
            return true;

        // local 和 remote 需要 ToHost 和 ToPort
        if (string.IsNullOrWhiteSpace(_toHost))
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        if (string.IsNullOrWhiteSpace(_toPort))
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        // 驗證 ToPort 是有效的數字
        if (!int.TryParse(_toPort, out int port) || port <= 0 || port > 65535)
        {
            // TODO: 顯示錯誤訊息
            return false;
        }

        return true;
    }
}
