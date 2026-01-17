using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Executor;
using Executor.WaveUI;
using Microsoft.Win32;

namespace Executor.WaveUI.WaveViews
{
    public partial class SettingsView : UserControl
    {
        public sealed class ApiEntry
        {
            public ApiEntry(string name, string? sunc, string? unc)
            {
                Name = name;
                sUNC = sunc;
                UNC = unc;
            }

            public string Name { get; }
            public string? sUNC { get; }
            public string? UNC { get; }

            public string SuncLine => $"sUNC: {NormalizeValue(sUNC)}";
            public string UncLine => $"UNC: {NormalizeValue(UNC)}";

            private static string NormalizeValue(string? v)
            {
                var trimmed = (v ?? "").Trim();
                return trimmed.Length == 0 ? "NULL" : trimmed;
            }
        }

        private void SkipLoadAppCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = SkipLoadAppCheckBox.IsChecked == true;
            SaveConfigValue(SkipLoadAppKey, enabled.ToString().ToLowerInvariant());
        }

        private void OpenAppDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFolder(AppPaths.AppDirectory);
        }

        private void OpenMonacoDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFolder(MonacoDirectoryPath);
        }

        private void OpenWaveUiDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFolder(Path.Combine(AppPaths.AppDirectory, "Assets", "WaveUI"));
        }

        private void OpenApisDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFolder(ApisDirectoryPath);
        }

        private void CopyAppDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            CopyPath(AppDirectoryPath, AppDirectoryCopyToolTip, sender as FrameworkElement);
        }

        private void CopyMonacoDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            CopyPath(MonacoDirectoryPath, MonacoCopyToolTip, sender as FrameworkElement);
        }

        private void CopyWaveUiResources_OnClick(object sender, RoutedEventArgs e)
        {
            CopyPath(WaveUiResourcesPath, WaveUiResourcesCopyToolTip, sender as FrameworkElement);
        }

        private void CopyApisDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            CopyPath(ApisDirectoryPath, ApisDirectoryCopyToolTip, sender as FrameworkElement);
        }

        private static void OpenFolder(string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    WaveToastService.Show("Error", dir);
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void CopyPath(string? path, ToolTip? toolTip, FrameworkElement? owner)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    WaveToastService.Show("Error", path ?? string.Empty);
                    return;
                }

                Clipboard.SetText(path);
                ShowCopyFeedback(toolTip, owner);
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void ShowCopyFeedback(ToolTip? toolTip, FrameworkElement? owner)
        {
            if (toolTip == null)
            {
                return;
            }

            if (owner != null)
            {
                toolTip.PlacementTarget = owner;
            }

            var originalStaysOpen = toolTip.StaysOpen;
            toolTip.StaysOpen = true;

            toolTip.Content = LocalizationManager.T("WaveUI.Common.Copied");
            toolTip.IsOpen = false;
            toolTip.IsOpen = true;

            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1000),
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                toolTip.IsOpen = false;
                toolTip.Content = LocalizationManager.T("WaveUI.Common.Copy");
                toolTip.StaysOpen = originalStaysOpen;
            };

            timer.Start();
        }

        public ObservableCollection<ApiEntry> ApiItems { get; } = new();

        public string AppDirectoryPath => AppPaths.AppDirectory;

        public string ApisDirectoryPath => Path.Combine(AppPaths.AppDirectory, "APIs");

        public string MonacoDirectoryPath => Path.Combine(AppPaths.AppDirectory, "Assets", "WaveUI", "Monaco");

        public string WaveUiResourcesPath => Path.Combine(AppPaths.AppDirectory, "Assets", "WaveUI");

        public string MonacoDirectoryPathDisplay => TruncateAfterAssets(MonacoDirectoryPath);

        public string WaveUiResourcesPathDisplay => TruncateAfterAssets(WaveUiResourcesPath);

        private static string TruncateAfterAssets(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var ridCut = TruncateAfterRid(path);
            if (!string.IsNullOrWhiteSpace(ridCut))
            {
                return ridCut;
            }

            const string token = "\\Assets\\";
            var idx = path.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return path;
            }

            var cut = idx + token.Length;
            if (cut < 0 || cut > path.Length)
            {
                return path;
            }

            return path[..cut] + "....";
        }

        private static string? TruncateAfterRid(string path)
        {
            var tokens = new[]
            {
                "\\win-x64\\",
                "\\win-x86\\",
            };

            foreach (var token in tokens)
            {
                var idx = path.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }

                var cut = idx + token.Length;
                if (cut <= 0 || cut > path.Length)
                {
                    continue;
                }

                return path[..cut] + "....";
            }

            return null;
        }

        private const string SelectedApiKey = "selected_api";
        private const string TopmostKey = "topmost";
        private const string StartupKey = "start_on_boot";
        private const string OpacityKey = "opacity";
        private const string SkipLoadAppKey = "skip_load_app";

        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryValueName = "Wave";

        private bool _isApiModalAnimating;
        private bool _suppressSettingEvents;
        private int _scrollAnimationId;
        private int _navScrollRequestId;
        private DispatcherOperation? _pendingNavScrollOperation;
        private string? _activeNavTag;
        private string? _targetNavTag;
        private string? _lockedNavTag;
        private bool _isScrollAnimating; // 新增：標記是否正在動畫滾動
        private bool _initialized;

        private DispatcherTimer? _apiDebugTimer;
        private bool? _lastApiDebugIsAttached;
        private bool? _lastApiDebugIsRobloxOpen;
        private int _apiDebugPollId;
        private bool _apiDebugPollRunning;

        public SettingsView()
        {
            InitializeComponent();

            DataContext = this;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;

            try
            {
                _apiDebugTimer?.Stop();
            }
            catch
            {
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            if (!_initialized)
            {
                _initialized = true;
                LoadApisFromVersionJson();
                ApplyInitialSelectedApi();
                LoadAndApplySettings();
            }

            _isScrollAnimating = false;
            UpdateNavHighlightFromScroll();

            ApplyLanguage();

            EnsureApiDebugTimer();
            UpdateApiDebugStatus(force: true);
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(ApplyLanguage));
        }

        private void ApplyLanguage()
        {
            if (!IsLoaded)
            {
                return;
            }

            SettingsRightTitleText.Text = LocalizationManager.T("WaveUI.Settings.Right.Title");
            SettingsRightSubtitleText.Text = LocalizationManager.T("WaveUI.Settings.Right.Subtitle");

            ApplicationSection.Text = LocalizationManager.T("WaveUI.Settings.Section.Application");
            AppearanceSection.Text = LocalizationManager.T("WaveUI.Settings.Section.Appearance");
            DataSection.Text = LocalizationManager.T("WaveUI.Settings.Section.Data");
            ApisSection.Text = LocalizationManager.T("WaveUI.Settings.Section.APIs");
            ApiDebugSection.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.Title");

            TopmostTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Topmost.Title");
            TopmostDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Topmost.Desc");
            BootTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Boot.Title");
            BootDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Boot.Desc");
            OpacityTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Opacity.Title");
            OpacityDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Opacity.Desc");

            ReloadAppTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Title");
            ReloadAppDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Desc");
            ReloadAppButtonText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Button");

            LanguageTitleText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Language.Title");
            LanguageDescText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Language.Desc");
            ThemeTitleText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Theme.Title");
            ThemeDescText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Theme.Desc");

            AppDirectoryTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.AppDirectory.Title");
            ApisDirectoryTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.ApisFolder.Title");
            MonacoTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.Monaco.Title");
            WaveUiImagesTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.WaveUiImages.Title");

            OpenConfigFolderTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenConfigFolder.Title");
            OpenConfigFolderDescText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenConfigFolder.Desc");
            OpenConfigFolderButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");

            OpenConfigFileTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenConfigFile.Title");
            OpenConfigFileDescText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenConfigFile.Desc");
            OpenConfigFileButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");

            OpenVersionFileTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenVersionFile.Title");
            OpenVersionFileDescText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenVersionFile.Desc");
            OpenVersionFileButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");

            OpenAppDirectoryButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");
            OpenApisDirectoryButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");
            OpenMonacoDirectoryButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");
            OpenWaveUiDirectoryButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");

            AppDirectoryCopyToolTip.Content = LocalizationManager.T("WaveUI.Common.Copy");
            ApisDirectoryCopyToolTip.Content = LocalizationManager.T("WaveUI.Common.Copy");
            MonacoCopyToolTip.Content = LocalizationManager.T("WaveUI.Common.Copy");
            WaveUiResourcesCopyToolTip.Content = LocalizationManager.T("WaveUI.Common.Copy");

            ChooseApisTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Choose.Title");
            ApiModalTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Choose.Title");
            ApiModalCurrentLabelText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Current");

            SkipLoadAppTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.SkipLoadApp.Title");
            SkipLoadAppDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.SkipLoadApp.Desc");

            TestPromptTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Title");
            TestPromptDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Desc");
            TestPromptButtonText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Button");

            NavApplicationTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Application");
            NavApplicationTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Application");
            NavAppearanceTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Appearance");
            NavAppearanceTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Appearance");
            NavDataTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Data");
            NavDataTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Data");
            NavApisTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.APIs");
            NavApisTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.APIs");

            NavDebugTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Debug");
            NavDebugTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Debug");

            UpdateApiDebugStatus(force: true);
        }

        private void TestPromptButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                WaveToastService.ShowPrompt(
                    LocalizationManager.T("WaveUI.Common.Info"),
                    LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Prompt"),
                    LocalizationManager.T("WaveUI.Common.Yes"),
                    LocalizationManager.T("WaveUI.Common.No"),
                    () =>
                    {
                        WaveToastService.Show(
                            LocalizationManager.T("WaveUI.Common.Info"),
                            LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Toast.Yes"));
                    },
                    () =>
                    {
                        WaveToastService.Show(
                            LocalizationManager.T("WaveUI.Common.Info"),
                            LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Toast.No"));
                    });
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void EnsureApiDebugTimer()
        {
            if (_apiDebugTimer == null)
            {
                _apiDebugTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(400),
                };

                _apiDebugTimer.Tick += (_, _) => UpdateApiDebugStatus(force: false);
            }

            if (!_apiDebugTimer.IsEnabled)
            {
                _apiDebugTimer.Start();
            }
        }

        private void UpdateApiDebugStatus(bool force)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (_apiDebugPollRunning)
            {
                return;
            }

            _apiDebugPollRunning = true;
            _apiDebugPollId++;
            var pollId = _apiDebugPollId;

            _ = Task.Run(() =>
            {
                bool? isRobloxOpen = null;
                bool? isAttached = null;

                try
                {
                    isRobloxOpen = SafeReadBool(() => SpashApiInvoker.IsRobloxProcessRunning());
                }
                catch
                {
                    isRobloxOpen = null;
                }

                try
                {
                    if (isRobloxOpen == false)
                    {
                        isAttached = false;
                    }
                    else if (SpashApiInvoker.IsInitializedForDebug())
                    {
                        isAttached = SafeReadBool(() => API.IsAttached());
                    }
                }
                catch
                {
                    isAttached = null;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsLoaded)
                    {
                        _apiDebugPollRunning = false;
                        return;
                    }

                    if (pollId != _apiDebugPollId)
                    {
                        _apiDebugPollRunning = false;
                        return;
                    }

                    var changed = force
                                  || _lastApiDebugIsAttached != isAttached
                                  || _lastApiDebugIsRobloxOpen != isRobloxOpen;

                    if (changed)
                    {
                        _lastApiDebugIsAttached = isAttached;
                        _lastApiDebugIsRobloxOpen = isRobloxOpen;

                        try
                        {
                            ApiDebugIsAttachedValueText.Text = FormatBoolStatus(isAttached);
                            ApiDebugIsRobloxOpenValueText.Text = FormatBoolStatus(isRobloxOpen);
                        }
                        catch
                        {
                        }
                    }

                    _apiDebugPollRunning = false;
                }), DispatcherPriority.Background);
            });
        }

        private static bool? SafeReadBool(Func<bool> func)
        {
            try
            {
                return func();
            }
            catch
            {
                return null;
            }
        }

        private static string FormatBoolStatus(bool? value)
        {
            if (value == null)
            {
                return "-";
            }

            return value.Value
                ? LocalizationManager.T("WaveUI.Common.Yes")
                : LocalizationManager.T("WaveUI.Common.No");
        }

        private void SettingsScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 在動畫滾動期間,完全忽略 ScrollChanged 事件
            if (_isScrollAnimating)
            {
                return;
            }

            UpdateNavHighlightFromScroll();
        }

