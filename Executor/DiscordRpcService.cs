using System;
using DiscordRPC;

namespace Executor
{
    internal static class DiscordRpcService
    {
        internal const string EnabledConfigKey = "discord_rpc";
        private const string DiscordAppId = "1465968954320093238";
        private static readonly object LockObj = new();
        private static DiscordRpcClient? _client;
        private static string? _lastTheme;

        internal static void ApplyTheme(string? theme)
        {
            try
            {
                var appId = DiscordAppId.Trim();

                if (string.IsNullOrWhiteSpace(appId))
                {
                    Shutdown();
                    return;
                }

                if (!ulong.TryParse(appId, out _))
                {
                    Shutdown();
                    return;
                }

                var normalizedTheme = NormalizeTheme(theme);

                lock (LockObj)
                {
                    if (_client == null)
                    {
                        try
                        {
                            _client?.Dispose();
                        }
                        catch
                        {
                        }

                        _client = null;
                        _lastTheme = null;

                        try
                        {
                            _client = new DiscordRpcClient(appId);
                            _client.Initialize();
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                Logger.Exception("DiscordRpcService", ex);
                            }
                            catch
                            {
                            }

                            try
                            {
                                _client?.Dispose();
                            }
                            catch
                            {
                            }

                            _client = null;
                            return;
                        }
                    }

                    if (_client == null)
                    {
                        return;
                    }

                    if (string.Equals(_lastTheme, normalizedTheme, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _lastTheme = normalizedTheme;

                    try
                    {
                        _client.SetPresence(BuildPresence(normalizedTheme));
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Logger.Exception("DiscordRpcService", ex);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        internal static void Shutdown()
        {
            lock (LockObj)
            {
                try
                {
                    _client?.Dispose();
                }
                catch
                {
                }

                _client = null;
                _lastTheme = null;
            }
        }

        private static RichPresence BuildPresence(string theme)
        {
            var details = GetDetails(theme);
            var largeKey = GetLargeImageKey(theme);
            var state = GetState(theme);

            return new RichPresence
            {
                Details = details,
                State = state,
                Assets = new Assets
                {
                    LargeImageKey = largeKey,
                    LargeImageText = details,
                },
            };
        }

        private static string GetState(string theme)
        {
            if (string.Equals(theme, "WaveUI-2026/2", StringComparison.OrdinalIgnoreCase))
            {
                return "v2026/2";
            }

            if (string.Equals(theme, "WaveUI-2025", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "Wave", StringComparison.OrdinalIgnoreCase))
            {
                return "v2025";
            }

            return "Idle";
        }

        private static string GetDetails(string theme)
        {
            if (string.Equals(theme, "KRNL", StringComparison.OrdinalIgnoreCase))
            {
                return "KRNL Executor Remake";
            }

            if (string.Equals(theme, "Synapse X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "SynapseX", StringComparison.OrdinalIgnoreCase))
            {
                return "Synapse X Executor Remake";
            }

            return "Wave Executor Remake";
        }

        private static string GetLargeImageKey(string theme)
        {
            if (string.Equals(theme, "KRNL", StringComparison.OrdinalIgnoreCase))
            {
                return "krnl";
            }

            if (string.Equals(theme, "Synapse X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "SynapseX", StringComparison.OrdinalIgnoreCase))
            {
                return "synapse";
            }

            return "wave";
        }

        private static string NormalizeTheme(string? theme)
        {
            var t = (theme ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
            {
                return "WaveUI-2025";
            }

            if (string.Equals(t, "Wave", StringComparison.OrdinalIgnoreCase))
            {
                return "WaveUI-2025";
            }

            return t;
        }
    }
}
