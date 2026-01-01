using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.Utils;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DotGstm.Desktop.Services;

/// <summary>
/// SSH tunnel management service (faithfully replicates gSTM fnssht.c implementation)
/// SSH 隧道管理服務（忠實複刻 gSTM fnssht.c 的實作邏輯）
/// </summary>
public class SshService
{
    // Dependency injection: GstmTunnels wrapper class
    // 依賴注入：GstmTunnels 封裝類別
    private readonly GstmTunnels _gstmTunnels;

    /// <summary>
    /// Constructor (dependency injection)
    /// 建構函式（依賴注入）
    /// </summary>
    /// <param name="gstmTunnels">
    /// GstmTunnels instance
    /// GstmTunnels 實例
    /// </param>
    public SshService(GstmTunnels gstmTunnels)
    {
        _gstmTunnels = gstmTunnels;
    }

    /// <summary>
    /// Start SSH tunnel
    /// Corresponds to gstm_ssht_starttunnel (fnssht.c:328)
    /// 啟動 SSH 隧道
    /// 對應 gstm_ssht_starttunnel (fnssht.c:328)
    /// </summary>
    /// <param name="tunnelName">
    /// Tunnel name
    /// 隧道名稱
    /// </param>
    public void StartTunnel(string tunnelName)
    {
        // Atomic operation: check-then-act
        // 原子操作：檢查後執行
        bool canStart = _gstmTunnels.ExecuteAtomic(() =>
        {
            if (!_gstmTunnels.TryGet(tunnelName, out var tunnel))
            {
                DebugLogger.Error($"Tunnel '{tunnelName}' does not exist");
                return false;
            }

            if (tunnel?.Active == true)
            {
                DebugLogger.Warning($"Tunnel '{tunnelName}' is already running");
                return false;
            }

            if (tunnel != null)
            {
                tunnel.Active = true;
            }
            return true;
        });

        if (!canStart) return;

        // Get tunnel reference
        // 取得 tunnel 參考
        if (!_gstmTunnels.TryGet(tunnelName, out var tunnelRef) || tunnelRef == null)
        {
            DebugLogger.Error($"StartTunnel('{tunnelName}') failed: tunnel not found");
            return;
        }

        // Create helper thread (corresponds to g_thread_new)
        // 建立 helper thread（對應 g_thread_new）
        var helperThread = new Thread(() => HelperThread(tunnelRef))
        {
            IsBackground = true,
            Name = $"SSH-{tunnelName}"
        };

        DebugLogger.Log($"[SshService] 🚀 StartTunnel('{tunnelName}') - Creating helper thread on Thread {Environment.CurrentManagedThreadId}");
        helperThread.Start();
        DebugLogger.Log($"[SshService] ✓ Helper thread started for '{tunnelName}' (ThreadId: {helperThread.ManagedThreadId})");
    }

