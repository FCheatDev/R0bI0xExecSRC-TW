using System;
using System.Diagnostics;
using System.IO;

namespace Executor
{
    internal static class AppPaths
    {
        private static readonly Lazy<string> AppDirLazy = new(GetAppDirectory);
        private static readonly Lazy<string> ConfigPathLazy = new(ResolveConfigPath);

        internal static string AppDirectory => AppDirLazy.Value;

        internal static string ConfigPath => ConfigPathLazy.Value;

        private static string GetAppDirectory()
        {
            try
            {
                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    var dir = Path.GetDirectoryName(processPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var fallback = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    var dir = Path.GetDirectoryName(fallback);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string ResolveConfigPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppDirectory, "config.cfg"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Executor", "config.cfg"),
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var dir = Path.GetDirectoryName(candidate);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using var _ = new FileStream(candidate, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    return candidate;
                }
                catch
                {
                }
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.cfg");
        }
    }
}
