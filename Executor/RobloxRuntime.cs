using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace Executor
{
    internal static class RobloxRuntime
    {
        private static readonly object Sync = new();
        private static bool _initialized;
        private static bool _isRunning;
        private static DateTime _lastRefreshUtc;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static ManagementEventWatcher? _startWatcher;
        private static ManagementEventWatcher? _stopWatcher;

        internal static bool IsRobloxRunning
        {
            get
            {
                bool running;
                var now = DateTime.UtcNow;

                lock (Sync)
                {
                    if (!_initialized)
                    {
                        return DetectRobloxProcessRunning();
                    }

                    running = _isRunning;
                    if (now - _lastRefreshUtc <= RefreshInterval)
                    {
                        return running;
                    }
                }

                try
                {
                    UpdateIsRunningFromTrace();
                }
                catch
                {
                }

                lock (Sync)
                {
                    return _isRunning;
                }
            }
        }

        internal static event Action<bool>? RobloxRunningChanged;

        internal static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
                _isRunning = DetectRobloxProcessRunning();
                _lastRefreshUtc = DateTime.UtcNow;
            }

            TryStartWatchers();
        }

        internal static void RefreshRunningState()
        {
            try
            {
                UpdateIsRunningFromTrace();
            }
            catch
            {
            }
        }

        internal static void Shutdown()
        {
            lock (Sync)
            {
                try
                {
                    _startWatcher?.Stop();
                }
                catch
                {
                }

                try
                {
                    _stopWatcher?.Stop();
                }
                catch
                {
                }

                try
                {
                    _startWatcher?.Dispose();
                }
                catch
                {
                }

                try
                {
                    _stopWatcher?.Dispose();
                }
                catch
                {
                }

                _startWatcher = null;
                _stopWatcher = null;
            }
        }

        internal static bool DetectRobloxProcessRunning()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    string? name;
                    try
                    {
                        name = p.ProcessName;
                    }
                    catch
                    {
                        continue;
                    }

                    if (name.StartsWith("RobloxPlayer", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("RobloxStudio", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        internal static bool TryGetRobloxProcessId(out int pid)
        {
            pid = 0;

            try
            {
                var candidates = Process.GetProcesses()
                    .Select(p =>
                    {
                        try
                        {
                            var name = p.ProcessName;
                            if (!name.StartsWith("RobloxPlayer", StringComparison.OrdinalIgnoreCase)
                                && !name.StartsWith("RobloxStudio", StringComparison.OrdinalIgnoreCase))
                            {
                                return null;
                            }

                            var hasWindow = false;
                            try
                            {
                                hasWindow = p.MainWindowHandle != IntPtr.Zero;
                            }
                            catch
                            {
                            }

                            DateTime startTime;
                            try
                            {
                                startTime = p.StartTime;
                            }
                            catch
                            {
                                startTime = DateTime.MinValue;
                            }

                            return new
                            {
                                Process = p,
                                HasWindow = hasWindow,
                                StartTime = startTime,
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(x => x != null)
                    .Select(x => x!)
                    .OrderByDescending(x => x.HasWindow)
                    .ThenByDescending(x => x.StartTime)
                    .ToList();

                var best = candidates.FirstOrDefault();
                if (best == null)
                {
                    return false;
                }

                try
                {
                    pid = best.Process.Id;
                    return pid > 0;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
            }

            return false;
        }

        internal static void KillRoblox()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    string? name;
                    try
                    {
                        name = p.ProcessName;
                    }
                    catch
                    {
                        continue;
                    }

                    if (!name.StartsWith("RobloxPlayer", StringComparison.OrdinalIgnoreCase)
                        && !name.StartsWith("RobloxStudio", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        p.Kill(true);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        internal static bool TryLaunchRoblox()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var explicitPath = ConfigManager.Get(cfg, "roblox_path");

                if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = explicitPath,
                        UseShellExecute = true,
                    });
                    return true;
                }

                if (TryResolveFishstrapRobloxPlayerPath(out var exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                    });
                    return true;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "roblox://",
                    UseShellExecute = true,
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveFishstrapRobloxPlayerPath(out string exePath)
        {
            exePath = string.Empty;

            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fishstrap", "Versions");
            if (!Directory.Exists(baseDir))
            {
                return false;
            }

            try
            {
                var best = Directory
                    .EnumerateDirectories(baseDir)
                    .Select(dir => new
                    {
                        Dir = dir,
                        Exe = Path.Combine(dir, "RobloxPlayerBeta.exe"),
                        Time = Directory.GetLastWriteTimeUtc(dir),
                    })
                    .Where(x =>
                    {
                        try
                        {
                            var name = Path.GetFileName(x.Dir);
                            return name.StartsWith("version-", StringComparison.OrdinalIgnoreCase) && File.Exists(x.Exe);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .OrderByDescending(x => x.Time)
                    .FirstOrDefault();

                if (best != null)
                {
                    exePath = best.Exe;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void TryStartWatchers()
        {
            try
            {
                _startWatcher = CreateWatcher(isStart: true);
                _stopWatcher = CreateWatcher(isStart: false);

                if (_startWatcher != null)
                {
                    _startWatcher.EventArrived += (_, _) => UpdateIsRunningFromTrace();
                }

                if (_stopWatcher != null)
                {
                    _stopWatcher.EventArrived += (_, _) => UpdateIsRunningFromTrace();
                }

                _startWatcher?.Start();
                _stopWatcher?.Start();
            }
            catch
            {
                Shutdown();

                lock (Sync)
                {
                    _initialized = false;
                }
            }
        }

        private static ManagementEventWatcher? CreateWatcher(bool isStart)
        {
            var wql = isStart
                ? "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName LIKE 'RobloxPlayer%.exe' OR ProcessName LIKE 'RobloxStudio%.exe'"
                : "SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName LIKE 'RobloxPlayer%.exe' OR ProcessName LIKE 'RobloxStudio%.exe'";

            var query = new WqlEventQuery(wql);
            return new ManagementEventWatcher(query);
        }

        private static void UpdateIsRunningFromTrace()
        {
            var running = DetectRobloxProcessRunning();
            bool changed;

            lock (Sync)
            {
                changed = running != _isRunning;
                _isRunning = running;
                _lastRefreshUtc = DateTime.UtcNow;
            }

            if (changed)
            {
                try
                {
                    RobloxRunningChanged?.Invoke(running);
                }
                catch
                {
                }
            }
        }
    }
}