    /// <summary>
    /// Helper thread for SSH tunnel process management
    /// Corresponds to gstm_ssht_helperthread (fnssht.c:35-136)
    /// SSH 隧道程序管理的 helper thread
    /// 對應 gstm_ssht_helperthread (fnssht.c:35-136)
    ///
    /// Flow | 流程:
    /// 1. Create SSH process | 建立 SSH 程序
    /// 2. Set tunnel.SshPid | 設定 tunnel.SshPid
    /// 3. Wait for process exit | 等待程序結束
    /// 4. Auto-restart if configured | 如果設定則自動重啟
    /// 5. Clean up and exit | 清理並結束
    /// </summary>
    /// <param name="tunnel">
    /// Tunnel configuration object
    /// 隧道設定物件
    /// </param>
    private void HelperThread(SshTunnel tunnel)
    {
        int numRestarts = 0;
        int maxRestarts = int.Parse(tunnel.MaxRestarts);
        int exitCode;

        DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🎯 Thread started (ThreadId: {Environment.CurrentManagedThreadId})");

        do
        {
            Process? process = null;
            StringBuilder stderrBuffer = new StringBuilder(); // Collect stderr (corresponds to fnssht.c:42-44)

            try
            {
                // Build SSH command (corresponds to gstm_ssht_craft_command)
                // 建構 SSH 指令（對應 gstm_ssht_craft_command）
                var args = BuildSshArguments(tunnel);
                DebugLogger.Log($"[HelperThread:{tunnel.Name}] 📝 SSH args: {string.Join(" ", args)}");

                // Create Process (corresponds to fork + execvp)
                // 建立 Process（對應 fork + execvp）
                process = CreateSshProcess(tunnel, args, stderrBuffer);

                DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🚀 Starting SSH process...");
                process.Start();

                // Save PID (corresponds to gSTMtunnels[id]->sshpid = pid)
                // 儲存 PID（對應 gSTMtunnels[id]->sshpid = pid）
                DebugLogger.Log($"[HelperThread:{tunnel.Name}] 💾 Saving PID={process.Id}");
                _gstmTunnels.SetSshPid(tunnel.Name, process.Id);

                DebugLogger.Log($"[HelperThread:{tunnel.Name}] ✓ SSH process started (PID={process.Id}, HasExited={process.HasExited})");

                // Start reading stderr (corresponds to fnssht.c:80-92)
                // 開始讀取 stderr（對應 fnssht.c:80-92）
                process.BeginErrorReadLine();

                DebugLogger.Log($"[HelperThread:{tunnel.Name}] ⏳ Waiting for SSH process to exit...");
                // Wait for SSH process to exit (corresponds to wait(&rv))
                // 等待 SSH process 結束（對應 wait(&rv)）
                process.WaitForExit();
                exitCode = process.ExitCode;

                // Log exit code (corresponds to gSTM fnssht.c:93-136)
                // 記錄退出碼（對應 gSTM fnssht.c:93-136）
                DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🛑 SSH process exited (ExitCode={exitCode})");

                // Clear PID
                // 清除 PID
                DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🧹 Clearing PID");
                _gstmTunnels.SetSshPid(tunnel.Name, 0);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[HelperThread:{tunnel.Name}] Exception: {ex.GetType().Name} - {ex.Message}");
                DebugLogger.Error($"[HelperThread:{tunnel.Name}] Stack trace: {ex.StackTrace}");
                exitCode = -1;
            }
            finally
            {
                if (process != null)
                {
                    DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🗑️ Disposing process object");
                    process.Dispose();
                }
            }

            // Wait 3 seconds (corresponds to fnssht.c:153)
            // 等待 3 秒（對應 fnssht.c:153）
            DebugLogger.Log($"[HelperThread:{tunnel.Name}] ⏸️ Waiting 3 seconds before checking restart...");
            Thread.Sleep(3000);

            // Auto-restart logic (corresponds to fnssht.c:101-155)
            // Auto-restart 邏輯（對應 fnssht.c:101-155）
            bool shouldRestart = false;

            DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🔄 Checking auto-restart (exitCode={exitCode}, restart={tunnel.Restart}, numRestarts={numRestarts}/{maxRestarts})");
            shouldRestart = _gstmTunnels.ExecuteAtomic(() =>
            {
                // Check if should restart (corresponds to fnssht.c:155)
                // numRestarts starts from 0, so condition is numRestarts < maxRestarts
                // 檢查是否應該重啟（對應 fnssht.c:155）
                // numRestarts 從 0 開始，所以條件是 numRestarts < maxRestarts
                if (exitCode != 0 && tunnel.Restart && numRestarts < maxRestarts && tunnel.SshPid == 0)
                {
                    numRestarts++;
                    DebugLogger.Log($"[HelperThread:{tunnel.Name}] ♻️ Auto-restart triggered ({numRestarts}/{maxRestarts})");

                    // Show notification (corresponds to fnssht.c:139-149)
                    // 顯示通知（對應 fnssht.c:139-149）
                    if (tunnel.Notify)
                    {
                        ShowErrorNotification(tunnel.Name, exitCode, stderrBuffer.ToString(), numRestarts, maxRestarts);
                    }

                    return true;
                }

                // Show final error if no restart (corresponds to fnssht.c:139-149)
                // 如果不重連則顯示最終錯誤（對應 fnssht.c:139-149）
                if (exitCode != 0 && (!tunnel.Restart || numRestarts >= maxRestarts))
                {
                    ShowErrorNotification(tunnel.Name, exitCode, stderrBuffer.ToString(), numRestarts, maxRestarts);
                }

                DebugLogger.Log($"[HelperThread:{tunnel.Name}] ⏹️ No restart needed");
                return false;
            });

            if (!shouldRestart)
                break;

        } while (true);

        // Thread ends, set Active = false (corresponds to fnssht.c:175-176)
        // Thread 結束，設定 Active = false（對應 fnssht.c:175-176）
        DebugLogger.Log($"[HelperThread:{tunnel.Name}] 🏁 Thread ending, setting Active=false");
        _gstmTunnels.SetActive(tunnel.Name, false);

        DebugLogger.Log($"[HelperThread:{tunnel.Name}] ✅ Thread completed (ThreadId: {Environment.CurrentManagedThreadId})");
    }

