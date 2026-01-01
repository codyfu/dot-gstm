using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DotGstm.Desktop.Models;
using DotGstm.Desktop.Utils;

namespace DotGstm.Desktop.Services;

/// <summary>
/// Corresponds to gSTM's global variable struct sshtunnel **gSTMtunnels.
/// Encapsulates ConcurrentDictionary and mutex to ensure all accesses are thread-safe.
/// 對應 gSTM 的全域變數 struct sshtunnel **gSTMtunnels。
/// 封裝 ConcurrentDictionary 和 mutex，確保所有存取都是 thread-safe。
/// </summary>
public class GstmTunnels
{
    private readonly ConcurrentDictionary<string, SshTunnel> _tunnels = new();
    private readonly object _lock = new object();

    // === Basic operations ===
    // === 基本操作 ===

    /// <summary>
    /// Get tunnel (corresponds to gSTMtunnels[id])
    /// 取得 tunnel（對應 gSTMtunnels[id]）
    /// </summary>
    public bool TryGet(string name, out SshTunnel? tunnel)
    {
        return _tunnels.TryGetValue(name, out tunnel);
    }

    /// <summary>
    /// Set/add tunnel (corresponds to gSTMtunnels[id] = ...)
    /// 設定/新增 tunnel（對應 gSTMtunnels[id] = ...）
    /// </summary>
    public void Set(string name, SshTunnel tunnel)
    {
        DebugLogger.Log($"[GstmTunnels] 🔒 LOCK: Set('{name}') - Thread {Environment.CurrentManagedThreadId}");
        lock (_lock)
        {
            _tunnels[name] = tunnel;
            DebugLogger.Log($"[GstmTunnels] ✓ Set('{name}') completed");
        }
        DebugLogger.Log($"[GstmTunnels] 🔓 UNLOCK: Set('{name}') - Thread {Environment.CurrentManagedThreadId}");
    }

    /// <summary>
    /// Remove tunnel
    /// 移除 tunnel
    /// </summary>
    public bool Remove(string name)
    {
        lock (_lock)
        {
            return _tunnels.TryRemove(name, out _);
        }
    }

    /// <summary>
    /// Clear all tunnels
    /// 清空所有 tunnels
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tunnels.Clear();
        }
    }

    // === Batch operations ===
    // === 批次操作 ===

    /// <summary>
    /// Batch set tunnels (used for LoadTunnelsAsync)
    /// Atomic operation: clear first, then batch add
    /// 批次設定 tunnels（用於 LoadTunnelsAsync）
    /// 原子操作：先清空再批次加入
    /// </summary>
    public void SetBatch(IEnumerable<SshTunnel> tunnels)
    {
        lock (_lock)
        {
            _tunnels.Clear();
            foreach (var tunnel in tunnels)
            {
                _tunnels[tunnel.Name] = tunnel;
            }
        }
    }

    /// <summary>
    /// Get snapshot of all tunnels (for iteration)
    /// 取得所有 tunnel 的快照（用於迭代）
    /// </summary>
    public List<SshTunnel> GetSnapshot()
    {
        lock (_lock)
        {
            return _tunnels.Values.ToList();
        }
    }

    /// <summary>
    /// Get all tunnel names (for NameDialog)
    /// 取得所有 tunnel 名稱（用於 NameDialog）
    /// </summary>
    public IEnumerable<string> GetNames()
    {
        return _tunnels.Keys;
    }

    // === State operations (require lock) ===
    // === 狀態操作（需要 lock）===

    /// <summary>
    /// Read tunnel's Active state
    /// 讀取 tunnel 的 Active 狀態
    /// </summary>
    public bool GetActive(string name)
    {
        lock (_lock)
        {
            if (_tunnels.TryGetValue(name, out var tunnel))
            {
                return tunnel.Active;
            }
            return false;
        }
    }

    /// <summary>
    /// Set tunnel's Active state
    /// 設定 tunnel 的 Active 狀態
    /// </summary>
    public void SetActive(string name, bool active)
    {
        DebugLogger.Log($"[GstmTunnels] 🔒 LOCK: SetActive('{name}', {active}) - Thread {Environment.CurrentManagedThreadId}");
        lock (_lock)
        {
            if (_tunnels.TryGetValue(name, out var tunnel))
            {
                tunnel.Active = active;
                DebugLogger.Log($"[GstmTunnels] ✓ SetActive('{name}') = {active}");
            }
        }
        DebugLogger.Log($"[GstmTunnels] 🔓 UNLOCK: SetActive('{name}') - Thread {Environment.CurrentManagedThreadId}");
    }

    /// <summary>
    /// Read tunnel's SshPid
    /// 讀取 tunnel 的 SshPid
    /// </summary>
    public int GetSshPid(string name)
    {
        lock (_lock)
        {
            if (_tunnels.TryGetValue(name, out var tunnel))
            {
                return tunnel.SshPid;
            }
            return 0;
        }
    }

    /// <summary>
    /// Set tunnel's SshPid
    /// 設定 tunnel 的 SshPid
    /// </summary>
    public void SetSshPid(string name, int pid)
    {
        DebugLogger.Log($"[GstmTunnels] 🔒 LOCK: SetSshPid('{name}', {pid}) - Thread {Environment.CurrentManagedThreadId}");
        lock (_lock)
        {
            if (_tunnels.TryGetValue(name, out var tunnel))
            {
                tunnel.SshPid = pid;
                DebugLogger.Log($"[GstmTunnels] ✓ SetSshPid('{name}') = {pid}");
            }
        }
        DebugLogger.Log($"[GstmTunnels] 🔓 UNLOCK: SetSshPid('{name}') - Thread {Environment.CurrentManagedThreadId}");
    }

    // === Atomic operation support ===
    // === 提供原子操作支援 ===

    /// <summary>
    /// Execute operation that requires atomicity (e.g., check-then-act)
    /// 執行需要原子性的操作（例如 check-then-act）
    ///
    /// Usage | 使用方式:
    /// _gstmTunnels.ExecuteAtomic(() => {
    ///     if (_gstmTunnels.TryGet(name, out var tunnel)) {
    ///         if (!tunnel.Active) {
    ///             tunnel.Active = true;
    ///             return true;
    ///         }
    ///     }
    ///     return false;
    /// });
    /// </summary>
    public void ExecuteAtomic(System.Action action)
    {
        lock (_lock)
        {
            action();
        }
    }

    /// <summary>
    /// Execute atomic operation with return value
    /// 執行需要原子性且有返回值的操作
    /// </summary>
    public T ExecuteAtomic<T>(System.Func<T> func)
    {
        DebugLogger.Log($"[GstmTunnels] 🔒 LOCK: ExecuteAtomic<{typeof(T).Name}>() - Thread {Environment.CurrentManagedThreadId}");
        lock (_lock)
        {
            var result = func();
            DebugLogger.Log($"[GstmTunnels] ✓ ExecuteAtomic completed, result = {result}");
            return result;
        }
        // Note: UNLOCK happens automatically when lock block exits
        // 注意：UNLOCK 會在 lock 區塊結束時自動發生
    }
}
