using System;

namespace DotGstm.Desktop.Utils;

/// <summary>
/// Global debug logging control.
/// Enabled by --debug command line parameter.
/// 全域 debug 日誌控制。
/// 透過 --debug 命令列參數啟用。
/// </summary>
public static class DebugLogger
{
    /// <summary>
    /// Debug output enabled flag
    /// </summary>
    public static bool Enabled { get; set; } = false;

    /// <summary>
    /// Print debug message (only if debug enabled)
    /// 打印 debug 訊息（僅在 debug 啟用時）
    /// </summary>
    public static void Log(string message)
    {
        if (Enabled)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Print warning message (always shown)
    /// 打印警告訊息（總是顯示）
    /// </summary>
    public static void Warning(string message)
    {
        Console.Error.WriteLine($"** WARNING: {message}");
    }

    /// <summary>
    /// Print error message (always shown)
    /// 打印錯誤訊息（總是顯示）
    /// </summary>
    public static void Error(string message)
    {
        Console.Error.WriteLine($"** ERROR: {message}");
    }
}