    /// <summary>
    /// Stop SSH tunnel
    /// Corresponds to gstm_ssht_stoptunnel (fnssht.c:349)
    /// 停止 SSH 隧道
    /// 對應 gstm_ssht_stoptunnel (fnssht.c:349)
    /// </summary>
    /// <param name="tunnelName">
    /// Tunnel name
    /// 隧道名稱
    /// </param>
    public void StopTunnel(string tunnelName)
    {
        DebugLogger.Log($"[SshService] 🛑 StopTunnel('{tunnelName}') called on Thread {Environment.CurrentManagedThreadId}");

        int pid = _gstmTunnels.GetSshPid(tunnelName);
        DebugLogger.Log($"[SshService] Current PID for '{tunnelName}': {pid}");

        if (pid == 0)
        {
            DebugLogger.Warning($"No running SSH process for tunnel '{tunnelName}'");
            return;
        }

        try
        {
            // Get Process and terminate (corresponds to kill(sshpid, SIGTERM))
            // 取得 Process 並終止（對應 kill(sshpid, SIGTERM)）
            var process = Process.GetProcessById(pid);

            DebugLogger.Log($"[SshService] 💀 Killing SSH process (PID={pid}, ProcessName={process.ProcessName}, HasExited={process.HasExited})");
            process.Kill(entireProcessTree: true);
            DebugLogger.Log($"[SshService] ✓ Process.Kill() completed");

            _gstmTunnels.SetSshPid(tunnelName, 0);
            DebugLogger.Log($"[SshService] ✓ StopTunnel('{tunnelName}') completed successfully");
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            // Process 已不存在
            DebugLogger.Warning($"Process PID={pid} no longer exists (already terminated)");
            _gstmTunnels.SetSshPid(tunnelName, 0);
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Failed to terminate SSH process: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if tunnel is active
    /// 檢查隧道是否在執行中
    /// </summary>
    public bool IsTunnelActive(string tunnelName)
    {
        return _gstmTunnels.GetActive(tunnelName);
    }

    /// <summary>
    /// Build SSH command arguments
    /// Corresponds to gstm_ssht_craft_command (fnssht.c:258-325)
    /// 建構 SSH 指令參數
    /// 對應 gstm_ssht_craft_command（fnssht.c:258-325）
    /// </summary>
    private List<string> BuildSshArguments(SshTunnel tunnel)
    {
        var args = new List<string>();

        // HOST (fnssht.c:269)
        args.Add(tunnel.Host);

        // -nN (fnssht.c:270)
        args.Add("-nN");

        // If not Preset mode, add detailed settings (fnssht.c:272-292)
        // 如果不是 Preset 模式，加入詳細設定 (fnssht.c:272-292)
        if (!tunnel.Preset)
        {
            // -p PORT (fnssht.c:273-276)
            if (!string.IsNullOrWhiteSpace(tunnel.Port) && tunnel.Port.Length > 1)
            {
                args.Add("-p");
                args.Add(tunnel.Port);
            }

            // -i PRIVKEY (fnssht.c:278-281)
            if (!string.IsNullOrWhiteSpace(tunnel.PrivateKeyPath) && tunnel.PrivateKeyPath.Length > 1)
            {
                args.Add("-i");
                args.Add(tunnel.PrivateKeyPath);
            }

            // -l LOGIN (fnssht.c:283-286)
            if (!string.IsNullOrWhiteSpace(tunnel.Login) && tunnel.Login.Length > 1)
            {
                args.Add("-l");
                args.Add(tunnel.Login);
            }

            // Connection options (fnssht.c:288-291)
            args.Add("-o");
            args.Add("ConnectTimeout=5");
            args.Add("-o");
            args.Add("NumberOfPasswordPrompts=1");
        }

        // Port Forwarding (fnssht.c:294-316)
        foreach (var redir in tunnel.PortRedirections)
        {
            var type = redir.Type.ToLower();

            if (type == "local")
            {
                // -L port1:host:port2 (fnssht.c:304-306)
                args.Add($"-L{redir.Port1}:{redir.Host}:{redir.Port2}");
            }
            else if (type == "remote")
            {
                // -R port1:host:port2 (fnssht.c:307-309)
                args.Add($"-R{redir.Port1}:{redir.Host}:{redir.Port2}");
            }
            else if (type == "dynamic")
            {
                // -D port1 (fnssht.c:310-312)
                args.Add($"-D{redir.Port1}");
            }
        }

        return args;
    }

    /// <summary>
    /// Create SSH Process
    /// 建立 SSH Process
    /// </summary>
    private Process CreateSshProcess(SshTunnel tunnel, List<string> args, StringBuilder stderrBuffer)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Add arguments
        // 加入參數
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        // Setup environment variables (SSH_ASKPASS)
        // 設定環境變數（SSH_ASKPASS）
        SetupEnvironment(process.StartInfo);

        // Register stderr handler (corresponds to fnssht.c:80-92)
        // 註冊 stderr 處理（對應 fnssht.c:80-92）
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                DebugLogger.Log($"[SSH:{tunnel.Name}] {e.Data}");
                // Collect stderr output (corresponds to fnssht.c:82-86)
                // 收集 stderr 輸出（對應 fnssht.c:82-86）
                stderrBuffer.AppendLine(e.Data);
            }
        };