private void UpdateNavHighlightFromScroll()
{
    if (!IsLoaded || SettingsScrollViewer == null)
    {
        return;
    }

    if (SettingsScrollViewer.Content is not FrameworkElement content)
    {
        return;
    }

    // 如果正在進行導航點擊,使用目標區塊而不是計算(避免高亮框在動畫開始前被 ScrollChanged 覆蓋)
    if (!string.IsNullOrWhiteSpace(_targetNavTag))
    {
        SetActiveNav(_targetNavTag);
        return;
    }

    if (!string.IsNullOrWhiteSpace(_lockedNavTag))
    {
        SetActiveNav(_lockedNavTag);
        return;
    }

    var viewport = SettingsScrollViewer.ViewportHeight;
    var bias = viewport > 1 ? (viewport * 0.35) : 60;
    var current = SettingsScrollViewer.VerticalOffset + bias;

    double appY = 0;
    double appearanceY = 0;
    double dataY = 0;
    double apisY = 0;
    double debugY = 0;

    try
    {
        appY = ApplicationSection.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        appearanceY = AppearanceSection.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        dataY = DataSection.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        apisY = ApisSection.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        debugY = ApiDebugSection.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
    }
    catch
    {
        return;
    }

    var appMid = (appY + appearanceY) / 2.0;
    var appearanceMid = (appearanceY + dataY) / 2.0;
    var dataMid = (dataY + apisY) / 2.0;
    var apisMid = (apisY + debugY) / 2.0;

    var tag = "Application";
    if (current >= apisMid)
    {
        tag = "Debug";
    }
    else if (current >= dataMid)
    {
        tag = "APIs";
    }
    else if (current >= appearanceMid)
    {
        tag = "Data";
    }
    else if (current >= appMid)
    {
        tag = "Appearance";
    }
    else
    {
        tag = "Application";
    }

    SetActiveNav(tag);
}

        private void SetActiveNav(string tag)
        {
            if (string.Equals(_activeNavTag, tag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeNavTag = tag;

            SetNavVisualState(
                isActive: string.Equals(tag, "Application", StringComparison.OrdinalIgnoreCase),
                activeBg: NavApplicationActiveBg,
                activeText: NavApplicationTextActive,
                inactiveText: NavApplicationTextInactive);

            SetNavVisualState(
                isActive: string.Equals(tag, "Appearance", StringComparison.OrdinalIgnoreCase),
                activeBg: NavAppearanceActiveBg,
                activeText: NavAppearanceTextActive,
                inactiveText: NavAppearanceTextInactive);

            SetNavVisualState(
                isActive: string.Equals(tag, "Data", StringComparison.OrdinalIgnoreCase),
                activeBg: NavDataActiveBg,
                activeText: NavDataTextActive,
                inactiveText: NavDataTextInactive);

            SetNavVisualState(
                isActive: string.Equals(tag, "APIs", StringComparison.OrdinalIgnoreCase),
                activeBg: NavApisActiveBg,
                activeText: NavApisTextActive,
                inactiveText: NavApisTextInactive);

            SetNavVisualState(
                isActive: string.Equals(tag, "Debug", StringComparison.OrdinalIgnoreCase),
                activeBg: NavDebugActiveBg,
                activeText: NavDebugTextActive,
                inactiveText: NavDebugTextInactive);
        }

        private static void SetNavVisualState(bool isActive, FrameworkElement activeBg, FrameworkElement activeText, FrameworkElement inactiveText)
        {
            var toActive = isActive ? 1.0 : 0.0;
            var toInactive = isActive ? 0.0 : 1.0;

            AnimateOpacity(activeBg, toActive);
            AnimateOpacity(activeText, toActive);
            AnimateOpacity(inactiveText, toInactive);
        }

        private static void AnimateOpacity(UIElement el, double to)
        {
            if (el == null)
            {
                return;
            }

            el.BeginAnimation(OpacityProperty, null);

            var anim = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            el.BeginAnimation(OpacityProperty, anim);
        }

        private void LoadAndApplySettings()
        {
            _suppressSettingEvents = true;
            try
            {
                var cfg = ConfigManager.ReadConfig();

                var topmost = ParseBool(ConfigManager.Get(cfg, TopmostKey), fallback: false);
                TopmostCheckBox.IsChecked = topmost;
                ApplyTopmost(topmost);

                var startup = ParseBool(ConfigManager.Get(cfg, StartupKey), fallback: IsStartupEnabledInRegistry());
                BootCheckBox.IsChecked = startup;

                var opacity = ParseDouble(ConfigManager.Get(cfg, OpacityKey), fallback: 1.0);
                opacity = Clamp(opacity, 0.1, 1.0);
                OpacitySlider.Value = opacity;
                OpacityValueText.Text = opacity.ToString("0.0");
                ApplyOpacity(opacity);

                var lang = (ConfigManager.Get(cfg, "language") ?? "zh").Trim().ToLowerInvariant();
                SelectLanguageCombo(lang);

                var theme = (ConfigManager.Get(cfg, "theme") ?? string.Empty).Trim();
                SelectThemeCombo(theme);

                var skipLoad = ParseBool(ConfigManager.Get(cfg, SkipLoadAppKey), fallback: false);
                SkipLoadAppCheckBox.IsChecked = skipLoad;
            }
            catch
            {
            }
            finally
            {
                _suppressSettingEvents = false;
            }
        }

        private static bool ParseBool(string? value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (bool.TryParse(value.Trim(), out var b))
            {
                return b;
            }

            return fallback;
        }

        private static double ParseDouble(string? value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (double.TryParse(value.Trim(), out var d))
            {
                return d;
            }

            return fallback;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min)
            {
                return min;
            }

            if (v > max)
            {
                return max;
            }

            return v;
        }

        private void ApplyTopmost(bool enabled)
        {
            try
            {
                var w = Window.GetWindow(this);
                if (w != null)
                {
                    w.Topmost = enabled;
                }
            }
            catch
            {
            }
        }

        private void ApplyOpacity(double opacity)
        {
            try
            {
                var w = Window.GetWindow(this);
                if (w != null)
                {
                    w.Opacity = Clamp(opacity, 0.1, 1.0);
                }
            }
            catch
            {
            }
        }

        private static bool IsStartupEnabledInRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
                var value = key?.GetValue(StartupRegistryValueName) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        private static void SetStartupInRegistry(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (key == null)
            {
                return;
            }

            if (!enabled)
            {
                try
                {
                    key.DeleteValue(StartupRegistryValueName, throwOnMissingValue: false);
                }
                catch
                {
                }
                return;
            }

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            key.SetValue(StartupRegistryValueName, "\"" + exePath + "\"");
        }

        private void SaveConfigValue(string key, string value)
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                ConfigManager.Set(cfg, key, value);
                ConfigManager.WriteConfig(cfg);
            }
            catch
            {
            }
        }

        private void SelectLanguageCombo(string lang)
        {
            foreach (var item in LanguageCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag as string, lang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageCombo.SelectedItem = cbi;
                    return;
                }
            }
        }

        private void SelectThemeCombo(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                return;
            }

            foreach (var item in ThemeCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Content as string, theme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeCombo.SelectedItem = cbi;
                    return;
                }
            }
        }

        private void TopmostCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = TopmostCheckBox.IsChecked == true;
            ApplyTopmost(enabled);
            SaveConfigValue(TopmostKey, enabled.ToString().ToLowerInvariant());
        }

        private void BootCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = BootCheckBox.IsChecked == true;
            try
            {
                SetStartupInRegistry(enabled);
                SaveConfigValue(StartupKey, enabled.ToString().ToLowerInvariant());
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void OpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || OpacitySlider == null || OpacityValueText == null)
            {
                return;
            }

            if (_suppressSettingEvents)
            {
                return;
            }

            var opacity = Clamp(OpacitySlider.Value, 0.1, 1.0);
            OpacityValueText.Text = opacity.ToString("0.0");
            ApplyOpacity(opacity);
            SaveConfigValue(OpacityKey, opacity.ToString("0.0"));
        }

        private void LanguageCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            if (LanguageCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var lang = (item.Tag as string) ?? "zh";
            SaveConfigValue("language", lang);
            LocalizationManager.Load(lang);
        }

        private void ThemeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            if (ThemeCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var theme = (item.Content as string) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(theme))
            {
                return;
            }

            SaveConfigValue("theme", theme);

            try
            {
                var w = Window.GetWindow(this);
                if (w is global::Executor.MainWindow mw)
                {
                    mw.ApplyTheme();
                }
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void OpenConfigFolder_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigManager.ConfigPath);
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void OpenConfigFile_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = ConfigManager.ConfigPath;
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void OpenVersionFile_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = GetVersionJsonPath();
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

