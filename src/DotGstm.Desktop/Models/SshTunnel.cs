using System.Collections.ObjectModel;

namespace DotGstm.Desktop.Models;

/// <summary>
/// Corresponds to gSTM's struct sshtunnel
/// SSH tunnel configuration
/// 對應 gSTM 的 struct sshtunnel
/// SSH 隧道設定
/// </summary>
public class SshTunnel
{
    // ===== Basic settings =====
    // ===== 基本設定 =====

    /// <summary>
    /// Tunnel name
    /// 隧道名稱
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// SSH host address
    /// SSH 主機位址
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// SSH port (default: 22)
    /// SSH 端口（預設 22）
    /// </summary>
    public string Port { get; set; } = "22";

    /// <summary>
    /// SSH username
    /// SSH 使用者名稱
    /// </summary>
    public string Login { get; set; } = "";

    /// <summary>
    /// Private key file path (optional, for public key authentication)
    /// 私鑰檔案路徑（選用，用於公鑰認證）
    /// </summary>
    public string PrivateKeyPath { get; set; } = "";

    // ===== Options =====
    // ===== 選項 =====

    /// <summary>
    /// Whether to auto-start this tunnel on program startup
    /// 是否在程式啟動時自動啟動此隧道
    /// </summary>
    public bool AutoStart { get; set; }

    /// <summary>
    /// Whether to auto-reconnect on connection failure
    /// 連線失敗時是否自動重連
    /// </summary>
    public bool Restart { get; set; }

    /// <summary>
    /// Whether to show notification on reconnection (default: true)
    /// 重連時是否顯示通知（預設為 true）
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Maximum reconnection attempts (default: "9")
    /// 最大重連次數（預設為 "9"）
    /// </summary>
    public string MaxRestarts { get; set; } = "9";

    /// <summary>
    /// Whether to use SSH Preset (read from ~/.ssh/config)
    /// 是否使用 SSH Preset（從 ~/.ssh/config 讀取）
    /// </summary>
    public bool Preset { get; set; }

    // ===== Port forwarding =====
    // ===== 端口轉發 =====

    /// <summary>
    /// Port forwarding rules list
    /// 端口轉發規則列表
    /// </summary>
    public ObservableCollection<PortRedirection> PortRedirections { get; set; } = new();

    // ===== Runtime state (not saved to file) =====
    // ===== 運行時狀態（不儲存至檔案） =====

    /// <summary>
    /// Whether tunnel is running
    /// 隧道是否正在運行
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// SSH process PID
    /// SSH 進程 PID
    /// </summary>
    public int SshPid { get; set; }

    // ===== File information =====
    // ===== 檔案資訊 =====

    /// <summary>
    /// Configuration file path (e.g., ~/.config/gSTM/MyTunnel.XXXXXX.gstm)
    /// 組態檔案路徑（例如：~/.config/gSTM/MyTunnel.XXXXXX.gstm）
    /// </summary>
    public string? FileName { get; set; }
}
