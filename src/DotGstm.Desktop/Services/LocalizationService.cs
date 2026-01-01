using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Reflection;
using DotGstm.Desktop.Utils;

namespace DotGstm.Desktop.Services;

/// <summary>
/// 多國語系服務
/// </summary>
public class LocalizationService
{
    private static LocalizationService? _instance;
    private ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public event EventHandler? LanguageChanged;

    private LocalizationService()
    {
        // 初始化 ResourceManager
        _resourceManager = new ResourceManager(
            "DotGstm.Desktop.Resources.Strings",
            Assembly.GetExecutingAssembly());

        // 自動偵測系統語言 (matches original gSTM behavior using gettext)
        // 自動偵測系統語言（符合原始 gSTM 使用 gettext 的行為）
        _currentCulture = DetectSystemLanguage();
        DebugLogger.Log($"[LocalizationService] Initialized with language: {_currentCulture.Name} ({_currentCulture.DisplayName})");
    }

    /// <summary>
    /// Detect system language and map to supported culture
    /// 偵測系統語言並映射到支援的文化
    /// </summary>
    private CultureInfo DetectSystemLanguage()
    {
        try
        {
            // Get system culture (corresponds to gSTM's gettext using LANG/LC_MESSAGES)
            // 取得系統文化（對應 gSTM 的 gettext 使用 LANG/LC_MESSAGES）
            var systemCulture = CultureInfo.CurrentCulture.Name;
            DebugLogger.Log($"[LocalizationService] System culture detected: {systemCulture}");

            // Map to supported language
            // 映射到支援的語言
            var mappedLanguage = MapToSupportedLanguage(systemCulture);
            DebugLogger.Log($"[LocalizationService] Mapped to supported language: {mappedLanguage}");

            return new CultureInfo(mappedLanguage);
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[LocalizationService] Failed to detect system language: {ex.Message}");
            // Fallback to English on error
            // 發生錯誤時使用英文作為備用
            return new CultureInfo("en-US");
        }
    }

    /// <summary>
    /// Map system culture to supported language
    /// 將系統文化映射到支援的語言
    /// </summary>
    /// <remarks>
    /// Supports various formats:
    /// - Standard: zh-TW, zh-CN, en-US, ja-JP
    /// - Underscore: zh_TW, zh_CN, en_US, ja_JP
    /// - With encoding: zh_TW.UTF-8, en_US.UTF-8
    /// - Script tags: zh-Hant, zh-Hans
    /// - Variants: en-GB, zh-HK, zh-SG
    /// - POSIX: C, C.UTF-8 (Invariant Culture)
    /// </remarks>
    private string MapToSupportedLanguage(string cultureName)
    {
        // Handle Invariant Culture (empty string from C/C.UTF-8)
        // 處理 Invariant Culture（C/C.UTF-8 產生的空字串）
        if (string.IsNullOrEmpty(cultureName))
        {
            DebugLogger.Log("[LocalizationService] Invariant culture detected, using English");
            return "en-US";
        }

        // Normalize: .NET already handles underscore and .UTF-8 suffix
        // 標準化：.NET 已經處理了底線和 .UTF-8 後綴
        var normalized = cultureName.ToLowerInvariant();

        // Traditional Chinese (繁體中文)
        if (normalized.StartsWith("zh-tw") ||      // Taiwan
            normalized.StartsWith("zh-hant") ||    // Traditional Chinese script
            normalized.StartsWith("zh-hk") ||      // Hong Kong
            normalized.StartsWith("zh-mo"))        // Macau
        {
            return "zh-TW";
        }

        // Simplified Chinese (简体中文)
        if (normalized.StartsWith("zh-cn") ||      // China
            normalized.StartsWith("zh-hans") ||    // Simplified Chinese script
            normalized.StartsWith("zh-sg"))        // Singapore
        {
            return "zh-CN";
        }

        // Japanese (日本語)
        if (normalized.StartsWith("ja"))           // ja, ja-JP, ja-jp
        {
            return "ja-JP";
        }

        // Korean (한국어)
        if (normalized.StartsWith("ko"))           // ko, ko-KR, ko-kr
        {
            return "ko-KR";
        }

        // English (all variants)
        if (normalized.StartsWith("en"))           // en, en-US, en-GB, en-AU, etc.
        {
            return "en-US";
        }

        // Unsupported language: fallback to English
        // 不支援的語言：使用英文作為備用
        DebugLogger.Log($"[LocalizationService] Unsupported culture '{cultureName}', fallback to English");
        return "en-US";
    }

    /// <summary>
    /// 取得當前語言代碼
    /// </summary>
    public string CurrentLanguage => _currentCulture.Name;

    /// <summary>
    /// 取得本地化字串
    /// </summary>
    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key;
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[LocalizationService] Cannot get string '{key}': {ex.Message}");
            return key;
        }
    }

    /// <summary>
    /// 取得格式化的本地化字串
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        try
        {
            var format = GetString(key);
            return string.Format(format, args);
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[LocalizationService] Failed to format string '{key}': {ex.Message}");
            return key;
        }
    }

    /// <summary>
    /// 切換語言
    /// </summary>
    public void ChangeLanguage(string cultureName)
    {
        try
        {
            _currentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = _currentCulture;
            CultureInfo.CurrentUICulture = _currentCulture;

            DebugLogger.Log($"[LocalizationService] Language changed to: {cultureName}");
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[LocalizationService] Failed to change language: {ex.Message}");
        }
    }

    /// <summary>
    /// 取得可用的語言清單
    /// </summary>
    public List<LanguageInfo> GetAvailableLanguages()
    {
        return new List<LanguageInfo>
        {
            new LanguageInfo("zh-TW", "繁體中文 (Traditional Chinese)"),
            new LanguageInfo("zh-CN", "简体中文 (Simplified Chinese)"),
            new LanguageInfo("en-US", "English"),
            new LanguageInfo("ja-JP", "日本語 (Japanese)"),
            new LanguageInfo("ko-KR", "한국어 (Korean)")
        };
    }
}

/// <summary>
/// 語言資訊
/// </summary>
public record LanguageInfo(string Code, string DisplayName);
