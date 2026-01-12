using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Executor
{
    internal static class LocalizationManager
    {
        private static Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

        internal static string CurrentLanguageCode { get; private set; } = "zh";

        internal static event Action? LanguageChanged;

        internal static string BaseDirectory => AppPaths.AppDirectory;

        internal static void Load(string? languageCode)
        {
            var lang = string.IsNullOrWhiteSpace(languageCode) ? "zh" : languageCode.Trim().ToLowerInvariant();
            CurrentLanguageCode = lang;

            var path = Path.Combine(BaseDirectory, "assets", "lang", $"{lang}.json");
            var outputAssetsPath = Path.Combine(BaseDirectory, "Assets", "lang", $"{lang}.json");
            Dictionary<string, string>? data = null;
            string? json = null;

            try
            {
                if (string.IsNullOrWhiteSpace(json) && File.Exists(outputAssetsPath))
                {
                    json = File.ReadAllText(outputAssetsPath);
                }

                if (string.IsNullOrWhiteSpace(json) && File.Exists(path))
                {
                    json = File.ReadAllText(path);
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    var uriCandidates = new[]
                    {
                        new Uri($"pack://application:,,,/assets/lang/{lang}.json", UriKind.Absolute),
                        new Uri($"pack://application:,,,/Assets/lang/{lang}.json", UriKind.Absolute),
                        new Uri($"pack://application:,,,/Assets/WaveUI/{lang}.json", UriKind.Absolute),
                    };

                    foreach (var uri in uriCandidates)
                    {
                        var streamInfo = Application.GetResourceStream(uri);
                        if (streamInfo == null)
                        {
                            continue;
                        }

                        using var reader = new StreamReader(streamInfo.Stream);
                        json = reader.ReadToEnd();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    var legacyPath = Path.Combine(BaseDirectory, "Assets", "WaveUI", $"{lang}.json");
                    if (File.Exists(legacyPath))
                    {
                        json = File.ReadAllText(legacyPath);
                    }
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    data = ParseLocalizationJson(json);
                }
            }
            catch
            {
                data = null;
            }

            _strings = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LanguageChanged?.Invoke();
        }

        private static Dictionary<string, string> ParseLocalizationJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    result[prop.Name] = prop.Value.GetString() ?? "";
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenObject(result, prop.Name, prop.Value);
                }
                else
                {
                    // ignore
                }
            }

            return result;
        }

        private static void FlattenObject(Dictionary<string, string> result, string prefix, JsonElement obj)
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var prop in obj.EnumerateObject())
            {
                var key = prefix + "." + prop.Name;

                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    result[key] = prop.Value.GetString() ?? "";
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenObject(result, key, prop.Value);
                }
                else
                {
                    // ignore
                }
            }
        }

        internal static string T(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "";
            }

            return _strings.TryGetValue(key, out var value) ? value : key;
        }

        internal static string F(string key, params object[] args)
        {
            var fmt = T(key);
            try
            {
                return string.Format(fmt, args);
            }
            catch
            {
                return fmt;
            }
        }
    }
}
