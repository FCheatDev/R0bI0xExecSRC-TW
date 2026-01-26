using System;
using System.Collections.Generic;
using System.Windows;
using Executor;

namespace Executor.WaveUI
{
    internal static class WaveToastService
    {
        private static WaveToastWindow? _window;
        private const string ToastFilterConfigPrefix = "WaveUI_toast_";

        private readonly struct ToastFilterRule
        {
            public ToastFilterRule(string id, string localizationKey)
            {
                Id = id;
                LocalizationKey = localizationKey;
            }

            public string Id { get; }
            public string LocalizationKey { get; }

            public bool IsMatch(string message)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return false;
                }

                var template = LocalizationManager.T(LocalizationKey);
                if (string.IsNullOrWhiteSpace(template))
                {
                    return false;
                }

                return MatchesTemplate(message, template);
            }
        }

        private static readonly ToastFilterRule[] ToastFilterRules =
        {
            new("shell_attaching", "WaveUI.Shell.Toast.Attaching"),
            new("shell_attached", "WaveUI.Shell.Toast.Attached"),
            new("shell_attach_error", "WaveUI.Shell.Toast.AttachError"),
            new("settings_api_restart", "WaveUI.Settings.APIs.Toast.RestartingRoblox"),
            new("editor_monaco_not_ready", "WaveUI.Editor.Toast.MonacoNotReady"),
            new("editor_pinned_tab", "WaveUI.Editor.Toast.PinnedTabCantBeClosed"),
            new("editor_at_least_one", "WaveUI.Editor.Toast.AtLeastOneTabMustRemain"),
            new("editor_max_tabs", "WaveUI.Editor.Toast.MaxTabs"),
            new("editor_monaco_assets", "WaveUI.Editor.Toast.MonacoAssetsNotFound"),
            new("editor_monaco_init_failed", "WaveUI.Editor.Toast.MonacoInitFailed"),
            new("editor_roblox_killed", "WaveUI.Editor.Toast.RobloxKilled"),
            new("editor_script_empty", "WaveUI.Editor.Toast.ScriptEmpty"),
            new("editor_script_name_empty", "WaveUI.Editor.Toast.ScriptNameEmpty"),
            new("editor_script_name_invalid", "WaveUI.Editor.Toast.ScriptNameInvalid"),
            new("editor_script_ext_invalid", "WaveUI.Editor.Toast.ScriptExtensionInvalid"),
            new("editor_script_exists", "WaveUI.Editor.Toast.ScriptExists"),
            new("editor_saved", "WaveUI.Editor.Toast.Saved"),
            new("scripts_script_empty", "WaveUI.Scripts.Toast.ScriptEmpty"),
            new("scripts_copied", "WaveUI.Scripts.Toast.Copied"),
            new("scripts_invalid_response", "WaveUI.Scripts.Toast.InvalidResponse"),
            new("roblox_prompt_open", "WaveUI.Roblox.Prompt.Open"),
        };

        private static string NormalizeNewlines(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            return text
                .Replace("\\\\r\\\\n", "\r\n", StringComparison.Ordinal)
                .Replace("\\\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r\\n", "\r\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal);
        }

        internal static void Show(string title, string message)
        {
            var normalizedMessage = NormalizeNewlines(message);
            if (!ShouldShowToast(normalizedMessage))
            {
                return;
            }

            if (Application.Current == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 如果視窗不存在，創建新視窗
                if (_window == null)
                {
                    _window = new WaveToastWindow();
                    _window.Closed += (_, _) => _window = null;
                }

                // 因為視窗已經是 Topmost，設置 Owner 會導致衝突
                // 移除這段：
                // if (Application.Current.MainWindow != null && _window.Owner != Application.Current.MainWindow)
                // {
                //     _window.Owner = Application.Current.MainWindow;
                // }

                _window.ShowToast(NormalizeNewlines(title), normalizedMessage);
            });
        }

        internal static void ShowPrompt(string title, string message, string yesText, string noText, Action? onYes, Action? onNo)
        {
            var normalizedMessage = NormalizeNewlines(message);
            if (!ShouldShowToast(normalizedMessage))
            {
                return;
            }

            if (Application.Current == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_window == null)
                {
                    _window = new WaveToastWindow();
                    _window.Closed += (_, _) => _window = null;
                }

                _window.ShowPrompt(
                    NormalizeNewlines(title),
                    normalizedMessage,
                    NormalizeNewlines(yesText),
                    NormalizeNewlines(noText),
                    onYes,
                    onNo);
            });
        }

        private static bool ShouldShowToast(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return true;
                }

                var cfg = ConfigManager.ReadConfig();
                if (AreAllToastFiltersExplicitlyDisabled(cfg))
                {
                    return false;
                }

                foreach (var rule in ToastFilterRules)
                {
                    if (!rule.IsMatch(message))
                    {
                        continue;
                    }

                    return IsToastFilterEnabled(cfg, rule.Id);
                }
            }
            catch
            {
            }

            return true;
        }

        private static bool AreAllToastFiltersExplicitlyDisabled(Dictionary<string, string> config)
        {
            foreach (var rule in ToastFilterRules)
            {
                var key = ToastFilterConfigPrefix + rule.Id;
                if (!config.TryGetValue(key, out var value))
                {
                    return false;
                }

                if (!bool.TryParse(value?.Trim(), out var enabled))
                {
                    return false;
                }

                if (enabled)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsToastFilterEnabled(Dictionary<string, string> config, string id)
        {
            var key = ToastFilterConfigPrefix + id;
            if (config.TryGetValue(key, out var value) && bool.TryParse(value?.Trim(), out var enabled))
            {
                return enabled;
            }

            return true;
        }

        private static bool MatchesTemplate(string message, string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return false;
            }

            if (!template.Contains("{0}", StringComparison.Ordinal))
            {
                return string.Equals(message, template, StringComparison.Ordinal);
            }

            var parts = template.Split(new[] { "{0}" }, StringSplitOptions.None);
            if (parts.Length == 0)
            {
                return string.Equals(message, template, StringComparison.Ordinal);
            }

            var prefix = parts[0];
            var suffix = parts[^1];
            if (!message.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (!message.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            return message.Length >= prefix.Length + suffix.Length;
        }
    }
}