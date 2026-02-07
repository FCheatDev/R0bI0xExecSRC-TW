using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
            public string Name { get; set; } = string.Empty;
            public string? Sunc { get; set; }
            public string? Unc { get; set; }
            public string SuncLine => string.IsNullOrEmpty(Sunc) ? "" : $"sUNC: {Sunc}";
            public string UncLine => string.IsNullOrEmpty(Unc) ? "" : $"UNC: {Unc}";
        }

        public sealed class FontOption
        {
            public FontOption(string name, FontFamily fontFamily, bool isDefault)
            {
                Name = name;
                FontFamily = fontFamily;
                IsDefault = isDefault;
                Value = isDefault ? string.Empty : name;
            }

            public string Name { get; }
            public FontFamily FontFamily { get; }
            public bool IsDefault { get; }
            public string Value { get; }
        }

        private void FontCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            if (FontCombo.SelectedItem is not FontOption item)
            {
                return;
            }

            var fontName = item.Value;

            ApplyUiFont(fontName);
            SaveConfigValue(UiFontKey, fontName);
        }

        private void FontCombo_OnDropDownOpened(object sender, EventArgs e)
        {
            if (sender is not ComboBox combo)
            {
                return;
            }

            var searchBox = combo.Template.FindName("FontSearchBox", combo) as TextBox;
            var placeholder = combo.Template.FindName("FontSearchPlaceholderText", combo) as TextBlock;
            _fontSearchText = string.Empty;

            if (searchBox != null)
            {
                if (!string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Text = string.Empty;
                }

                searchBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }), DispatcherPriority.Background);
            }

            if (placeholder != null)
            {
                placeholder.Text = LocalizationManager.T("WaveUI.Editor.Explorer.SearchHint");
            }

            ApplyFontSearchFilter();
        }

        private void FontSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _fontSearchText = (sender as TextBox)?.Text ?? string.Empty;
            ApplyFontSearchFilter();
        }

        private void ApplyFontSearchFilter()
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(FontItems);
                if (view == null)
                {
                    return;
                }

                view.Filter = FontFilterMatch;
                view.Refresh();
            }
            catch
            {
            }
        }

        private void ChangeKeyRowButton_OnClick(object sender, RoutedEventArgs e)
        {
            ShowChangeKeyModal();
        }

        private void ChangeKeyModalBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideChangeKeyModal();
        }

        private void ChangeKeyModalContent_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ChangeKeyCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            HideChangeKeyModal();
        }

        private void ChangeKeyConfirmButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var value = (ChangeKeyModalKeyBox?.Text ?? string.Empty).Trim();
                var cfg = ConfigManager.ReadConfig();
                ConfigManager.Set(cfg, GlobalKeyKey, value);
                ConfigManager.WriteConfig(cfg);
            }
            catch (Exception ex)
            {
                WaveToastService.Show(LocalizationManager.T("WaveUI.Common.Error"), ex.Message);
                return;
            }

            HideChangeKeyModal();
        }

        private void ShowChangeKeyModal()
        {
            if (_isChangeKeyModalAnimating || _isChangeKeyModalOpen)
            {
                return;
            }

            if (ChangeKeyModalOverlay == null || ChangeKeyModalScale == null || ChangeKeyModalTranslate == null)
            {
                return;
            }

            _isChangeKeyModalOpen = true;
            _isChangeKeyModalAnimating = true;

            try
            {
                var cfg = ConfigManager.ReadConfig();
                var existing = ConfigManager.Get(cfg, GlobalKeyKey) ?? string.Empty;
                if (ChangeKeyModalKeyBox != null)
                {
                    ChangeKeyModalKeyBox.Text = existing;
                }
            }
            catch
            {
            }

            ChangeKeyModalOverlay.Visibility = Visibility.Visible;
            ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, null);
            ChangeKeyModalOverlay.Opacity = 0;

            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ChangeKeyModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            ChangeKeyModalScale.ScaleX = 0.96;
            ChangeKeyModalScale.ScaleY = 0.96;
            ChangeKeyModalTranslate.Y = 8;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeIn.Completed += (_, _) =>
            {
                ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, null);
                ChangeKeyModalOverlay.Opacity = 1;
                _isChangeKeyModalAnimating = false;

                try
                {
                    if (ChangeKeyModalKeyBox != null)
                    {
                        ChangeKeyModalKeyBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ChangeKeyModalKeyBox.Focus();
                            ChangeKeyModalKeyBox.SelectAll();
                        }), DispatcherPriority.Background);
                    }
                }
                catch
                {
                }
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            var translateAnim = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, fadeIn);
            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            ChangeKeyModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private void HideChangeKeyModal()
        {
            if (_isChangeKeyModalAnimating || !_isChangeKeyModalOpen)
            {
                return;
            }

            if (ChangeKeyModalOverlay == null || ChangeKeyModalScale == null || ChangeKeyModalTranslate == null)
            {
                _isChangeKeyModalAnimating = false;
                _isChangeKeyModalOpen = false;
                return;
            }

            _isChangeKeyModalAnimating = true;

            ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, null);
            var fadeOut = new DoubleAnimation
            {
                From = ChangeKeyModalOverlay.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeOut.Completed += (_, _) =>
            {
                ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, null);
                ChangeKeyModalOverlay.Opacity = 0;
                ChangeKeyModalOverlay.Visibility = Visibility.Collapsed;
                _isChangeKeyModalAnimating = false;
                _isChangeKeyModalOpen = false;
            };

            var scaleAnim = new DoubleAnimation
            {
                From = ChangeKeyModalScale.ScaleX,
                To = 0.96,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            var translateAnim = new DoubleAnimation
            {
                From = ChangeKeyModalTranslate.Y,
                To = 8,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            ChangeKeyModalOverlay.BeginAnimation(OpacityProperty, fadeOut);
            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ChangeKeyModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            ChangeKeyModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private static void ApplySmallWaveFrameRate(int fps)
        {
            try
            {
                if (Application.Current == null)
                {
                    return;
                }

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is WaveMinimizeWindow wave)
                    {
                        wave.ApplyFrameRate(fps);
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private bool FontFilterMatch(object obj)
        {
            if (obj is not FontOption option)
            {
                return false;
            }

            var query = _fontSearchText;
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            if (option.IsDefault)
            {
                return true;
            }

            return option.Name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
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

        private void MultiInstanceCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = MultiInstanceCheckBox.IsChecked == true;
            SaveConfigValue(MultiInstanceKey, enabled.ToString().ToLowerInvariant());
            UpdateMultiInstanceValueText();
        }

        private void UnlimitedTabsCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = UnlimitedTabsCheckBox.IsChecked == true;
            SaveConfigValue(UnlimitedTabsKey, enabled.ToString().ToLowerInvariant());
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

        private void SmallWaveEffectCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            if (SmallWaveEffectCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var effect = (item.Tag as string) ?? WaveMinimizeWindow.EffectPulse;
            SaveConfigValue(SmallWaveEffectKey, effect);
            ApplySmallWaveEffect(effect);
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

        private void LoadFontItems()
        {
            try
            {
                FontItems.Clear();

                var defaultFamily = new FontFamily(App.DefaultWaveFontFamily);
                FontItems.Add(new FontOption("Default", defaultFamily, isDefault: true));

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var family in Fonts.SystemFontFamilies.OrderBy(font => font.Source))
                {
                    var name = family.Source?.Trim();
                    if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                    {
                        continue;
                    }

                    FontItems.Add(new FontOption(name, family, isDefault: false));
                }
            }
            catch
            {
            }
        }

        public ObservableCollection<FontOption> FontItems { get; } = new();
        public ObservableCollection<ApiEntry> ApiItems { get; } = new();

        public string AppDirectoryPath => AppPaths.AppDirectory;

        public string ApisDirectoryPath => Path.Combine(AppPaths.AppDirectory, "APIs");

        public string MonacoDirectoryPath => Path.Combine(AppPaths.AppDirectory, "Assets", "WaveUI", "Monaco", "2025");

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
        private const string StartupKey = "WaveUI_start_on_boot";
        private const string DiscordRpcKey = DiscordRpcService.EnabledConfigKey;
        private const string OpacityKey = "opacity";
        private const string SmallWaveOpacityKey = "WaveUI_small_wave_opacity";
        private const string SmallWaveEffectKey = "WaveUI_small_wave_effect";
        private const string SmallWaveFpsKey = "WaveUI_small_wave_fps";
        private const string UiFontKey = "font";
        private const string SkipLoadAppKey = "WaveUI_skip_load_app";
        private const string MultiInstanceKey = "allow_multi_instance";
        private const string UnlimitedTabsKey = "unlimited_tabs";

        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryValueName = "Wave";
        private const string GlobalKeyKey = "key";

        private bool _isApiModalAnimating;
        private bool _isChangeKeyModalAnimating;
        private bool _isChangeKeyModalOpen;
        private bool _suppressSettingEvents;
        private int _scrollAnimationId;
        private int _navScrollRequestId;
        private DispatcherOperation? _pendingNavScrollOperation;
        private bool _initialized;
        private bool _isOpacityEditing;
        private bool _isSmallWaveOpacityEditing;
        private string _fontSearchText = string.Empty;
        private string _settingsSearchText = string.Empty;
        private readonly List<TextBlock> _settingsSearchTargets = new();
        private readonly Dictionary<TextBlock, string> _settingsSearchBaseText = new();

        private const int SmallWaveFpsDefault = 60;
        private const int SmallWaveFpsMin = 30;
        private const int SmallWaveFpsMax = 120;

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

        private static void ApplySmallWaveOpacity(double opacity)
        {
            try
            {
                if (Application.Current == null)
                {
                    return;
                }

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is WaveMinimizeWindow wave)
                    {
                        wave.ApplyOpacity(opacity);
                        break;
                    }
                }
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

            ApplyLanguage();

            EnsureApiDebugTimer();
            UpdateApiDebugStatus(force: true);
        }

        private void SettingsSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _settingsSearchText = (sender as TextBox)?.Text ?? string.Empty;
            ApplySettingsSearchHighlight();
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
            SmallWaveSection.Text = LocalizationManager.T("WaveUI.Settings.Section.SmallWave");
            AppearanceSection.Text = LocalizationManager.T("WaveUI.Settings.Section.Appearance");
            DataSection.Text = LocalizationManager.T("WaveUI.Settings.Section.Data");
            ApisSection.Text = LocalizationManager.T("WaveUI.Settings.Section.APIs");
            ApiDebugSection.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.Title");

            TopmostTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Topmost.Title");
            TopmostDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Topmost.Desc");
            BootTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Boot.Title");
            BootDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Boot.Desc");
            DiscordRpcTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.DiscordRpc.Title");
            DiscordRpcDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.DiscordRpc.Desc");

            ChangeKeyTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.ChangeKey.Title");
            ChangeKeyDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.ChangeKey.Desc");
            ChangeKeyActionText.Text = LocalizationManager.T("WaveUI.Settings.Application.ChangeKey.Button");

            ChangeKeyModalTitleText.Text = LocalizationManager.T("WaveUI.Settings.ChangeKeyModal.Title");
            ChangeKeyModalKeyLabelText.Text = LocalizationManager.T("WaveUI.Settings.ChangeKeyModal.KeyLabel");
            ChangeKeyCancelText.Text = LocalizationManager.T("WaveUI.Settings.ChangeKeyModal.Cancel");
            ChangeKeyConfirmText.Text = LocalizationManager.T("WaveUI.Settings.ChangeKeyModal.Confirm");

            OpacityTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.Opacity.Title");
            OpacityDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.Opacity.Desc");

            ReloadAppTitleText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Title");
            ReloadAppDescText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Desc");
            ReloadAppButtonText.Text = LocalizationManager.T("WaveUI.Settings.Application.ReloadApp.Button");

            LanguageTitleText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Language.Title");
            LanguageDescText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Language.Desc");
            ThemeTitleText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Theme.Title");
            ThemeDescText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Theme.Desc");
            FontTitleText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Font.Title");
            FontDescText.Text = LocalizationManager.T("WaveUI.Settings.Appearance.Font.Desc");

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

            OpenLogFolderTitleText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenLogFolder.Title");
            OpenLogFolderDescText.Text = LocalizationManager.T("WaveUI.Settings.Data.OpenLogFolder.Desc");
            OpenLogFolderButtonText.Text = LocalizationManager.T("WaveUI.Common.Open");

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

            MultiInstanceTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.MultiInstance.Title");
            MultiInstanceDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.MultiInstance.Desc");
            UpdateMultiInstanceValueText();

            UnlimitedTabsTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.UnlimitedTabs.Title");
            UnlimitedTabsDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.UnlimitedTabs.Desc");

            TestPromptTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Title");
            TestPromptDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Desc");
            TestPromptButtonText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.TestPrompt.Button");

            NavApplicationTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Application");
            NavApplicationTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Application");
            NavSmallWaveTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.SmallWave");
            NavSmallWaveTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.SmallWave");
            NavAppearanceTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Appearance");
            NavAppearanceTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Appearance");
            NavDataTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Data");
            NavDataTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Data");
            NavApisTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.APIs");
            NavApisTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.APIs");

            NavDebugTextInactive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Debug");
            NavDebugTextActive.Text = LocalizationManager.T("WaveUI.Settings.Nav.Debug");

            SmallWaveOpacityTitleText.Text = LocalizationManager.T("WaveUI.Settings.SmallWave.Opacity.Title");
            SmallWaveOpacityDescText.Text = LocalizationManager.T("WaveUI.Settings.SmallWave.Opacity.Desc");
            SmallWaveEffectTitleText.Text = LocalizationManager.T("WaveUI.Settings.SmallWave.Effect.Title");
            SmallWaveEffectDescText.Text = LocalizationManager.T("WaveUI.Settings.SmallWave.Effect.Desc");
            SmallWaveEffectPulseItem.Content = LocalizationManager.T("WaveUI.Settings.SmallWave.Effect.Pulse");
            SmallWaveEffectFadeItem.Content = LocalizationManager.T("WaveUI.Settings.SmallWave.Effect.Fade");

            SmallWaveFpsTitleText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.SmallWaveFps.Title");
            SmallWaveFpsDescText.Text = LocalizationManager.T("WaveUI.Settings.APIs.Debug.SmallWaveFps.Desc");

            SettingsSearchPlaceholderText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.SearchHint");

            RefreshSettingsSearchBaseText();
            ApplySettingsSearchHighlight();

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
            return;
        }

        private void LoadAndApplySettings()
        {
            _suppressSettingEvents = true;
            try
            {
                LoadFontItems();
                var cfg = ConfigManager.ReadConfig();

                var topmost = ParseBool(ConfigManager.Get(cfg, TopmostKey), fallback: false);
                TopmostCheckBox.IsChecked = topmost;
                ApplyTopmost(topmost);

                var startup = ParseBool(ConfigManager.Get(cfg, StartupKey), fallback: IsStartupEnabledInRegistry());
                BootCheckBox.IsChecked = startup;

                var discordRpc = ParseBool(ConfigManager.Get(cfg, DiscordRpcKey), fallback: true);
                DiscordRpcCheckBox.IsChecked = discordRpc;

                var opacity = ParseDouble(ConfigManager.Get(cfg, OpacityKey), fallback: 1.0);
                opacity = Clamp(opacity, 0.1, 1.0);
                OpacitySlider.Value = opacity;
                OpacityValueText.Text = opacity.ToString("0.0");
                if (OpacityValueEditBox != null)
                {
                    OpacityValueEditBox.Text = opacity.ToString("0.0");
                }
                ApplyOpacity(opacity);

                var smallWaveOpacity = ParseDouble(ConfigManager.Get(cfg, SmallWaveOpacityKey), fallback: 1.0);
                smallWaveOpacity = Clamp(smallWaveOpacity, 0.1, 1.0);
                SmallWaveOpacitySlider.Value = smallWaveOpacity;
                SmallWaveOpacityValueText.Text = smallWaveOpacity.ToString("0.0");
                if (SmallWaveOpacityValueEditBox != null)
                {
                    SmallWaveOpacityValueEditBox.Text = smallWaveOpacity.ToString("0.0");
                }
                ApplySmallWaveOpacity(smallWaveOpacity);

                var effect = ConfigManager.Get(cfg, SmallWaveEffectKey) ?? WaveMinimizeWindow.EffectPulse;
                SelectSmallWaveEffectCombo(effect);
                ApplySmallWaveEffect(effect);

                var smallWaveFps = ParseInt(ConfigManager.Get(cfg, SmallWaveFpsKey), fallback: SmallWaveFpsDefault);
                smallWaveFps = ClampInt(smallWaveFps, SmallWaveFpsMin, SmallWaveFpsMax);
                SmallWaveFpsSlider.Value = smallWaveFps;
                SmallWaveFpsValueText.Text = smallWaveFps.ToString(CultureInfo.InvariantCulture);
                ApplySmallWaveFrameRate(smallWaveFps);

                var lang = (ConfigManager.Get(cfg, "language") ?? "zh").Trim().ToLowerInvariant();
                SelectLanguageCombo(lang);

                var theme = (ConfigManager.Get(cfg, "theme") ?? string.Empty).Trim();
                SelectThemeCombo(theme);

                var fontName = ConfigManager.Get(cfg, UiFontKey);
                SelectFontCombo(fontName);
                ApplyUiFont(fontName);

                var skipLoad = ParseBool(ConfigManager.Get(cfg, SkipLoadAppKey), fallback: false);
                SkipLoadAppCheckBox.IsChecked = skipLoad;

                var allowMultiInstance = ParseBool(ConfigManager.Get(cfg, MultiInstanceKey), fallback: false);
                MultiInstanceCheckBox.IsChecked = allowMultiInstance;
                UpdateMultiInstanceValueText();

                var unlimitedTabs = ParseBool(ConfigManager.Get(cfg, UnlimitedTabsKey), fallback: false);
                UnlimitedTabsCheckBox.IsChecked = unlimitedTabs;
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

        private static int ParseInt(string? value, int fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (int.TryParse(value.Trim(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static int ClampInt(int v, int min, int max)
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
                    var clamped = Clamp(opacity, 0.1, 1.0);
                    w.Opacity = clamped;
                    if (w is MainWindow mw)
                    {
                        mw.TargetOpacity = clamped;
                    }
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

            var matchTheme = theme.Trim();
            if (string.Equals(matchTheme, "Wave", StringComparison.OrdinalIgnoreCase))
            {
                matchTheme = "WaveUI-2025";
            }

            foreach (var item in ThemeCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Content as string, matchTheme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeCombo.SelectedItem = cbi;
                    return;
                }
            }
        }

        private void SelectFontCombo(string? fontName)
        {
            var target = (fontName ?? string.Empty).Trim();
            var useDefault = string.IsNullOrWhiteSpace(target);

            var selected = useDefault
                ? FontItems.FirstOrDefault(item => item.IsDefault)
                : FontItems.FirstOrDefault(item => string.Equals(item.Name, target, StringComparison.OrdinalIgnoreCase));

            if (selected != null)
            {
                FontCombo.SelectedItem = selected;
                return;
            }

            var fallback = FontItems.FirstOrDefault(item => item.IsDefault) ?? FontItems.FirstOrDefault();
            if (fallback != null)
            {
                FontCombo.SelectedItem = fallback;
            }
        }

        private void SelectSmallWaveEffectCombo(string effect)
        {
            var target = (effect ?? WaveMinimizeWindow.EffectPulse).Trim();

            foreach (var item in SmallWaveEffectCombo.Items)
            {
                if (item is not ComboBoxItem cbi)
                {
                    continue;
                }

                if (string.Equals(cbi.Tag as string, target, StringComparison.OrdinalIgnoreCase))
                {
                    SmallWaveEffectCombo.SelectedItem = cbi;
                    return;
                }
            }

            SmallWaveEffectCombo.SelectedItem = SmallWaveEffectPulseItem;
        }

        private static void ApplyUiFont(string? fontName)
        {
            try
            {
                global::Executor.App.ApplyWaveFont(fontName);
            }
            catch
            {
            }
        }

        private static void ApplySmallWaveEffect(string? effect)
        {
            try
            {
                if (Application.Current == null)
                {
                    return;
                }

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is WaveMinimizeWindow wave)
                    {
                        wave.ApplyEffect(effect);
                        break;
                    }
                }
            }
            catch
            {
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

        private void DiscordRpcCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var enabled = DiscordRpcCheckBox.IsChecked == true;
            SaveConfigValue(DiscordRpcKey, enabled.ToString().ToLowerInvariant());

            try
            {
                if (!enabled)
                {
                    DiscordRpcService.Shutdown();
                    return;
                }

                var cfg = ConfigManager.ReadConfig();
                var theme = ConfigManager.Get(cfg, "theme") ?? string.Empty;
                DiscordRpcService.ApplyTheme(theme);
            }
            catch
            {
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
            if (OpacityValueEditBox != null)
            {
                OpacityValueEditBox.Text = opacity.ToString("0.0");
            }
            ApplyOpacity(opacity);
            SaveConfigValue(OpacityKey, opacity.ToString("0.0"));
        }

        private void SmallWaveFpsSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || SmallWaveFpsSlider == null || SmallWaveFpsValueText == null)
            {
                return;
            }

            if (_suppressSettingEvents)
            {
                return;
            }

            var fps = ClampInt((int)Math.Round(SmallWaveFpsSlider.Value), SmallWaveFpsMin, SmallWaveFpsMax);
            SmallWaveFpsValueText.Text = fps.ToString(CultureInfo.InvariantCulture);
            ApplySmallWaveFrameRate(fps);
            SaveConfigValue(SmallWaveFpsKey, fps.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshSettingsSearchBaseText()
        {
            EnsureSettingsSearchTargets();
            _settingsSearchBaseText.Clear();

            foreach (var target in _settingsSearchTargets)
            {
                if (target == null)
                {
                    continue;
                }

                _settingsSearchBaseText[target] = target.Text ?? string.Empty;
            }
        }

        private void EnsureSettingsSearchTargets()
        {
            if (_settingsSearchTargets.Count > 0)
            {
                return;
            }

            AddSettingsSearchTarget(SettingsRightTitleText);
            AddSettingsSearchTarget(SettingsRightSubtitleText);
            AddSettingsSearchTarget(ApplicationSection);
            AddSettingsSearchTarget(TopmostTitleText);
            AddSettingsSearchTarget(TopmostDescText);
            AddSettingsSearchTarget(SkipLoadAppTitleText);
            AddSettingsSearchTarget(SkipLoadAppDescText);
            AddSettingsSearchTarget(BootTitleText);
            AddSettingsSearchTarget(BootDescText);
            AddSettingsSearchTarget(DiscordRpcTitleText);
            AddSettingsSearchTarget(DiscordRpcDescText);
            AddSettingsSearchTarget(OpacityTitleText);
            AddSettingsSearchTarget(OpacityDescText);
            AddSettingsSearchTarget(ReloadAppTitleText);
            AddSettingsSearchTarget(ReloadAppDescText);
            AddSettingsSearchTarget(ReloadAppButtonText);

            AddSettingsSearchTarget(SmallWaveSection);
            AddSettingsSearchTarget(SmallWaveOpacityTitleText);
            AddSettingsSearchTarget(SmallWaveOpacityDescText);
            AddSettingsSearchTarget(SmallWaveEffectTitleText);
            AddSettingsSearchTarget(SmallWaveEffectDescText);

            AddSettingsSearchTarget(AppearanceSection);
            AddSettingsSearchTarget(LanguageTitleText);
            AddSettingsSearchTarget(LanguageDescText);
            AddSettingsSearchTarget(ThemeTitleText);
            AddSettingsSearchTarget(ThemeDescText);
            AddSettingsSearchTarget(FontTitleText);
            AddSettingsSearchTarget(FontDescText);

            AddSettingsSearchTarget(DataSection);
            AddSettingsSearchTarget(AppDirectoryTitleText);
            AddSettingsSearchTarget(ApisDirectoryTitleText);
            AddSettingsSearchTarget(MonacoTitleText);
            AddSettingsSearchTarget(WaveUiImagesTitleText);
            AddSettingsSearchTarget(OpenConfigFolderTitleText);
            AddSettingsSearchTarget(OpenConfigFolderDescText);
            AddSettingsSearchTarget(OpenConfigFolderButtonText);
            AddSettingsSearchTarget(OpenConfigFileTitleText);
            AddSettingsSearchTarget(OpenConfigFileDescText);
            AddSettingsSearchTarget(OpenConfigFileButtonText);
            AddSettingsSearchTarget(OpenVersionFileTitleText);
            AddSettingsSearchTarget(OpenVersionFileDescText);
            AddSettingsSearchTarget(OpenVersionFileButtonText);
            AddSettingsSearchTarget(OpenLogFolderTitleText);
            AddSettingsSearchTarget(OpenLogFolderDescText);
            AddSettingsSearchTarget(OpenLogFolderButtonText);

            AddSettingsSearchTarget(ApisSection);
            AddSettingsSearchTarget(ChooseApisTitleText);
            AddSettingsSearchTarget(ApiDebugSection);
            AddSettingsSearchTarget(MultiInstanceTitleText);
            AddSettingsSearchTarget(MultiInstanceDescText);
            AddSettingsSearchTarget(MultiInstanceValueText);
            AddSettingsSearchTarget(UnlimitedTabsTitleText);
            AddSettingsSearchTarget(UnlimitedTabsDescText);
            AddSettingsSearchTarget(SmallWaveFpsTitleText);
            AddSettingsSearchTarget(SmallWaveFpsDescText);
            AddSettingsSearchTarget(TestPromptTitleText);
            AddSettingsSearchTarget(TestPromptDescText);
            AddSettingsSearchTarget(TestPromptButtonText);
        }

        private void UpdateMultiInstanceValueText()
        {
            if (MultiInstanceValueText == null)
            {
                return;
            }

            var allowed = MultiInstanceCheckBox?.IsChecked == true;
            var key = allowed
                ? "WaveUI.Settings.APIs.Debug.MultiInstance.Allowed"
                : "WaveUI.Settings.APIs.Debug.MultiInstance.NotAllowed";
            MultiInstanceValueText.Text = LocalizationManager.T(key);
        }

        private void AddSettingsSearchTarget(TextBlock? target)
        {
            if (target == null)
            {
                return;
            }

            _settingsSearchTargets.Add(target);
        }

        private void ApplySettingsSearchHighlight()
        {
            EnsureSettingsSearchTargets();

            var query = _settingsSearchText?.Trim() ?? string.Empty;
            foreach (var target in _settingsSearchTargets)
            {
                if (!_settingsSearchBaseText.TryGetValue(target, out var baseText))
                {
                    baseText = target.Text ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    ResetSettingsSearchHighlight(target, baseText);
                    continue;
                }

                HighlightTextBlock(target, baseText, query);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                ScrollToSettingsSearchMatch(query);
            }
        }

        private void ScrollToSettingsSearchMatch(string query)
        {
            if (SettingsScrollViewer == null)
            {
                return;
            }

            EnsureSettingsSearchTargets();

            foreach (var target in _settingsSearchTargets)
            {
                if (target == null)
                {
                    continue;
                }

                if (!_settingsSearchBaseText.TryGetValue(target, out var baseText))
                {
                    baseText = target.Text ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(baseText))
                {
                    continue;
                }

                if (baseText.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!TryGetSearchTargetOffset(target, out var targetOffset))
                {
                    continue;
                }

                AnimateSearchScrollTo(targetOffset);
                break;
            }
        }

        private bool TryGetSearchTargetOffset(FrameworkElement target, out double targetOffset)
        {
            targetOffset = 0;
            if (SettingsScrollViewer == null)
            {
                return false;
            }

            try
            {
                var p = target.TransformToAncestor(SettingsScrollViewer).Transform(new Point(0, 0));
                targetOffset = SettingsScrollViewer.VerticalOffset + p.Y;
            }
            catch
            {
                return false;
            }

            var centered = targetOffset + (target.ActualHeight / 2.0) - (SettingsScrollViewer.ViewportHeight / 2.0);
            targetOffset = Clamp(centered, 0, SettingsScrollViewer.ScrollableHeight);
            return true;
        }

        private void AnimateSearchScrollTo(double targetOffset)
        {
            if (SettingsScrollViewer == null)
            {
                return;
            }

            _scrollAnimationId++;
            var animationId = _scrollAnimationId;

            var startOffset = SettingsScrollViewer.VerticalOffset;
            var delta = targetOffset - startOffset;

            if (Math.Abs(delta) < 0.5)
            {
                SettingsScrollViewer.ScrollToVerticalOffset(targetOffset);
                return;
            }

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

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (animationId != _scrollAnimationId)
                        {
                            return;
                        }
                    }), DispatcherPriority.ContextIdle);
                    return;
                }

                var eased = EaseOutQuad(t);
                SettingsScrollViewer.ScrollToVerticalOffset(startOffset + (delta * eased));
            };

            timer.Start();
        }

        private static void ResetSettingsSearchHighlight(TextBlock target, string baseText)
        {
            target.Inlines.Clear();
            target.Text = baseText;
        }

        private static void HighlightTextBlock(TextBlock target, string baseText, string query)
        {
            if (string.IsNullOrEmpty(baseText) || string.IsNullOrEmpty(query))
            {
                target.Inlines.Clear();
                target.Text = baseText;
                return;
            }

            var index = 0;
            var comparison = StringComparison.OrdinalIgnoreCase;
            var highlightBrush = new SolidColorBrush(Color.FromRgb(0x1D, 0xA1, 0xF2));
            highlightBrush.Freeze();

            target.Inlines.Clear();
            target.Text = string.Empty;

            while (true)
            {
                var match = baseText.IndexOf(query, index, comparison);
                if (match < 0)
                {
                    break;
                }

                if (match > index)
                {
                    target.Inlines.Add(new Run(baseText.Substring(index, match - index)));
                }

                var highlightRun = new Run(baseText.Substring(match, query.Length))
                {
                    Foreground = highlightBrush,
                };
                target.Inlines.Add(highlightRun);

                index = match + query.Length;
            }

            if (index < baseText.Length)
            {
                target.Inlines.Add(new Run(baseText.Substring(index)));
            }
        }

        private void OpacityValueText_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                return;
            }

            e.Handled = true;
            BeginOpacityEdit();
        }

        private void OpacityValueEditBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                EndOpacityEdit(commit: true);
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndOpacityEdit(commit: false);
            }
        }

        private void OpacityValueEditBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            EndOpacityEdit(commit: true);
        }

        private void BeginOpacityEdit()
        {
            if (_isOpacityEditing || OpacityValueEditBox == null || OpacityValueText == null)
            {
                return;
            }

            _isOpacityEditing = true;
            OpacityValueEditBox.Text = OpacityValueText.Text;
            OpacityValueEditBox.Visibility = Visibility.Visible;
            OpacityValueText.Visibility = Visibility.Collapsed;

            OpacityValueEditBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                OpacityValueEditBox.Focus();
                OpacityValueEditBox.SelectAll();
            }), DispatcherPriority.Background);
        }

        private void EndOpacityEdit(bool commit)
        {
            if (!_isOpacityEditing || OpacityValueEditBox == null || OpacityValueText == null || OpacitySlider == null)
            {
                return;
            }

            _isOpacityEditing = false;

            if (commit)
            {
                var currentOpacity = Clamp(OpacitySlider.Value, 0.1, 1.0);
                var opacity = currentOpacity;

                if (TryParseOpacity(OpacityValueEditBox.Text, out var parsed))
                {
                    opacity = Clamp(parsed, 0.1, 1.0);
                }

                try
                {
                    _suppressSettingEvents = true;
                    OpacitySlider.Value = opacity;
                }
                finally
                {
                    _suppressSettingEvents = false;
                }

                OpacityValueText.Text = opacity.ToString("0.0");
                OpacityValueEditBox.Text = opacity.ToString("0.0");
                ApplyOpacity(opacity);
                SaveConfigValue(OpacityKey, opacity.ToString("0.0"));
            }

            OpacityValueEditBox.Visibility = Visibility.Collapsed;
            OpacityValueText.Visibility = Visibility.Visible;
        }

        private void SmallWaveOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || SmallWaveOpacitySlider == null || SmallWaveOpacityValueText == null)
            {
                return;
            }

            if (_suppressSettingEvents)
            {
                return;
            }

            var opacity = Clamp(SmallWaveOpacitySlider.Value, 0.1, 1.0);
            SmallWaveOpacityValueText.Text = opacity.ToString("0.0");
            if (SmallWaveOpacityValueEditBox != null)
            {
                SmallWaveOpacityValueEditBox.Text = opacity.ToString("0.0");
            }
            ApplySmallWaveOpacity(opacity);
            SaveConfigValue(SmallWaveOpacityKey, opacity.ToString("0.0"));
        }

        private void SmallWaveOpacityValueText_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                return;
            }

            e.Handled = true;
            BeginSmallWaveOpacityEdit();
        }

        private void SmallWaveOpacityValueEditBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                EndSmallWaveOpacityEdit(commit: true);
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndSmallWaveOpacityEdit(commit: false);
            }
        }

        private void SmallWaveOpacityValueEditBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            EndSmallWaveOpacityEdit(commit: true);
        }

        private void BeginSmallWaveOpacityEdit()
        {
            if (_isSmallWaveOpacityEditing || SmallWaveOpacityValueEditBox == null || SmallWaveOpacityValueText == null)
            {
                return;
            }

            _isSmallWaveOpacityEditing = true;
            SmallWaveOpacityValueEditBox.Text = SmallWaveOpacityValueText.Text;
            SmallWaveOpacityValueEditBox.Visibility = Visibility.Visible;
            SmallWaveOpacityValueText.Visibility = Visibility.Collapsed;

            SmallWaveOpacityValueEditBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                SmallWaveOpacityValueEditBox.Focus();
                SmallWaveOpacityValueEditBox.SelectAll();
            }), DispatcherPriority.Background);
        }

        private void EndSmallWaveOpacityEdit(bool commit)
        {
            if (!_isSmallWaveOpacityEditing || SmallWaveOpacityValueEditBox == null || SmallWaveOpacityValueText == null || SmallWaveOpacitySlider == null)
            {
                return;
            }

            _isSmallWaveOpacityEditing = false;

            if (commit)
            {
                var currentOpacity = Clamp(SmallWaveOpacitySlider.Value, 0.1, 1.0);
                var opacity = currentOpacity;

                if (TryParseOpacity(SmallWaveOpacityValueEditBox.Text, out var parsed))
                {
                    opacity = Clamp(parsed, 0.1, 1.0);
                }

                try
                {
                    _suppressSettingEvents = true;
                    SmallWaveOpacitySlider.Value = opacity;
                }
                finally
                {
                    _suppressSettingEvents = false;
                }

                SmallWaveOpacityValueText.Text = opacity.ToString("0.0");
                SmallWaveOpacityValueEditBox.Text = opacity.ToString("0.0");
                ApplySmallWaveOpacity(opacity);
                SaveConfigValue(SmallWaveOpacityKey, opacity.ToString("0.0"));
            }

            SmallWaveOpacityValueEditBox.Visibility = Visibility.Collapsed;
            SmallWaveOpacityValueText.Visibility = Visibility.Visible;
        }

        private static bool TryParseOpacity(string? raw, out double opacity)
        {
            opacity = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out opacity)
                   || double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out opacity);
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

        private void OpenLogFolder_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFolder(ResolveLogDirectory());
        }

        private static string ResolveLogDirectory()
        {
            try
            {
                var logPath = Logger.LogFilePath;
                if (!string.IsNullOrWhiteSpace(logPath))
                {
                    var dir = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
            }

            return Path.Combine(AppPaths.AppDirectory, "ax-log");
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
    else if (string.Equals(tag, "SmallWave", StringComparison.OrdinalIgnoreCase))
    {
        target = SmallWaveSection;
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
        return;
    }

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

                    ApiItems.Add(new ApiEntry 
                    { 
                        Name = apiName, 
                        Sunc = sunc, 
                        Unc = unc 
                    });
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
