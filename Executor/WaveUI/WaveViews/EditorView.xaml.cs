using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Executor;
using Executor.WaveUI;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace Executor.WaveUI.WaveViews
{
    public partial class EditorView : UserControl
    {
        private readonly Action<string> _toast;
        private bool _isExplorerOpen = true;

        private bool _monacoInitialized;
        private const string MonacoHostName = "monaco";

        public bool IsMonacoReady => MonacoView?.CoreWebView2 != null;

        private const int MaxTabs = 30;
        private const string UnlimitedTabsKey = "unlimited_tabs";
        private const string ClosePromptPreferenceKey = "WaveUI_close_unsaved_tabs";
        private const string TabsStateFileName = "editor_tabs.json";
        public ObservableCollection<TabEntry> Tabs { get; } = new();
        private TabEntry _activeTab = null!;
        private ClosePromptPreference _closePromptPreference = ClosePromptPreference.Ask;
        private bool _isClosePromptOpen;
        private bool _isClosePromptAnimating;
        private TaskCompletionSource<ClosePromptResult>? _closePromptTcs;

        private const double SavedScriptsExpandedAngle = 90;
        private const double SavedScriptsCollapsedAngle = 0;
        private const double SavedScriptsMaxHeight = 260;
        private const double SavedScriptsCollapsedTranslateY = -8;
        private bool _isSavedScriptsExpanded;
        private bool _isSavedScriptsAnimating;
        private bool _isNewScriptModalOpen;
        private bool _isNewScriptModalAnimating;
        private readonly string _workspaceDirectory;
        private readonly string _tabsStatePath;
        private readonly DispatcherTimer _workspaceRefreshTimer;
        private readonly DispatcherTimer _savedScriptsSearchTimer;
        private FileSystemWatcher? _workspaceWatcher;
        private string _savedScriptsSearchText = string.Empty;

        public ObservableCollection<WorkspaceScriptEntry> WorkspaceScripts { get; } = new();

        private const double ExplorerWidth = 280;
        private const double ExplorerGap = 10;
        private static readonly Thickness ExplorerOpenMargin = new(0, 0, ExplorerWidth + ExplorerGap, 0);
        private static readonly Thickness ExplorerClosedMargin = new(0, 0, 0, 0);

        public EditorView(Action<string> toast)
        {
            InitializeComponent();
            _toast = toast;

            _workspaceDirectory = ResolveWorkspaceDirectory();
            _tabsStatePath = Path.Combine(_workspaceDirectory, TabsStateFileName);
            _closePromptPreference = LoadClosePromptPreference();
            _isSavedScriptsExpanded = false;

            _workspaceRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _workspaceRefreshTimer.Tick += (_, _) =>
            {
                _workspaceRefreshTimer.Stop();
                LoadWorkspaceScripts();
            };

            _savedScriptsSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _savedScriptsSearchTimer.Tick += (_, _) =>
            {
                _savedScriptsSearchTimer.Stop();
                ApplySavedScriptsFilter();
            };

            DataContext = this;

            ApplyExplorerState(animated: false);
            ApplySavedScriptsState(animated: false);

            InitializeTabs();

            Loaded += EditorView_OnLoaded;
            Unloaded += EditorView_OnUnloaded;
        }

        private async void EditorView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            StopWorkspaceWatcher();

            _workspaceRefreshTimer.Stop();
            _savedScriptsSearchTimer.Stop();

            try
            {
                await SaveTabsStateAsync();
            }
            catch
            {
            }
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

            if (ExecuteButtonText != null)
            {
                ExecuteButtonText.Text = LocalizationManager.T("WaveUI.Editor.Buttons.Execute");
            }

            if (ClearButtonText != null)
            {
                ClearButtonText.Text = LocalizationManager.T("WaveUI.Editor.Buttons.Clear");
            }

            if (OpenButtonText != null)
            {
                OpenButtonText.Text = LocalizationManager.T("WaveUI.Editor.Buttons.Open");
            }

            if (SaveButtonText != null)
            {
                SaveButtonText.Text = LocalizationManager.T("WaveUI.Editor.Buttons.Save");
            }

            if (KillRobloxButtonText != null)
            {
                KillRobloxButtonText.Text = LocalizationManager.T("WaveUI.Editor.Buttons.KillRoblox");
            }

            if (ExplorerTitleText != null)
            {
                ExplorerTitleText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.Title");
            }

            if (ExplorerSubtitleText != null)
            {
                ExplorerSubtitleText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.Subtitle");
            }

            if (ExplorerSavedScriptsText != null)
            {
                ExplorerSavedScriptsText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.SavedScripts");
            }

            if (SavedScriptsSearchPlaceholderText != null)
            {
                SavedScriptsSearchPlaceholderText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.SearchHint");
            }

            if (NewScriptTitleText != null)
            {
                NewScriptTitleText.Text = LocalizationManager.T("WaveUI.Editor.NewScript.Title");
            }

            if (NewScriptNameLabelText != null)
            {
                NewScriptNameLabelText.Text = LocalizationManager.T("WaveUI.Editor.NewScript.NameLabel");
            }

            if (NewScriptContentLabelText != null)
            {
                NewScriptContentLabelText.Text = LocalizationManager.T("WaveUI.Editor.NewScript.ContentLabel");
            }

            if (NewScriptCancelText != null)
            {
                NewScriptCancelText.Text = LocalizationManager.T("WaveUI.Editor.NewScript.Cancel");
            }

            if (NewScriptCreateText != null)
            {
                NewScriptCreateText.Text = LocalizationManager.T("WaveUI.Editor.NewScript.Create");
            }

            if (ClosePromptTitleText != null)
            {
                ClosePromptTitleText.Text = LocalizationManager.T("WaveUI.Editor.ClosePrompt.Title");
            }

            if (ClosePromptMessageText != null)
            {
                ClosePromptMessageText.Text = LocalizationManager.T("WaveUI.Editor.ClosePrompt.Message");
            }

            if (ClosePromptRememberText != null)
            {
                ClosePromptRememberText.Text = LocalizationManager.T("WaveUI.Editor.ClosePrompt.Remember");
            }

            if (ClosePromptCancelText != null)
            {
                ClosePromptCancelText.Text = LocalizationManager.T("WaveUI.Editor.ClosePrompt.Cancel");
            }

            if (ClosePromptConfirmText != null)
            {
                ClosePromptConfirmText.Text = LocalizationManager.T("WaveUI.Editor.ClosePrompt.Confirm");
            }

            UpdateSavedScriptsEmptyState();
        }

        private async System.Threading.Tasks.Task CloseAllUnpinnedTabsAsync()
        {
            if (Tabs.Count == 0)
            {
                return;
            }

            EndRenameAll(commit: true);

            var tabsToClose = Tabs.Where(t => !t.IsPinned).ToList();
            if (tabsToClose.Count == 0)
            {
                return;
            }

            if (tabsToClose.Count == Tabs.Count)
            {
                tabsToClose.Remove(_activeTab);
                if (tabsToClose.Count == 0)
                {
                    return;
                }
            }

            if (await ShouldCancelTabsCloseAsync(tabsToClose))
            {
                return;
            }

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }
            }
            catch
            {
            }

            foreach (var tab in tabsToClose)
            {
                Tabs.Remove(tab);
            }

            if (!Tabs.Contains(_activeTab))
            {
                _activeTab.IsActive = false;
                _activeTab = Tabs[0];
                _activeTab.IsActive = true;

                try
                {
                    if (MonacoView?.CoreWebView2 != null)
                    {
                        await SetEditorTextAsync(_activeTab.Content);
                    }
                }
                catch
                {
                }
            }

            try
            {
                await SaveTabsStateAsync();
            }
            catch
            {
            }
        }

        private void MonacoView_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            EndRenameAll(commit: true);
        }

        private void StartWorkspaceWatcher()
        {
            if (_workspaceWatcher != null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_workspaceDirectory);
                _workspaceWatcher = new FileSystemWatcher(_workspaceDirectory)
                {
                    Filter = "*.*",
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };

                _workspaceWatcher.Created += WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Deleted += WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Changed += WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Renamed += WorkspaceWatcher_OnRenamed;
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private void StopWorkspaceWatcher()
        {
            if (_workspaceWatcher == null)
            {
                return;
            }

            try
            {
                _workspaceWatcher.EnableRaisingEvents = false;
                _workspaceWatcher.Created -= WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Deleted -= WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Changed -= WorkspaceWatcher_OnChanged;
                _workspaceWatcher.Renamed -= WorkspaceWatcher_OnRenamed;
                _workspaceWatcher.Dispose();
            }
            catch
            {
            }

            _workspaceWatcher = null;
        }

        private void WorkspaceWatcher_OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!HasSupportedScriptExtension(e.FullPath))
            {
                return;
            }

            ScheduleWorkspaceRefresh();
        }

        private void WorkspaceWatcher_OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!HasSupportedScriptExtension(e.FullPath) && !HasSupportedScriptExtension(e.OldFullPath))
            {
                return;
            }

            ScheduleWorkspaceRefresh();
        }

        private void ScheduleWorkspaceRefresh()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScheduleWorkspaceRefresh));
                return;
            }

            _workspaceRefreshTimer.Stop();
            _workspaceRefreshTimer.Start();
        }

        private void InitializeTabs()
        {
            if (LoadTabsState())
            {
                return;
            }

            Tabs.Clear();
            Tabs.Add(new TabEntry(CreateTabId(), FormatUntitledTitle(1), LocalizationManager.T("WaveUI.Editor.DefaultScript")));
            _activeTab = Tabs[^1];
            _activeTab.IsActive = true;
        }

        private static string CreateTabId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string FormatUntitledTitle(int n)
        {
            return LocalizationManager.F("WaveUI.Editor.Untitled", n);
        }

        private static bool IsUnlimitedTabsEnabled()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var raw = ConfigManager.Get(cfg, UnlimitedTabsKey);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                return bool.TryParse(raw.Trim(), out var enabled) && enabled;
            }
            catch
            {
                return false;
            }
        }

        private static ClosePromptPreference LoadClosePromptPreference()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var raw = ConfigManager.Get(cfg, ClosePromptPreferenceKey);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return ClosePromptPreference.Ask;
                }

                var value = raw.Trim();
                if (value.Equals("close", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return ClosePromptPreference.AlwaysClose;
                }

                if (value.Equals("cancel", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("keep", StringComparison.OrdinalIgnoreCase))
                {
                    return ClosePromptPreference.AlwaysCancel;
                }

                return ClosePromptPreference.Ask;
            }
            catch
            {
                return ClosePromptPreference.Ask;
            }
        }

        private static void SaveClosePromptPreference(ClosePromptPreference preference)
        {
            try
            {
                var value = preference switch
                {
                    ClosePromptPreference.AlwaysClose => "close",
                    ClosePromptPreference.AlwaysCancel => "cancel",
                    _ => "ask",
                };

                var cfg = ConfigManager.ReadConfig();
                ConfigManager.Set(cfg, ClosePromptPreferenceKey, value);
                ConfigManager.WriteConfig(cfg);
            }
            catch
            {
            }
        }

        private bool IsTabLimitReached()
        {
            return !IsUnlimitedTabsEnabled() && Tabs.Count >= MaxTabs;
        }

        private static readonly string[] SupportedExtensions =
        {
            ".luau",
            ".lua",
            ".txt",
        };

        private static bool IsSupportedScriptExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return false;
            }

            return SupportedExtensions.Any(candidate => ext.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSupportedScriptExtension(string path)
        {
            return IsSupportedScriptExtension(Path.GetExtension(path));
        }

        private string GetSelectedScriptExtension()
        {
            if (NewScriptExtensionCombo?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag.Trim();
            }

            return ".luau";
        }

        private static string ResolveWorkspaceDirectory()
        {
            var dir = Path.Combine(AppPaths.AppDirectory, "workspace");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
            }
            return dir;
        }

        private bool LoadTabsState()
        {
            try
            {
                if (!File.Exists(_tabsStatePath))
                {
                    return false;
                }

                var json = File.ReadAllText(_tabsStatePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var state = JsonSerializer.Deserialize<TabsState>(json);
                if (state == null || state.Tabs.Count == 0)
                {
                    return false;
                }

                Tabs.Clear();
                TabEntry? activeTab = null;

                foreach (var entry in state.Tabs)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var tabId = string.IsNullOrWhiteSpace(entry.Id) ? CreateTabId() : entry.Id;
                    var title = string.IsNullOrWhiteSpace(entry.Title)
                        ? FormatUntitledTitle(Tabs.Count + 1)
                        : entry.Title;
                    var tab = new TabEntry(tabId, title, entry.Content ?? string.Empty)
                    {
                        IsPinned = entry.IsPinned,
                        FilePath = entry.FilePath,
                    };
                    Tabs.Add(tab);

                    if (!string.IsNullOrWhiteSpace(state.ActiveTabId)
                        && string.Equals(state.ActiveTabId, tab.Id, StringComparison.Ordinal))
                    {
                        activeTab = tab;
                    }
                }

                if (Tabs.Count == 0)
                {
                    return false;
                }

                _activeTab = activeTab ?? Tabs[0];
                foreach (var tab in Tabs)
                {
                    tab.IsActive = ReferenceEquals(tab, _activeTab);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task SaveTabsStateAsync()
        {
            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }
            }
            catch
            {
            }

            SaveTabsState();
        }

        private void SaveTabsState()
        {
            try
            {
                Directory.CreateDirectory(_workspaceDirectory);
                var state = new TabsState
                {
                    ActiveTabId = _activeTab?.Id,
                };

                foreach (var tab in Tabs)
                {
                    state.Tabs.Add(new TabStateEntry
                    {
                        Id = tab.Id,
                        Title = tab.Title,
                        Content = tab.Content ?? string.Empty,
                        IsPinned = tab.IsPinned,
                        FilePath = tab.FilePath,
                    });
                }

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tabsStatePath, json);
            }
            catch
            {
            }
        }

        private void LoadWorkspaceScripts()
        {
            WorkspaceScripts.Clear();

            IEnumerable<string> files = Array.Empty<string>();
            try
            {
                Directory.CreateDirectory(_workspaceDirectory);
                files = Directory.EnumerateFiles(_workspaceDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(HasSupportedScriptExtension)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                WorkspaceScripts.Add(new WorkspaceScriptEntry(name, file));
            }

            ApplySavedScriptsFilter();
        }

        private void UpdateSavedScriptsEmptyState()
        {
            if (SavedScriptsEmptyText == null)
            {
                return;
            }

            var view = CollectionViewSource.GetDefaultView(WorkspaceScripts);
            var hasItems = view?.Cast<object>().Any() ?? WorkspaceScripts.Count > 0;

            var emptyKey = string.IsNullOrWhiteSpace(_savedScriptsSearchText)
                ? "WaveUI.Editor.Explorer.Empty"
                : "WaveUI.Editor.Explorer.EmptySearch";
            SavedScriptsEmptyText.Text = LocalizationManager.T(emptyKey);

            SavedScriptsEmptyText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            if (SavedScriptsScrollViewer != null)
            {
                SavedScriptsScrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplySavedScriptsFilter()
        {
            var view = CollectionViewSource.GetDefaultView(WorkspaceScripts);
            if (view == null)
            {
                UpdateSavedScriptsEmptyState();
                return;
            }

            view.Filter = obj =>
            {
                if (obj is not WorkspaceScriptEntry entry)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_savedScriptsSearchText))
                {
                    return true;
                }

                return entry.Name.IndexOf(_savedScriptsSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            view.Refresh();
            UpdateSavedScriptsEmptyState();
            ApplySavedScriptsState(animated: false);
        }

        private double CalculateSavedScriptsHeight()
        {
            var maxHeight = SavedScriptsMaxHeight;

            if (SavedScriptsSection != null && SavedScriptsHeader != null)
            {
                var availableHeight = SavedScriptsSection.ActualHeight - SavedScriptsHeader.ActualHeight;
                if (availableHeight > 0)
                {
                    maxHeight = availableHeight;
                }
            }

            return Math.Max(0, maxHeight);
        }

        private void SavedScriptsSection_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isSavedScriptsExpanded)
            {
                return;
            }

            ApplySavedScriptsState(animated: false);
        }

        private void ApplySavedScriptsState(bool animated)
        {
            if (SavedScriptsListHost == null || SavedScriptsToggleRotate == null || SavedScriptsListTranslate == null)
            {
                return;
            }

            var targetHeight = _isSavedScriptsExpanded ? CalculateSavedScriptsHeight() : 0;
            var targetOpacity = _isSavedScriptsExpanded ? 1 : 0;
            var targetAngle = _isSavedScriptsExpanded ? SavedScriptsExpandedAngle : SavedScriptsCollapsedAngle;
            var targetTranslate = _isSavedScriptsExpanded ? 0 : SavedScriptsCollapsedTranslateY;

            if (!animated)
            {
                SavedScriptsListHost.BeginAnimation(Border.MaxHeightProperty, null);
                SavedScriptsListHost.BeginAnimation(UIElement.OpacityProperty, null);
                SavedScriptsToggleRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                SavedScriptsListTranslate.BeginAnimation(TranslateTransform.YProperty, null);

                SavedScriptsListHost.MaxHeight = targetHeight;
                SavedScriptsListHost.Opacity = targetOpacity;
                SavedScriptsToggleRotate.Angle = targetAngle;
                SavedScriptsListTranslate.Y = targetTranslate;
                return;
            }

            if (_isSavedScriptsAnimating)
            {
                return;
            }

            _isSavedScriptsAnimating = true;

            var heightAnim = new DoubleAnimation
            {
                From = SavedScriptsListHost.MaxHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            heightAnim.Completed += (_, _) =>
            {
                SavedScriptsListHost.BeginAnimation(Border.MaxHeightProperty, null);
                SavedScriptsListHost.MaxHeight = targetHeight;
                _isSavedScriptsAnimating = false;
            };

            var opacityAnim = new DoubleAnimation
            {
                From = SavedScriptsListHost.Opacity,
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            opacityAnim.Completed += (_, _) =>
            {
                SavedScriptsListHost.BeginAnimation(UIElement.OpacityProperty, null);
                SavedScriptsListHost.Opacity = targetOpacity;
            };

            var rotateAnim = new DoubleAnimation
            {
                From = SavedScriptsToggleRotate.Angle,
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            rotateAnim.Completed += (_, _) =>
            {
                SavedScriptsToggleRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                SavedScriptsToggleRotate.Angle = targetAngle;
            };

            var translateAnim = new DoubleAnimation
            {
                From = SavedScriptsListTranslate.Y,
                To = targetTranslate,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            translateAnim.Completed += (_, _) =>
            {
                SavedScriptsListTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                SavedScriptsListTranslate.Y = targetTranslate;
            };

            SavedScriptsListHost.BeginAnimation(Border.MaxHeightProperty, heightAnim);
            SavedScriptsListHost.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            SavedScriptsToggleRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            SavedScriptsListTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private async void EditorView_OnLoaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();
            _savedScriptsSearchText = SavedScriptsSearchBox?.Text ?? string.Empty;
            LoadWorkspaceScripts();
            StartWorkspaceWatcher();

            if (_monacoInitialized)
            {
                return;
            }

            _monacoInitialized = true;

            try
            {
                await MonacoView.EnsureCoreWebView2Async();

                var baseDir = AppPaths.AppDirectory;

                var monacoFolder = ResolveMonacoFolder(baseDir);
                if (monacoFolder == null)
                {
                    _toast(LocalizationManager.T("WaveUI.Editor.Toast.MonacoAssetsNotFound"));
                    return;
                }

                MonacoView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                MonacoView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                MonacoView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    MonacoHostName,
                    monacoFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                MonacoView.CoreWebView2.NavigationCompleted += MonacoView_OnNavigationCompleted;
                MonacoView.Source = new Uri($"https://{MonacoHostName}/index.html");
            }
            catch (Exception ex)
            {
                _toast(LocalizationManager.F("WaveUI.Editor.Toast.MonacoInitFailed", ex.Message));
            }
        }

        private static string? ResolveMonacoFolder(string baseDir)
        {
            var candidates = new[]
            {
                Path.Combine(baseDir, "Assets", "WaveUI", "Monaco"),
                Path.Combine(baseDir, "Assets", "Monaco"),
                Path.Combine(baseDir, "Assets", "waveUI", "Monaco"),
                Path.Combine(baseDir, "waveUI", "Monaco1"),
            };

            foreach (var folder in candidates)
            {
                if (Directory.Exists(folder) && File.Exists(Path.Combine(folder, "index.html")))
                {
                    return folder;
                }
            }

            return null;
        }

        private static bool IsDescendantOf(DependencyObject? obj, DependencyObject ancestor)
        {
            var current = obj;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void SavedScriptsToggle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _isSavedScriptsExpanded = !_isSavedScriptsExpanded;
            ApplySavedScriptsState(animated: true);
        }

        private void SavedScriptsAdd_OnClick(object sender, RoutedEventArgs e)
        {
            ShowNewScriptModal();
        }

        private void SavedScriptsSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _savedScriptsSearchText = SavedScriptsSearchBox?.Text ?? string.Empty;
            _savedScriptsSearchTimer.Stop();
            _savedScriptsSearchTimer.Start();
        }

        private void SavedScriptItem_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not WorkspaceScriptEntry entry)
            {
                return;
            }

            try
            {
                var text = File.ReadAllText(entry.FullPath);
                OpenScriptInNewTab(entry.Name, text, entry.FullPath);
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private void ShowNewScriptModal()
        {
            if (_isNewScriptModalAnimating || _isNewScriptModalOpen)
            {
                return;
            }

            _isNewScriptModalOpen = true;
            _isNewScriptModalAnimating = true;

            if (NewScriptModalOverlay == null || NewScriptModalScale == null || NewScriptModalTranslate == null)
            {
                _isNewScriptModalAnimating = false;
                _isNewScriptModalOpen = false;
                return;
            }

            SetMonacoModalState(true);

            if (NewScriptNameBox != null)
            {
                NewScriptNameBox.Text = LocalizationManager.T("WaveUI.Common.Untitled");
            }

            if (NewScriptContentBox != null)
            {
                NewScriptContentBox.Text = string.Empty;
            }

            NewScriptModalOverlay.Visibility = Visibility.Visible;
            NewScriptModalOverlay.BeginAnimation(OpacityProperty, null);
            NewScriptModalOverlay.Opacity = 0;

            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            NewScriptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            NewScriptModalScale.ScaleX = 0.96;
            NewScriptModalScale.ScaleY = 0.96;
            NewScriptModalTranslate.Y = 8;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeIn.Completed += (_, _) =>
            {
                NewScriptModalOverlay.BeginAnimation(OpacityProperty, null);
                NewScriptModalOverlay.Opacity = 1;
                _isNewScriptModalAnimating = false;

                if (NewScriptNameBox != null)
                {
                    NewScriptNameBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        NewScriptNameBox.Focus();
                        NewScriptNameBox.SelectAll();
                    }), DispatcherPriority.Background);
                }
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var translateAnim = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            scaleAnim.Completed += (_, _) =>
            {
                NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                NewScriptModalScale.ScaleX = 1;
                NewScriptModalScale.ScaleY = 1;
            };

            translateAnim.Completed += (_, _) =>
            {
                NewScriptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                NewScriptModalTranslate.Y = 0;
            };

            NewScriptModalOverlay.BeginAnimation(OpacityProperty, fadeIn);
            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            NewScriptModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private void HideNewScriptModal()
        {
            if (_isNewScriptModalAnimating || !_isNewScriptModalOpen)
            {
                return;
            }

            if (NewScriptModalOverlay == null || NewScriptModalScale == null || NewScriptModalTranslate == null)
            {
                _isNewScriptModalAnimating = false;
                _isNewScriptModalOpen = false;
                SetMonacoModalState(false);
                return;
            }

            _isNewScriptModalAnimating = true;

            NewScriptModalOverlay.BeginAnimation(OpacityProperty, null);
            var fadeOut = new DoubleAnimation
            {
                From = NewScriptModalOverlay.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeOut.Completed += (_, _) =>
            {
                NewScriptModalOverlay.BeginAnimation(OpacityProperty, null);
                NewScriptModalOverlay.Opacity = 0;
                NewScriptModalOverlay.Visibility = Visibility.Collapsed;
                _isNewScriptModalAnimating = false;
                _isNewScriptModalOpen = false;
                SetMonacoModalState(false);
            };

            var scaleAnim = new DoubleAnimation
            {
                From = NewScriptModalScale.ScaleX,
                To = 0.96,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var translateAnim = new DoubleAnimation
            {
                From = NewScriptModalTranslate.Y,
                To = 8,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            scaleAnim.Completed += (_, _) =>
            {
                NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                NewScriptModalScale.ScaleX = 0.96;
                NewScriptModalScale.ScaleY = 0.96;
            };

            translateAnim.Completed += (_, _) =>
            {
                NewScriptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                NewScriptModalTranslate.Y = 8;
            };

            NewScriptModalOverlay.BeginAnimation(OpacityProperty, fadeOut);
            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            NewScriptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            NewScriptModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private void SetMonacoModalState(bool isModalOpen)
        {
            if (MonacoView == null)
            {
                return;
            }

            MonacoView.IsHitTestVisible = !isModalOpen;
            MonacoView.Visibility = isModalOpen ? Visibility.Hidden : Visibility.Visible;
        }

        private void NewScriptOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (NewScriptModalPanel != null
                && e.OriginalSource is DependencyObject obj
                && IsDescendantOf(obj, NewScriptModalPanel))
            {
                return;
            }

            e.Handled = true;
            HideNewScriptModal();
        }

        private void NewScriptCancel_OnClick(object sender, RoutedEventArgs e)
        {
            HideNewScriptModal();
        }

        private System.Threading.Tasks.Task<ClosePromptResult> ShowClosePromptAsync()
        {
            if (_isClosePromptAnimating || _isClosePromptOpen)
            {
                return _closePromptTcs?.Task ?? System.Threading.Tasks.Task.FromResult(ClosePromptResult.Cancel);
            }

            if (ClosePromptModalOverlay == null
                || ClosePromptModalScale == null
                || ClosePromptModalTranslate == null)
            {
                return System.Threading.Tasks.Task.FromResult(ClosePromptResult.Cancel);
            }

            _isClosePromptOpen = true;
            _isClosePromptAnimating = true;
            _closePromptTcs = new TaskCompletionSource<ClosePromptResult>();

            SetMonacoModalState(true);

            if (ClosePromptRememberCheckBox != null)
            {
                ClosePromptRememberCheckBox.IsChecked = false;
            }

            ClosePromptModalOverlay.Visibility = Visibility.Visible;
            ClosePromptModalOverlay.BeginAnimation(OpacityProperty, null);
            ClosePromptModalOverlay.Opacity = 0;

            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ClosePromptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            ClosePromptModalScale.ScaleX = 0.96;
            ClosePromptModalScale.ScaleY = 0.96;
            ClosePromptModalTranslate.Y = 8;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeIn.Completed += (_, _) =>
            {
                ClosePromptModalOverlay.BeginAnimation(OpacityProperty, null);
                ClosePromptModalOverlay.Opacity = 1;
                _isClosePromptAnimating = false;
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var translateAnim = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            scaleAnim.Completed += (_, _) =>
            {
                ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                ClosePromptModalScale.ScaleX = 1;
                ClosePromptModalScale.ScaleY = 1;
            };

            translateAnim.Completed += (_, _) =>
            {
                ClosePromptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ClosePromptModalTranslate.Y = 0;
            };

            ClosePromptModalOverlay.BeginAnimation(OpacityProperty, fadeIn);
            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            ClosePromptModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);

            return _closePromptTcs.Task;
        }

        private void ClosePromptOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ClosePromptModalPanel != null
                && e.OriginalSource is DependencyObject obj
                && IsDescendantOf(obj, ClosePromptModalPanel))
            {
                return;
            }

            e.Handled = true;
            CompleteClosePrompt(ClosePromptResult.Cancel);
        }

        private void ClosePromptCancel_OnClick(object sender, RoutedEventArgs e)
        {
            CompleteClosePrompt(ClosePromptResult.Cancel);
        }

        private void ClosePromptConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            CompleteClosePrompt(ClosePromptResult.Close);
        }

        private void CompleteClosePrompt(ClosePromptResult result)
        {
            if (ClosePromptRememberCheckBox?.IsChecked == true)
            {
                _closePromptPreference = result == ClosePromptResult.Close
                    ? ClosePromptPreference.AlwaysClose
                    : ClosePromptPreference.AlwaysCancel;
                SaveClosePromptPreference(_closePromptPreference);
            }

            HideClosePromptModal(result);
        }

        private void HideClosePromptModal(ClosePromptResult result)
        {
            if (ClosePromptModalOverlay == null || ClosePromptModalScale == null || ClosePromptModalTranslate == null)
            {
                _closePromptTcs?.TrySetResult(result);
                _closePromptTcs = null;
                _isClosePromptOpen = false;
                _isClosePromptAnimating = false;
                SetMonacoModalState(false);
                return;
            }

            if (_isClosePromptAnimating || !_isClosePromptOpen)
            {
                _closePromptTcs?.TrySetResult(result);
                _closePromptTcs = null;
                return;
            }

            _isClosePromptAnimating = true;

            ClosePromptModalOverlay.BeginAnimation(OpacityProperty, null);
            var fadeOut = new DoubleAnimation
            {
                From = ClosePromptModalOverlay.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeOut.Completed += (_, _) =>
            {
                ClosePromptModalOverlay.BeginAnimation(OpacityProperty, null);
                ClosePromptModalOverlay.Opacity = 0;
                ClosePromptModalOverlay.Visibility = Visibility.Collapsed;
                _isClosePromptAnimating = false;
                _isClosePromptOpen = false;
                SetMonacoModalState(false);
                _closePromptTcs?.TrySetResult(result);
                _closePromptTcs = null;
            };

            var scaleAnim = new DoubleAnimation
            {
                From = ClosePromptModalScale.ScaleX,
                To = 0.96,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var translateAnim = new DoubleAnimation
            {
                From = ClosePromptModalTranslate.Y,
                To = 8,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            scaleAnim.Completed += (_, _) =>
            {
                ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                ClosePromptModalScale.ScaleX = 0.96;
                ClosePromptModalScale.ScaleY = 0.96;
            };

            translateAnim.Completed += (_, _) =>
            {
                ClosePromptModalTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ClosePromptModalTranslate.Y = 8;
            };

            ClosePromptModalOverlay.BeginAnimation(OpacityProperty, fadeOut);
            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ClosePromptModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            ClosePromptModalTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private System.Threading.Tasks.Task<bool> ShouldCancelTabCloseAsync(TabEntry tab)
        {
            return ShouldCancelTabsCloseAsync(new[] { tab });
        }

        private async System.Threading.Tasks.Task<bool> ShouldCancelTabsCloseAsync(IReadOnlyCollection<TabEntry> tabsToClose)
        {
            if (tabsToClose.Count == 0)
            {
                return false;
            }

            var hasUnsavedContent = false;
            foreach (var tab in tabsToClose)
            {
                if (ReferenceEquals(tab, _activeTab) && MonacoView?.CoreWebView2 != null)
                {
                    try
                    {
                        tab.Content = await GetEditorTextAsync();
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrWhiteSpace(tab.Content))
                {
                    hasUnsavedContent = true;
                    break;
                }
            }

            if (!hasUnsavedContent)
            {
                return false;
            }

            if (_closePromptPreference == ClosePromptPreference.AlwaysClose)
            {
                return false;
            }

            if (_closePromptPreference == ClosePromptPreference.AlwaysCancel)
            {
                return true;
            }

            var result = await ShowClosePromptAsync();
            return result != ClosePromptResult.Close;
        }

        private void NewScriptCreate_OnClick(object sender, RoutedEventArgs e)
        {
            var rawName = NewScriptNameBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptNameEmpty"));
                return;
            }

            var trimmedName = rawName.TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptNameEmpty"));
                return;
            }

            if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptNameInvalid"));
                return;
            }

            var baseName = trimmedName;
            foreach (var supported in SupportedExtensions)
            {
                if (baseName.EndsWith(supported, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName[..^supported.Length].TrimEnd('.', ' ');
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptNameEmpty"));
                return;
            }

            var fileName = $"{baseName}{GetSelectedScriptExtension()}";

            var path = Path.Combine(_workspaceDirectory, fileName);
            if (File.Exists(path))
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptExists"));
                return;
            }

            var content = NewScriptContentBox?.Text ?? string.Empty;

            try
            {
                Directory.CreateDirectory(_workspaceDirectory);
                File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
                return;
            }

            HideNewScriptModal();
            LoadWorkspaceScripts();
            OpenScriptInNewTab(Path.GetFileName(path), content, path);
        }

        private async void MonacoView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                await SetEditorTextAsync(_activeTab.Content);
            }
            catch
            {
            }
        }

        private async System.Threading.Tasks.Task SetEditorTextAsync(string text)
        {
            var json = JsonSerializer.Serialize(text);
            await MonacoView.ExecuteScriptAsync($"if (window.SetText) SetText({json}); else if (window.editor) editor.setValue({json});");
        }

        private async System.Threading.Tasks.Task<string> GetEditorTextAsync()
        {
            var result = await MonacoView.ExecuteScriptAsync("(window.GetText ? GetText() : (window.editor ? editor.getValue() : ''))");
            try
            {
                using var doc = JsonDocument.Parse(result);
                return doc.RootElement.GetString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void TabBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsClickInsideCloseButton(e))
            {
                return;
            }

            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not TabEntry tab)
            {
                return;
            }

            e.Handled = true;

            if (e.ClickCount >= 2)
            {
                await SwitchToTabAsync(tab);
                BeginRename(tab);
                return;
            }

            await SwitchToTabAsync(tab);
        }

        private async System.Threading.Tasks.Task SwitchToTabAsync(TabEntry tab)
        {
            if (ReferenceEquals(tab, _activeTab))
            {
                return;
            }

            EndRenameAll(commit: true);

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    var currentText = await GetEditorTextAsync();
                    _activeTab.Content = currentText;
                }
            }
            catch
            {
            }

            _activeTab.IsActive = false;
            _activeTab = tab;
            _activeTab.IsActive = true;

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    await SetEditorTextAsync(_activeTab.Content);
                }
            }
            catch
            {
            }

            try
            {
                _ = Dispatcher.BeginInvoke(new Action(() => ScrollTabIntoView(tab)), DispatcherPriority.Background);
            }
            catch
            {
            }
        }

        private void ScrollTabIntoView(TabEntry tab)
        {
            if (TabScrollViewer == null || TabsItems == null)
            {
                return;
            }

            try
            {
                TabsItems.UpdateLayout();
            }
            catch
            {
            }

            if (TabsItems.ItemContainerGenerator.ContainerFromItem(tab) is not FrameworkElement container)
            {
                return;
            }

            try
            {
                container.UpdateLayout();
            }
            catch
            {
            }

            Rect bounds;
            try
            {
                bounds = container.TransformToAncestor(TabScrollViewer)
                    .TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
            }
            catch
            {
                return;
            }

            var viewportWidth = TabScrollViewer.ViewportWidth;
            if (viewportWidth <= 0)
            {
                return;
            }

            var left = bounds.Left;
            var right = bounds.Right;
            var offset = TabScrollViewer.HorizontalOffset;
            var target = offset;

            if (left < 0)
            {
                target = Math.Max(0, offset + left);
            }
            else if (right > viewportWidth)
            {
                target = offset + (right - viewportWidth);
            }

            if (Math.Abs(target - offset) > 0.5)
            {
                TabScrollViewer.ScrollToHorizontalOffset(target);
            }
        }

        private void BeginRename(TabEntry tab)
        {
            EndRenameAll(commit: true);

            tab.EditingTitle = tab.Title;
            tab.IsEditing = true;
        }

        private void EndRenameAll(bool commit)
        {
            foreach (var tab in Tabs)
            {
                if (!tab.IsEditing)
                {
                    continue;
                }

                if (commit)
                {
                    CommitRename(tab);
                }
                else
                {
                    tab.IsEditing = false;
                    tab.EditingTitle = tab.Title;
                }
            }
        }

        private void CommitRename(TabEntry tab)
        {
            var name = (tab.EditingTitle ?? string.Empty).Trim();
            if (name.Length > 0)
            {
                tab.Title = name;
            }

            tab.IsEditing = false;
            tab.EditingTitle = tab.Title;
        }

        private void TabRenameBox_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox box)
            {
                return;
            }

            if (box.DataContext is not TabEntry tab || !tab.IsEditing)
            {
                return;
            }

            box.Dispatcher.BeginInvoke(new Action(() =>
            {
                box.Focus();
                box.SelectAll();
            }), DispatcherPriority.Background);
        }

        private void TabRenameBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box)
            {
                return;
            }

            if (box.DataContext is not TabEntry tab)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitRename(tab);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                tab.IsEditing = false;
                tab.EditingTitle = tab.Title;
                e.Handled = true;
            }
        }

        private void TabRenameBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox box)
            {
                return;
            }

            if (box.DataContext is not TabEntry tab)
            {
                return;
            }

            if (tab.IsEditing)
            {
                CommitRename(tab);
            }
        }

        private void EditorViewRoot_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject obj)
            {
                while (obj != null)
                {
                    if (obj is TextBox)
                    {
                        return;
                    }
                    obj = VisualTreeHelper.GetParent(obj);
                }
            }

            EndRenameAll(commit: true);
        }

        private void AddTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is UIElement el)
            {
                try
                {
                    el.BeginAnimation(UIElement.OpacityProperty, null);
                    var anim = new DoubleAnimation
                    {
                        From = 1,
                        To = 0.65,
                        Duration = TimeSpan.FromMilliseconds(70),
                        AutoReverse = true,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    };
                    el.BeginAnimation(UIElement.OpacityProperty, anim);
                }
                catch
                {
                }
            }
            _ = AddTabAsync();
        }

        public void OpenScriptInNewTab(string title, string script, string? filePath = null)
        {
            _ = Dispatcher.InvokeAsync(async () => await OpenScriptInNewTabAsync(title, script, filePath));
        }

        private async System.Threading.Tasks.Task OpenScriptInNewTabAsync(string title, string script, string? filePath)
        {
            EndRenameAll(commit: true);

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var existing = Tabs.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.FilePath)
                                                        && string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    await SwitchToTabAsync(existing);
                    return;
                }
            }

            if (IsTabLimitReached())
            {
                _toast(LocalizationManager.F("WaveUI.Editor.Toast.MaxTabs", MaxTabs));
                return;
            }

            var finalTitle = string.IsNullOrWhiteSpace(title) ? FormatUntitledTitle(Tabs.Count + 1) : title;
            var tab = new TabEntry(CreateTabId(), finalTitle, script ?? string.Empty)
            {
                FilePath = filePath,
            };
            Tabs.Add(tab);

            foreach (var t in Tabs)
            {
                t.IsActive = false;
            }

            _activeTab = tab;
            _activeTab.IsActive = true;

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    await SetEditorTextAsync(_activeTab.Content);
                }
            }
            catch
            {
            }

            try
            {
                TabScrollViewer.ScrollToRightEnd();
            }
            catch
            {
            }
        }

        private async System.Threading.Tasks.Task AddTabAsync()
        {
            if (IsTabLimitReached())
            {
                _toast(LocalizationManager.F("WaveUI.Editor.Toast.MaxTabs", MaxTabs));
                return;
            }

            var nextNumber = Tabs.Count + 1;
            var tab = new TabEntry(CreateTabId(), FormatUntitledTitle(nextNumber), string.Empty);
            Tabs.Add(tab);

            await SwitchToTabAsync(tab);

            try
            {
                TabScrollViewer.ScrollToRightEnd();
            }
            catch
            {
            }
        }

        private async void TabClose_OnClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not TabEntry tab)
            {
                return;
            }

            if (tab.IsPinned)
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.PinnedTabCantBeClosed"));
                return;
            }

            if (Tabs.Count <= 1)
            {
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.AtLeastOneTabMustRemain"));
                return;
            }

            if (await ShouldCancelTabCloseAsync(tab))
            {
                return;
            }

            try
            {
                if (ReferenceEquals(tab, _activeTab) && MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }
            }
            catch
            {
            }

            var idx = Tabs.IndexOf(tab);

            Border? tabRoot = null;
            try
            {
                tabRoot = FindAncestor<Border>(el);
            }
            catch
            {
                tabRoot = null;
            }

            if (tabRoot != null)
            {
                try
                {
                    tabRoot.BeginAnimation(UIElement.OpacityProperty, null);
                    tabRoot.BeginAnimation(UIElement.RenderTransformProperty, null);

                    TranslateTransform? translate = null;
                    if (tabRoot.RenderTransform is TransformGroup group
                        && group.Children.Count > 1
                        && group.Children[1] is TranslateTransform translateTransform)
                    {
                        translate = translateTransform;
                    }

                    var fade = new DoubleAnimation
                    {
                        From = tabRoot.Opacity,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(140),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.HoldEnd,
                    };
                    tabRoot.BeginAnimation(UIElement.OpacityProperty, fade);

                    if (translate != null)
                    {
                        translate.BeginAnimation(TranslateTransform.XProperty, null);
                        var slideOut = new DoubleAnimation
                        {
                            From = translate.X,
                            To = 10,
                            Duration = TimeSpan.FromMilliseconds(180),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                            FillBehavior = FillBehavior.HoldEnd,
                        };
                        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
                    }

                    await System.Threading.Tasks.Task.Delay(180);
                }
                catch
                {
                }
            }

            Tabs.Remove(tab);

            if (!ReferenceEquals(tab, _activeTab))
            {
                return;
            }

            if (Tabs.Count == 0)
            {
                return;
            }

            var nextIdx = Math.Clamp(idx - 1, 0, Tabs.Count - 1);
            _activeTab.IsActive = false;
            _activeTab = Tabs[nextIdx];
            _activeTab.IsActive = true;

            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    await SetEditorTextAsync(_activeTab.Content);
                }
            }
            catch
            {
            }

            try
            {
                await SaveTabsStateAsync();
            }
            catch
            {
            }
        }

        private void TabScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                var next = TabScrollViewer.HorizontalOffset - e.Delta;
                TabScrollViewer.ScrollToHorizontalOffset(next);
                e.Handled = true;
            }
            catch
            {
            }
        }

        private static bool IsClickInsideCloseButton(MouseButtonEventArgs e)
        {
            var obj = e.OriginalSource as DependencyObject;
            while (obj != null)
            {
                if (obj is Button)
                {
                    return true;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private void TabBorder_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not TabEntry tab)
            {
                return;
            }

            e.Handled = true;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("TabContextMenuStyle"),
                PlacementTarget = el,
            };

            var pinItem = new MenuItem
            {
                Header = tab.IsPinned
                    ? LocalizationManager.T("WaveUI.Editor.TabMenu.Unpin")
                    : LocalizationManager.T("WaveUI.Editor.TabMenu.Pin"),
                Icon = new WaveIcon { IconName = "pin", Width = 14, Height = 14, Stretch = Stretch.Uniform },
                Style = (Style)FindResource("TabContextMenuItemStyle"),
            };
            pinItem.Click += (_, _) => TogglePin(tab);
            menu.Items.Add(pinItem);

            var renameItem = new MenuItem
            {
                Header = LocalizationManager.T("WaveUI.Editor.TabMenu.Rename"),
                Icon = new WaveIcon { IconName = "rename", Width = 14, Height = 14, Stretch = Stretch.Uniform },
                Style = (Style)FindResource("TabContextMenuItemStyle"),
            };
            renameItem.Click += async (_, _) =>
            {
                await SwitchToTabAsync(tab);
                BeginRename(tab);
            };
            menu.Items.Add(renameItem);

            var separator = new Separator
            {
                Style = (Style)FindResource("TabContextMenuSeparatorStyle"),
            };
            menu.Items.Add(separator);

            var closeAllItem = new MenuItem
            {
                Header = LocalizationManager.T("WaveUI.Editor.TabMenu.CloseAll"),
                Icon = new WaveIcon { IconName = "remove", Width = 14, Height = 14, Stretch = Stretch.Uniform },
                Style = (Style)FindResource("TabContextMenuItemStyle"),
            };
            closeAllItem.Click += async (_, _) => await CloseAllUnpinnedTabsAsync();
            menu.Items.Add(closeAllItem);

            el.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void TogglePin(TabEntry tab)
        {
            if (tab.IsPinned)
            {
                tab.IsPinned = false;
                return;
            }

            tab.IsPinned = true;

            try
            {
                var idx = Tabs.IndexOf(tab);
                if (idx > 0)
                {
                    Tabs.Move(idx, 0);
                }
            }
            catch
            {
            }
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void ExplorerToggle_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isExplorerOpen)
            {
                CloseExplorerAnimated();
                return;
            }

            OpenExplorerAnimated();
        }

        private void CloseExplorerAnimated()
        {
            if (!_isExplorerOpen)
            {
                return;
            }

            _isExplorerOpen = false;
            ApplyToggleIcon(animated: true);

            ExplorerPanel.Visibility = Visibility.Visible;

            ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            EditorHost.BeginAnimation(MarginProperty, null);

            var slide = new DoubleAnimation
            {
                From = 0,
                To = ExplorerWidth + ExplorerGap,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var margin = new ThicknessAnimation
            {
                From = EditorHost.Margin,
                To = ExplorerClosedMargin,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            slide.Completed += (_, _) =>
            {
                ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                ExplorerTransform.X = ExplorerWidth + ExplorerGap;
                ExplorerPanel.Visibility = Visibility.Collapsed;
            };

            margin.Completed += (_, _) =>
            {
                EditorHost.BeginAnimation(MarginProperty, null);
                EditorHost.Margin = ExplorerClosedMargin;
            };

            ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
            EditorHost.BeginAnimation(MarginProperty, margin);
        }

        private void OpenExplorerAnimated()
        {
            if (_isExplorerOpen)
            {
                return;
            }

            _isExplorerOpen = true;
            ApplyToggleIcon(animated: true);

            ExplorerPanel.Visibility = Visibility.Visible;

            ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            ExplorerTransform.X = ExplorerWidth + ExplorerGap;

            EditorHost.BeginAnimation(MarginProperty, null);
            EditorHost.Margin = ExplorerClosedMargin;

            var slide = new DoubleAnimation
            {
                From = ExplorerWidth + ExplorerGap,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            var margin = new ThicknessAnimation
            {
                From = EditorHost.Margin,
                To = ExplorerOpenMargin,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            slide.Completed += (_, _) =>
            {
                ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                ExplorerTransform.X = 0;
            };

            margin.Completed += (_, _) =>
            {
                EditorHost.BeginAnimation(MarginProperty, null);
                EditorHost.Margin = ExplorerOpenMargin;
            };

            ExplorerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
            EditorHost.BeginAnimation(MarginProperty, margin);
        }

        private void ApplyExplorerState(bool animated)
        {
            if (_isExplorerOpen)
            {
                ExplorerPanel.Visibility = Visibility.Visible;
                ExplorerTransform.X = 0;
                EditorHost.Margin = ExplorerOpenMargin;
                ApplyToggleIcon(animated);
                return;
            }

            ExplorerPanel.Visibility = Visibility.Collapsed;
            ExplorerTransform.X = ExplorerWidth + ExplorerGap;
            EditorHost.Margin = ExplorerClosedMargin;
            ApplyToggleIcon(animated);
        }

        private void ApplyToggleIcon(bool animated)
        {
            var iconName = _isExplorerOpen ? "right" : "left";

            void SetSource()
            {
                try
                {
                    ExplorerToggleIcon.Source = WaveAssets.TryLoadIcon(iconName);
                }
                catch
                {
                    ExplorerToggleIcon.Source = null;
                }
            }

            if (!animated)
            {
                ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, null);
                ExplorerToggleIcon.Opacity = 1;
                SetSource();
                return;
            }

            ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, null);

            var fadeOut = new DoubleAnimation
            {
                From = ExplorerToggleIcon.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(90),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            fadeOut.Completed += (_, _) =>
            {
                ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, null);
                ExplorerToggleIcon.Opacity = 0;
                SetSource();

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(120),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop,
                };

                fadeIn.Completed += (_, _) =>
                {
                    ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, null);
                    ExplorerToggleIcon.Opacity = 1;
                };

                ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            ExplorerToggleIcon.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void Execute_OnClick(object sender, RoutedEventArgs e)
        {
            _ = ExecuteCurrentTabAsync();
        }

        public void ExecuteCurrentTab()
        {
            _ = ExecuteCurrentTabAsync();
        }

        private async System.Threading.Tasks.Task ExecuteCurrentTabAsync()
        {
            try
            {
                if (MonacoView?.CoreWebView2 == null)
                {
                    _toast(LocalizationManager.T("WaveUI.Editor.Toast.MonacoNotReady"));
                    return;
                }

                try
                {
                    RobloxRuntime.Initialize();
                }
                catch
                {
                }

                try
                {
                    RobloxRuntime.RefreshRunningState();
                }
                catch
                {
                }

                if (!RobloxRuntime.IsRobloxRunning)
                {
                    WaveToastService.ShowPrompt(
                        LocalizationManager.T("WaveUI.Common.Info"),
                        LocalizationManager.T("WaveUI.Roblox.Prompt.Open"),
                        LocalizationManager.T("WaveUI.Common.Yes"),
                        LocalizationManager.T("WaveUI.Common.No"),
                        () => _ = RobloxRuntime.TryLaunchRoblox(),
                        null);
                    return;
                }

                var script = await GetEditorTextAsync();
                _activeTab.Content = script;

                if (string.IsNullOrWhiteSpace(script))
                {
                    _toast(LocalizationManager.T("WaveUI.Editor.Toast.ScriptEmpty"));
                    return;
                }

                if (!global::Executor.API.IsAttached())
                {
                    var attach = await global::Executor.API.AttachAsync(System.Threading.CancellationToken.None);
                    if (!attach.Success)
                    {
                        _toast(attach.Message);
                        return;
                    }
                }

                var result = await global::Executor.API.ExecuteScriptAsync(script, System.Threading.CancellationToken.None);
                _toast(result.Message);
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private void KillRoblox_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                RobloxRuntime.KillRoblox();
                _toast(LocalizationManager.T("WaveUI.Editor.Toast.RobloxKilled"));
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private async void Clear_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await SetEditorTextAsync(string.Empty);
                _activeTab.Content = string.Empty;
            }
            catch
            {
            }
        }

        private async void Open_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }

                var dlg = new OpenFileDialog
                {
                    Filter = "Script Files (*.luau;*.lua;*.txt)|*.luau;*.lua;*.txt|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false,
                    Title = "Open",
                };

                if (dlg.ShowDialog() != true)
                {
                    return;
                }

                var path = dlg.FileName;
                var text = File.ReadAllText(path);

                _activeTab.FilePath = path;
                _activeTab.Title = Path.GetFileName(path);
                _activeTab.EditingTitle = _activeTab.Title;
                _activeTab.Content = text;

                if (MonacoView?.CoreWebView2 != null)
                {
                    await SetEditorTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private async void Save_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MonacoView?.CoreWebView2 != null)
                {
                    _activeTab.Content = await GetEditorTextAsync();
                }

                var path = _activeTab.FilePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = "Luau (*.luau)|*.luau|Lua (*.lua)|*.lua|Text (*.txt)|*.txt|All Files (*.*)|*.*",
                        Title = "Save",
                        FileName = _activeTab.Title,
                        AddExtension = true,
                    };

                    if (dlg.ShowDialog() != true)
                    {
                        return;
                    }

                    path = dlg.FileName;
                    _activeTab.FilePath = path;
                    _activeTab.Title = Path.GetFileName(path);
                    _activeTab.EditingTitle = _activeTab.Title;
                }

                File.WriteAllText(path, _activeTab.Content ?? string.Empty);
                _toast("Saved.");
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        public sealed class TabEntry : INotifyPropertyChanged
        {
            private string _title;
            private string _content;
            private bool _isActive;
            private bool _isEditing;
            private string _editingTitle;
            private bool _isPinned;
            private string? _filePath;

            public TabEntry(string id, string title, string content)
            {
                Id = id;
                _title = title;
                _content = content;
                _editingTitle = title;
            }

            public string Id { get; }

            public string Title
            {
                get => _title;
                set
                {
                    if (value == _title)
                    {
                        return;
                    }
                    _title = value;
                    OnPropertyChanged();
                }
            }

            public string Content
            {
                get => _content;
                set
                {
                    if (value == _content)
                    {
                        return;
                    }
                    _content = value;
                    OnPropertyChanged();
                }
            }

            public bool IsActive
            {
                get => _isActive;
                set
                {
                    if (value == _isActive)
                    {
                        return;
                    }
                    _isActive = value;
                    OnPropertyChanged();
                }
            }

            public bool IsEditing
            {
                get => _isEditing;
                set
                {
                    if (value == _isEditing)
                    {
                        return;
                    }
                    _isEditing = value;
                    OnPropertyChanged();
                }
            }

            public string EditingTitle
            {
                get => _editingTitle;
                set
                {
                    if (value == _editingTitle)
                    {
                        return;
                    }
                    _editingTitle = value;
                    OnPropertyChanged();
                }
            }

            public bool IsPinned
            {
                get => _isPinned;
                set
                {
                    if (value == _isPinned)
                    {
                        return;
                    }
                    _isPinned = value;
                    OnPropertyChanged();
                }
            }

            public string? FilePath
            {
                get => _filePath;
                set
                {
                    if (string.Equals(value, _filePath, StringComparison.Ordinal))
                    {
                        return;
                    }
                    _filePath = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        public sealed class WorkspaceScriptEntry
        {
            public WorkspaceScriptEntry(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
            }

            public string Name { get; }
            public string FullPath { get; }
        }

        private sealed class TabsState
        {
            public string? ActiveTabId { get; set; }
            public List<TabStateEntry> Tabs { get; } = new();
        }

        private sealed class TabStateEntry
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public bool IsPinned { get; set; }
            public string? FilePath { get; set; }
        }

        private enum ClosePromptPreference
        {
            Ask,
            AlwaysClose,
            AlwaysCancel,
        }

        private enum ClosePromptResult
        {
            Cancel,
            Close,
        }
    }
}