        process.EnableRaisingEvents = true;

        return process;
    }

    /// <summary>
    /// Show error notification (corresponds to gstm_interface_error via gdk_threads_add_idle)
    /// 顯示錯誤通知（對應通過 gdk_threads_add_idle 調用 gstm_interface_error）
    /// </summary>
    /// <param name="tunnelName">Tunnel name</param>
    /// <param name="exitCode">Process exit code</param>
    /// <param name="stderrOutput">SSH stderr output</param>
    /// <param name="numRestarts">Current restart count</param>
    /// <param name="maxRestarts">Maximum restart count</param>
    private void ShowErrorNotification(string tunnelName, int exitCode, string stderrOutput, int numRestarts, int maxRestarts)
    {
        // Dispatch to UI thread (corresponds to gdk_threads_add_idle in fnssht.c:147)
        // 調度到 UI 線程（對應 fnssht.c:147 的 gdk_threads_add_idle）
        Dispatcher.UIThread.Post(async () =>
        {
            var loc = LocalizationService.Instance;
            StringBuilder message = new StringBuilder();

            // Build main error message (corresponds to fnssht.c:113-132)
            // 構建主要錯誤訊息（對應 fnssht.c:113-132）
            if (!string.IsNullOrEmpty(stderrOutput) && exitCode != 15)
            {
                // Normal error with stderr output
                // 一般錯誤有 stderr 輸出
                message.AppendLine(loc.GetString("Tunnel_Stopped", tunnelName));
                message.AppendLine();
                message.Append(stderrOutput);
            }
            else if (exitCode == 9)
            {
                // SIGKILL (kill -9)
                message.AppendLine(loc.GetString("Tunnel_Stopped", tunnelName));
                message.Append(loc.GetString("Tunnel_ProcessKilled"));
            }
            else if (exitCode == 15)
            {
                // SIGTERM (kill -15)
                message.AppendLine(loc.GetString("Tunnel_Terminated", tunnelName));
                if (!string.IsNullOrEmpty(stderrOutput))
                {
                    message.AppendLine();
                    message.Append(stderrOutput);
                }
            }
            else
            {
                // Unknown error code
                // 未知錯誤碼
                message.AppendLine(loc.GetString("Tunnel_Stopped", tunnelName));
                message.AppendLine(loc.GetString("Tunnel_UnknownError", exitCode));
                if (!string.IsNullOrEmpty(stderrOutput))
                {
                    message.AppendLine();
                    message.Append(stderrOutput);
                }
            }

            // Add restart notification (corresponds to fnssht.c:101-108)
            // 添加重連通知（對應 fnssht.c:101-108）
            if (numRestarts > 0 && numRestarts <= maxRestarts)
            {
                message.AppendLine();
                message.Append(loc.GetString("Tunnel_Restarting", numRestarts, maxRestarts));
            }

            // Show message box
            // 顯示訊息框
            DebugLogger.Log($"[SshService] 💬 Showing error notification: {message.ToString().Replace("\n", "\\n")}");
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                loc.GetString("Error_Title"),
                message.ToString(),
                ButtonEnum.Ok);

            await msgBox.ShowAsync();
        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Setup environment variables
    /// Corresponds to fnssht.c:62
    /// 設定環境變數
    /// 對應 fnssht.c:62
    /// </summary>
    private void SetupEnvironment(ProcessStartInfo startInfo)
    {
        // Set SSH_ASKPASS to daskpass
        // First try to find in same directory (release location)
        // SSH_ASKPASS 設定為 daskpass
        // 首先嘗試在同目錄尋找（發佈後的位置）
        var askpassPath = Path.Combine(
            AppContext.BaseDirectory,
            "daskpass" + (OperatingSystem.IsWindows() ? ".exe" : "")
        );

        // If not found, try development environment relative path
        // 如果找不到，嘗試開發環境的相對路徑
        if (!File.Exists(askpassPath))
        {
            // From dstm/bin/Debug/net10.0 to daskpass/bin/Debug/net10.0
            // 從 dstm/bin/Debug/net10.0 到 daskpass/bin/Debug/net10.0
            var devPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "DotGstm.Askpass", "bin", "Debug", "net10.0",
                "daskpass" + (OperatingSystem.IsWindows() ? ".exe" : "")
            );
            askpassPath = Path.GetFullPath(devPath);
        }

        if (File.Exists(askpassPath))
        {
            startInfo.Environment["SSH_ASKPASS"] = askpassPath;

            // SSH_ASKPASS_REQUIRE=force forces SSH_ASKPASS usage (no DISPLAY needed)
            // However, it's only supported in OpenSSH 8.4+ (2020-09-27)
            // macOS 10.15, 11 have older OpenSSH versions that don't support this
            // So we skip it on macOS to ensure compatibility
            // SSH_ASKPASS_REQUIRE=force 強制使用 SSH_ASKPASS（不需要 DISPLAY）
            // 但僅在 OpenSSH 8.4+ (2020-09-27) 支援
            // macOS 10.15, 11 內建的 OpenSSH 版本較舊，不支援此參數
            // 因此在 macOS 上跳過此設定以確保兼容性
            if (!OperatingSystem.IsMacOS())
            {
                startInfo.Environment["SSH_ASKPASS_REQUIRE"] = "force";
                DebugLogger.Log($"[SshService] SSH_ASKPASS_REQUIRE set to force");
            }
            else
            {
                DebugLogger.Log($"[SshService] macOS detected - SSH_ASKPASS_REQUIRE skipped for compatibility");
            }

            DebugLogger.Log($"[SshService] SSH_ASKPASS set to: {askpassPath}");
        }
        else
        {
            DebugLogger.Warning($"Askpass program not found at {askpassPath}");
        }
    }
}
