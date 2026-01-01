using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.Utils;

namespace DotGstm.Desktop.Services;

/// <summary>
/// Configuration file management service (follows gSTM conffile.c implementation)
/// 設定檔管理服務（遵循 gSTM conffile.c 的實作邏輯）
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;

    /// <summary>
    /// Create ConfigService instance
    /// 建立 ConfigService 實例
    /// </summary>
    /// <param name="configDirectory">
    /// Configuration directory path, uses default path if null
    /// 設定檔目錄路徑，如果為 null 則使用預設路徑
    /// </param>
    public ConfigService(string? configDirectory = null)
    {
        // Follow gSTM's config file path: ~/.config/gSTM
        // Reference: gSTM main.c:40-70
        // 遵循 gSTM 的設定檔路徑：~/.config/gSTM
        // 參考 gSTM main.c:40-70
        if (configDirectory == null)
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                configHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config"
                );
            }
            _configDirectory = Path.Combine(configHome, "gSTM");
        }
        else
        {
            _configDirectory = configDirectory;
        }

        // Ensure directory exists
        // 確保目錄存在
        Directory.CreateDirectory(_configDirectory);
        DebugLogger.Log($"[ConfigService] Config directory: {_configDirectory}");
    }

    /// <summary>
    /// Load all tunnel configurations
    /// Corresponds to conffile.c:gstm_readfiles()
    /// 讀取所有隧道設定
    /// 對應 conffile.c:gstm_readfiles()
    /// </summary>
    /// <returns>
    /// List of tunnels
    /// 隧道列表
    /// </returns>
    public async Task<List<SshTunnel>> LoadTunnelsAsync()
    {
        var tunnels = new List<SshTunnel>();

        try
        {
            // Scan *.gstm files (conffile.c:73-88)
            // 掃描 *.gstm 檔案（conffile.c:73-88）
            var gstmFiles = Directory.EnumerateFiles(_configDirectory, "*.gstm");

            foreach (var filePath in gstmFiles)
            {
                try
                {
                    var tunnel = await LoadTunnelFromFileAsync(filePath);
                    if (tunnel != null)
                    {
                        tunnel.FileName = Path.GetFileName(filePath);
                        tunnels.Add(tunnel);
                        DebugLogger.Log($"[ConfigService] Loaded tunnel: {tunnel.Name} ({tunnel.FileName})");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"[ConfigService] Failed to load file {filePath}: {ex.Message}");
                }
            }

            DebugLogger.Log($"[ConfigService] Loaded {tunnels.Count} tunnels total");
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[ConfigService] Failed to read config directory: {ex.Message}");
        }

        return tunnels;
    }

    /// <summary>
    /// Load single tunnel from XML file
    /// Corresponds to conffile.c:gstm_file2tunnel()
    /// 從 XML 檔案載入單個隧道
    /// 對應 conffile.c:gstm_file2tunnel()
    /// </summary>
    private async Task<SshTunnel?> LoadTunnelFromFileAsync(string filePath)
    {
        try
        {
            // Read XML file
            // 讀取 XML 檔案
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var root = doc.Element("sshtunnel");

            if (root == null)
            {
                DebugLogger.Error($"[ConfigService] Invalid XML format: {filePath}");
                return null;
            }

            // Parse tunnel data (conffile.c:120-180)
            // 解析隧道資料（conffile.c:120-180）
            var tunnel = new SshTunnel
            {
                Name = root.Element("name")?.Value ?? "",
                Host = root.Element("host")?.Value ?? "",
                Port = root.Element("port")?.Value ?? "22",
                Login = root.Element("login")?.Value ?? "",
                PrivateKeyPath = root.Element("privkey")?.Value ?? "",
                AutoStart = ParseBool(root.Element("autostart")?.Value),
                Restart = ParseBool(root.Element("restart")?.Value),
                Notify = ParseBool(root.Element("notify")?.Value),
                MaxRestarts = root.Element("maxrestarts")?.Value ?? "9",
                Preset = ParseBool(root.Element("preset")?.Value),
                PortRedirections = new ObservableCollection<PortRedirection>()
            };

            // Parse port redirections (conffile.c:145-165)
            // 解析端口轉發（conffile.c:145-165）
            var redirections = root.Elements("tunnel");
            foreach (var redir in redirections)
            {
                var portRedir = new PortRedirection
                {
                    Type = redir.Element("type")?.Value ?? "local",
                    Port1 = redir.Element("port1")?.Value ?? "",
                    Host = redir.Element("host")?.Value ?? "",
                    Port2 = redir.Element("port2")?.Value ?? ""
                };
                tunnel.PortRedirections.Add(portRedir);
            }

            return tunnel;
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[ConfigService] Failed to parse XML {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save tunnel to XML file
    /// Corresponds to conffile.c:gstm_tunnel2file()
    /// 儲存隧道到 XML 檔案
    /// 對應 conffile.c:gstm_tunnel2file()
    /// </summary>
    public async Task<bool> SaveTunnelAsync(SshTunnel tunnel)
    {
        try
        {
            DebugLogger.Log($"[ConfigService] SaveTunnelAsync 開始: Name={tunnel.Name}");
            DebugLogger.Log($"[ConfigService] tunnel.FileName={tunnel.FileName ?? "null"}");
            DebugLogger.Log($"[ConfigService] tunnel.PortRedirections.Count={tunnel.PortRedirections.Count}");

            // Determine file name (conffile.c:212-220)
            // 確定檔案名稱（conffile.c:212-220）
            var fileName = tunnel.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                DebugLogger.Log($"[ConfigService] FileName 為空，生成新檔名");
                // Generate unique file name like gSTM's mkstemp (conffile.c:67-86)
                // 產生唯一檔名，類似 gSTM 的 mkstemp（conffile.c:67-86）
                var baseName = SanitizeFileName(tunnel.Name);
                var randomId = Path.GetRandomFileName().Replace(".", "").Substring(0, 6);
                fileName = $"{baseName}.{randomId}.gstm";
                tunnel.FileName = fileName;
                DebugLogger.Log($"[ConfigService] 新檔名: {fileName}");
            }
            else
            {
                DebugLogger.Log($"[ConfigService] 使用現有檔名: {fileName}");
            }

            var filePath = Path.Combine(_configDirectory, fileName);
            DebugLogger.Log($"[ConfigService] 完整路徑: {filePath}");

            // Create XML document (conffile.c:224-270)
            // 建立 XML 文件（conffile.c:224-270）
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("sshtunnel",
                    new XElement("name", tunnel.Name),
                    new XElement("host", tunnel.Host),
                    new XElement("port", tunnel.Port),
                    new XElement("login", tunnel.Login),
                    new XElement("privkey", tunnel.PrivateKeyPath),
                    new XElement("autostart", tunnel.AutoStart ? "1" : "0"),
                    new XElement("restart", tunnel.Restart ? "1" : "0"),
                    new XElement("notify", tunnel.Notify ? "1" : "0"),
                    new XElement("maxrestarts", tunnel.MaxRestarts),
                    new XElement("preset", tunnel.Preset ? "1" : "0")
                )
            );

            var root = doc.Root;
            if (root == null)
            {
                DebugLogger.Error("[ConfigService] Failed to create XML root element");
                return false;
            }

            // Add port redirections (conffile.c:247-265)
            // 加入端口轉發（conffile.c:247-265）
            DebugLogger.Log($"[ConfigService] 開始寫入 {tunnel.PortRedirections.Count} 個 port redirections");
            foreach (var redir in tunnel.PortRedirections)
            {
                DebugLogger.Log($"[ConfigService] 寫入轉發: Type={redir.Type}, Port1={redir.Port1}, Host={redir.Host}, Port2={redir.Port2}");
                var redirElement = new XElement("tunnel",
                    new XElement("type", redir.Type),
                    new XElement("port1", redir.Port1),
                    new XElement("host", redir.Host),
                    new XElement("port2", redir.Port2)
                );
                root.Add(redirElement);
            }

            // Save file
            // 儲存檔案
            DebugLogger.Log($"[ConfigService] 開始寫入檔案: {filePath}");
            await Task.Run(() => doc.Save(filePath));
            DebugLogger.Log($"[ConfigService] Saved tunnel: {tunnel.Name} → {filePath}");

            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[ConfigService] Failed to save tunnel {tunnel.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete tunnel configuration file
    /// Corresponds to conffile.c:gstm_tunnel_del()
    /// 刪除隧道設定檔
    /// 對應 conffile.c:gstm_tunnel_del()
    /// </summary>
    public async Task<bool> DeleteTunnelAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, fileName);

            if (!File.Exists(filePath))
            {
                DebugLogger.Warning($"File does not exist: {filePath}");
                return false;
            }

            // Delete file (conffile.c:280-290)
            // 刪除檔案（conffile.c:280-290）
            await Task.Run(() => File.Delete(filePath));
            DebugLogger.Log($"[ConfigService] Deleted tunnel config: {fileName}");

            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[ConfigService] Failed to delete tunnel {fileName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if tunnel name already exists
    /// Corresponds to conffile.c:gstm_tunnel_name_exists()
    /// 檢查隧道名稱是否已存在
    /// 對應 conffile.c:gstm_tunnel_name_exists()
    /// </summary>
    public async Task<bool> TunnelNameExistsAsync(string name, string? excludeFileName = null)
    {
        try
        {
            var tunnels = await LoadTunnelsAsync();

            foreach (var tunnel in tunnels)
            {
                // Exclude specified file (used when editing existing tunnel)
                // 排除指定檔案（編輯現有隧道時使用）
                if (excludeFileName != null && tunnel.FileName == excludeFileName)
                    continue;

                if (tunnel.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get configuration directory path
    /// 取得設定目錄路徑
    /// </summary>
    public string GetConfigDirectory() => _configDirectory;

    /// <summary>
    /// Sanitize file name (remove illegal characters)
    /// 清理檔案名稱（移除不合法字元）
    /// </summary>
    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "tunnel" : sanitized;
    }

    /// <summary>
    /// Parse boolean value (supports both "1"/"0" and "true"/"false")
    /// 解析布林值（支援 "1"/"0" 和 "true"/"false" 兩種格式）
    /// </summary>
    private bool ParseBool(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Support gSTM format: "1" = true, "0" = false (conffile.c:546-548)
        // 支援 gSTM 格式：conffile.c:546-548
        if (value == "1")
            return true;
        if (value == "0")
            return false;

        // Also support .NET format: "true"/"false"
        // 也支援 .NET 格式
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
