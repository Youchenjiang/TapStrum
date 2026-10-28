namespace Tapster;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

/// <summary>
/// Provides unified application localization, language switching, and string table lookup.
/// </summary>
public static class LocalizationManager
{
    public const string LangAuto = "auto";
    public const string LangZhTw = "zh-TW";
    public const string LangEnUs = "en-US";
    public const string LangJaJp = "ja-JP";
    public const string LangZhCn = "zh-CN";

    public static readonly IReadOnlyList<(string Code, string DisplayName)> SupportedLanguages = new[]
    {
        (LangAuto, "跟隨系統"),
        (LangZhTw, "繁體中文"),
        (LangEnUs, "English"),
        (LangJaJp, "日本語"),
        (LangZhCn, "简体中文"),
    };

    private static readonly string[] ResourceLocales = [LangZhTw, LangEnUs, LangJaJp, LangZhCn];

    private static string _currentLanguage = LangAuto;
    private static string _effectiveLanguage = LangEnUs;

    private static readonly Dictionary<string, Dictionary<string, string>> RawTranslations = InitializeTranslations();

    static LocalizationManager()
    {
        SetLanguage(LangAuto);
    }

    public static event Action? LanguageChanged;

    public static string CurrentLanguage => _currentLanguage;

    public static string EffectiveLanguage => _effectiveLanguage;

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Translations { get; } =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(
            RawTranslations.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(kvp.Value),
                StringComparer.OrdinalIgnoreCase));

    public static string GetLanguageDisplayName(string code)
    {
        return code switch
        {
            LangAuto => Get("Settings_LangAuto"),
            LangZhTw => "繁體中文",
            LangEnUs => "English",
            LangJaJp => "日本語",
            LangZhCn => "简体中文",
            _ => code,
        };
    }

    public static void SetLanguage(string langCode)
    {
        _currentLanguage = string.IsNullOrWhiteSpace(langCode) ? LangAuto : langCode;

        if (string.Equals(_currentLanguage, LangAuto, StringComparison.OrdinalIgnoreCase))
        {
            _effectiveLanguage = ResolveSystemLanguage();
        }
        else
        {
            _effectiveLanguage = NormalizeLanguageCode(_currentLanguage);
        }

        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        if (RawTranslations.TryGetValue(_effectiveLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }

        if (RawTranslations.TryGetValue(LangEnUs, out var fallbackDict) && fallbackDict.TryGetValue(key, out var fallbackVal))
        {
            return fallbackVal;
        }

        return key;
    }

    private static string ResolveSystemLanguage()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        if (currentCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            if (currentCulture.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("HK", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("MO", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("Hant", StringComparison.OrdinalIgnoreCase))
            {
                return LangZhTw;
            }

            return LangZhCn;
        }

        if (currentCulture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LangJaJp;
        }

        return LangEnUs;
    }

    private static string NormalizeLanguageCode(string code)
    {
        if (code.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return LangZhTw;
        }

        if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LangZhCn;
        }

        if (code.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LangJaJp;
        }

        return LangEnUs;
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeTranslations()
    {
        var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(LocalizationManager).Assembly;
        var manifestNames = assembly.GetManifestResourceNames();

        foreach (var locale in ResourceLocales)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var suffix = $".{locale}.json";
            var resourceName = manifestNames.FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName is not null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is not null)
                {
                    using var doc = JsonDocument.Parse(stream);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                }
            }

            translations[locale] = dict;
        }

        return translations;
    }
}
