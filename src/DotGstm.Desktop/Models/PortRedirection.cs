namespace DotGstm.Desktop.Models;

/// <summary>
/// Corresponds to gSTM's struct portredir
/// Port forwarding configuration
/// 對應 gSTM 的 struct portredir
/// 端口轉發設定
/// </summary>
public class PortRedirection
{
    /// <summary>
    /// Forwarding type: "local", "remote", "dynamic"
    /// 轉發類型："local"、"remote"、"dynamic"
    /// </summary>
    public string Type { get; set; } = "local";

    /// <summary>
    /// Bind port (Port1)
    /// - local: default is "127.0.0.1:"
    /// - remote: default is empty string
    /// - dynamic: only this field is used, Host and Port2 set to "n/a"
    /// 綁定端口（Port1）
    /// - local: 預設為 "127.0.0.1:"
    /// - remote: 預設為空字串
    /// - dynamic: 僅使用此欄位，Host 和 Port2 設為 "n/a"
    /// </summary>
    public string Port1 { get; set; } = "";

    /// <summary>
    /// Target host (To Host)
    /// - Set to "n/a" for dynamic type
    /// 目標主機（To Host）
    /// - dynamic 類型時設為 "n/a"
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// Target port (To Port / Port2)
    /// - Set to "n/a" for dynamic type
    /// 目標端口（To Port / Port2）
    /// - dynamic 類型時設為 "n/a"
    /// </summary>
    public string Port2 { get; set; } = "";
}