private void RightNavItem_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not FrameworkElement el)
    {
        return;
    }

    var tag = el.Tag as string;
    if (string.IsNullOrWhiteSpace(tag))
    {
        return;
    }

    FrameworkElement? target = null;
    if (string.Equals(tag, "Application", StringComparison.OrdinalIgnoreCase))
    {
        target = ApplicationSection;
    }
    else if (string.Equals(tag, "Appearance", StringComparison.OrdinalIgnoreCase))
    {
        target = AppearanceSection;
    }
    else if (string.Equals(tag, "Data", StringComparison.OrdinalIgnoreCase))
    {
        target = DataSection;
    }
    else if (string.Equals(tag, "APIs", StringComparison.OrdinalIgnoreCase))
    {
        target = ApisSection;
    }
    else if (string.Equals(tag, "Debug", StringComparison.OrdinalIgnoreCase))
    {
        target = ApiDebugSection;
    }

    if (target == null)
    {
        return;
    }

    // 記錄目標區塊,在動畫期間保持此高亮
    _targetNavTag = tag;
    _lockedNavTag = tag;
    
    // 立即設置導航高亮
    SetActiveNav(tag);

    _navScrollRequestId++;
    var requestId = _navScrollRequestId;

    try
    {
        _pendingNavScrollOperation?.Abort();
    }
    catch
    {
    }

    _pendingNavScrollOperation = Dispatcher.BeginInvoke(new Action(() =>
    {
        if (requestId != _navScrollRequestId)
        {
            return;
        }

        SmoothScrollToSection(target, requestId);
    }), DispatcherPriority.Background);
}

        private void SmoothScrollToSection(FrameworkElement target, int requestId)
        {
            if (SettingsScrollViewer == null)
            {
                if (requestId == _navScrollRequestId)
                {
                    _targetNavTag = null;
                }
                return;
            }

            double targetOffset;
            try
            {
                var p = target.TransformToAncestor(SettingsScrollViewer).Transform(new Point(0, 0));
                targetOffset = SettingsScrollViewer.VerticalOffset + p.Y;
            }
            catch
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (requestId != _navScrollRequestId)
                    {
                        return;
                    }

                    SmoothScrollToSection(target, requestId);
                }), DispatcherPriority.ContextIdle);
                return;
            }

            targetOffset -= 10;
            if (targetOffset < 0)
            {
                targetOffset = 0;
            }

            var max = SettingsScrollViewer.ScrollableHeight;
            if (targetOffset > max)
            {
                targetOffset = max;
            }

            AnimateScrollTo(targetOffset, requestId);
        }

