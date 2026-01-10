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

        internal static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        internal static void Load(string? languageCode)
        {
            var lang = string.IsNullOrWhiteSpace(languageCode) ? "zh" : languageCode.Trim().ToLowerInvariant();
            CurrentLanguageCode = lang;

            var path = Path.Combine(BaseDirectory, "Assets", "WaveUI", $"{lang}.json");
            Dictionary<string, string>? data = null;
            string? json = null;

            try
            {
                var uriCandidates = new[]
                {
                    new Uri($"pack://application:,,,/Assets/WaveUI/{lang}.json", UriKind.Absolute),
                    new Uri($"pack://application:,,,/assets/lang/{lang}.json", UriKind.Absolute),
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
            catch
            {
                json = null;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(json) && File.Exists(path))
                {
                    json = File.ReadAllText(path);
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    var legacyPath = Path.Combine(BaseDirectory, "assets", "lang", $"{lang}.json");
                    if (File.Exists(legacyPath))
                    {
                        json = File.ReadAllText(legacyPath);
                    }
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
            }
            catch
            {
                data = null;
            }

            _strings = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LanguageChanged?.Invoke();
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
