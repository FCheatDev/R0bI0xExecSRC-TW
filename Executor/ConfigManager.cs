using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Executor
{
    internal static class ConfigManager
    {
        internal static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.cfg");

        internal static Dictionary<string, string> ReadConfig()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(ConfigPath))
            {
                return result;
            }

            foreach (var rawLine in File.ReadAllLines(ConfigPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (key.Length == 0)
                {
                    continue;
                }

                result[key] = value;
            }

            return result;
        }

        internal static void WriteConfig(IDictionary<string, string> config)
        {
            var lines = new List<string>();

            if (config.TryGetValue("language", out var lang))
            {
                lines.Add($"language={lang}");
            }

            if (config.TryGetValue("theme", out var theme))
            {
                lines.Add($"theme={theme}");
            }

            foreach (var kv in config
                .Where(kv => !string.Equals(kv.Key, "language", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(kv.Key, "theme", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"{kv.Key}={kv.Value}");
            }

            File.WriteAllLines(ConfigPath, lines);
        }

        internal static string? Get(Dictionary<string, string> config, string key)
        {
            return config.TryGetValue(key, out var value) ? value : null;
        }

        internal static void Set(Dictionary<string, string> config, string key, string value)
        {
            config[key] = value;
        }
    }
}