private void AnimateScrollTo(double targetOffset, int requestId)
{
    _scrollAnimationId++;
    var animationId = _scrollAnimationId;

    var startOffset = SettingsScrollViewer.VerticalOffset;
    var delta = targetOffset - startOffset;

    if (Math.Abs(delta) < 0.5)
    {
        SettingsScrollViewer.ScrollToVerticalOffset(targetOffset);

        if (requestId == _navScrollRequestId && animationId == _scrollAnimationId)
        {
            _targetNavTag = null; // 清除目標標記
        }
        return;
    }

    // 設置動畫標記,阻止 ScrollChanged 更新導航
    _isScrollAnimating = true;

    var durationMs = 320.0;
    var sw = Stopwatch.StartNew();

    var timer = new DispatcherTimer(DispatcherPriority.Render)
    {
        Interval = TimeSpan.FromMilliseconds(16),
    };

    timer.Tick += (_, _) =>
    {
        if (animationId != _scrollAnimationId)
        {
            timer.Stop();
            return;
        }

        var t = sw.Elapsed.TotalMilliseconds / durationMs;
        if (t >= 1)
        {
            SettingsScrollViewer.ScrollToVerticalOffset(targetOffset);
            timer.Stop();
            
            // 延遲更長時間再解除動畫標記,確保滾動完全穩定
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (animationId != _scrollAnimationId || requestId != _navScrollRequestId)
                {
                    return;
                }

                _isScrollAnimating = false;
                // 清除目標標記,恢復正常的滾動檢測
                _targetNavTag = null;
                // 滾動完成後,手動更新一次導航高亮(這次會正常計算)
                UpdateNavHighlightFromScroll();
            }), DispatcherPriority.ContextIdle);
            return;
        }

        var eased = EaseOutQuad(t);
        SettingsScrollViewer.ScrollToVerticalOffset(startOffset + (delta * eased));
    };

    timer.Start();
}

        private static double EaseOutQuad(double t)
        {
            if (t <= 0)
            {
                return 0;
            }

            if (t >= 1)
            {
                return 1;
            }

            return 1 - ((1 - t) * (1 - t));
        }

        private static string GetVersionJsonPath()
        {
            var preferred = Path.GetFullPath(Path.Combine(AppPaths.AppDirectory, "..", "version.json"));
            if (File.Exists(preferred))
            {
                return preferred;
            }

            var appLocal = Path.Combine(AppPaths.AppDirectory, "version.json");
            if (File.Exists(appLocal))
            {
                return appLocal;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "version.json"));
        }

        private void LoadApisFromVersionJson()
        {
            ApiItems.Clear();

            try
            {
                var path = GetVersionJsonPath();
                if (!File.Exists(path))
                {
                    return;
                }

                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);

                if (!doc.RootElement.TryGetProperty("APIs", out var apisElement) || apisElement.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                foreach (var apiProp in apisElement.EnumerateObject())
                {
                    var apiName = apiProp.Name;
                    var apiObj = apiProp.Value;

                    string? sunc = null;
                    string? unc = null;

                    if (apiObj.ValueKind == JsonValueKind.Object)
                    {
                        if (apiObj.TryGetProperty("sUNC", out var suncEl) && suncEl.ValueKind == JsonValueKind.String)
                        {
                            sunc = suncEl.GetString();
                        }

                        if (apiObj.TryGetProperty("UNC", out var uncEl) && uncEl.ValueKind == JsonValueKind.String)
                        {
                            unc = uncEl.GetString();
                        }
                    }

                    ApiItems.Add(new ApiEntry(apiName, sunc, unc));
                }
            }
            catch
            {
            }
        }

        private void ApplyInitialSelectedApi()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var selected = ConfigManager.Get(cfg, SelectedApiKey);

                if (string.IsNullOrWhiteSpace(selected))
                {
                    selected = ApiItems.FirstOrDefault()?.Name;
                }

                if (string.IsNullOrWhiteSpace(selected))
                {
                    CurrentApiText.Text = "-";
                    return;
                }

                SetSelectedApi(selected, persist: false);
            }
            catch
            {
                CurrentApiText.Text = "-";
            }
        }

        private void SetSelectedApi(string apiName, bool persist)
        {
            CurrentApiText.Text = apiName;

            if (!persist)
            {
                return;
            }

            try
            {
                var cfg = ConfigManager.ReadConfig();
                ConfigManager.Set(cfg, SelectedApiKey, apiName);
                ConfigManager.WriteConfig(cfg);
            }
            catch
            {
            }
        }

        private void ChooseApiButton_OnClick(object sender, RoutedEventArgs e)
        {
            ShowApiModal();
        }

        private void CloseApiModalButton_OnClick(object sender, RoutedEventArgs e)
        {
            HideApiModal();
        }

        private void ApiModalBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideApiModal();
        }

        private void ApiModalContent_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ApiTileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            if (btn.Tag is ApiEntry entry)
            {
                var newApi = entry.Name;
                var oldApi = (CurrentApiText?.Text ?? string.Empty).Trim();

                if (!string.Equals(oldApi, newApi, StringComparison.OrdinalIgnoreCase))
                {
                    var wasAttached = false;
                    try
                    {
                        if (SpashApiInvoker.IsRobloxProcessRunning() && SpashApiInvoker.IsInitializedForDebug())
                        {
                            wasAttached = API.IsAttached();
                        }
                        else
                        {
                            wasAttached = false;
                        }
                    }
                    catch
                    {
                        wasAttached = false;
                    }

                    if (wasAttached)
                    {
                        try
                        {
                            WaveToastService.Show(
                                LocalizationManager.T("WaveUI.Common.Info"),
                                LocalizationManager.T("WaveUI.Settings.APIs.Toast.RestartingRoblox"));
                        }
                        catch
                        {
                        }

                        try
                        {
                            RobloxRuntime.KillRoblox();
                        }
                        catch
                        {
                        }
                    }

                    SetSelectedApi(newApi, persist: true);

                    try
                    {
                        SpashApiInvoker.ResetForApiChange();
                    }
                    catch
                    {
                    }

                    if (wasAttached)
                    {
                        try
                        {
                            _ = RobloxRuntime.TryLaunchRoblox();
                        }
                        catch
                        {
                        }
                    }
                }
            }

            HideApiModal();
        }

        private void ReloadApp_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                });

                Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                WaveToastService.Show("Error", ex.Message);
            }
        }

        private void ShowApiModal()
        {
            if (_isApiModalAnimating)
            {
                return;
            }

            if (ApiModalOverlay.Visibility == Visibility.Visible)
            {
                return;
            }

            _isApiModalAnimating = true;

            ApiModalOverlay.Visibility = Visibility.Visible;
            ApiModalOverlay.BeginAnimation(OpacityProperty, null);
            ApiModalOverlay.Opacity = 0;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeIn.Completed += (_, _) =>
            {
                ApiModalOverlay.Opacity = 1;
                _isApiModalAnimating = false;
            };

            ApiModalOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void HideApiModal()
        {
            if (_isApiModalAnimating)
            {
                return;
            }

            if (ApiModalOverlay.Visibility != Visibility.Visible)
            {
                return;
            }

            _isApiModalAnimating = true;

            ApiModalOverlay.BeginAnimation(OpacityProperty, null);
            var start = ApiModalOverlay.Opacity;

            var fadeOut = new DoubleAnimation
            {
                From = start,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeOut.Completed += (_, _) =>
            {
                ApiModalOverlay.BeginAnimation(OpacityProperty, null);
                ApiModalOverlay.Opacity = 0;
                ApiModalOverlay.Visibility = Visibility.Collapsed;
                _isApiModalAnimating = false;
            };

            ApiModalOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
