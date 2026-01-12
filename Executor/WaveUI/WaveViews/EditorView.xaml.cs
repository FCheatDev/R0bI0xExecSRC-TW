using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
        public ObservableCollection<TabEntry> Tabs { get; } = new();
        private TabEntry _activeTab = null!;

        private const double ExplorerWidth = 280;
        private const double ExplorerGap = 10;
        private static readonly Thickness ExplorerOpenMargin = new(0, 0, ExplorerWidth + ExplorerGap, 0);
        private static readonly Thickness ExplorerClosedMargin = new(0, 0, 0, 0);

        public EditorView(Action<string> toast)
        {
            InitializeComponent();
            _toast = toast;

            DataContext = this;

            ApplyExplorerState(animated: false);

            InitializeTabs();

            Loaded += EditorView_OnLoaded;
            Unloaded += EditorView_OnUnloaded;
        }

        private void EditorView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
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
        }

        private void InitializeTabs()
        {
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

        private async void EditorView_OnLoaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

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

        public void OpenScriptInNewTab(string title, string script)
        {
            _ = Dispatcher.InvokeAsync(async () => await OpenScriptInNewTabAsync(title, script));
        }

        private async System.Threading.Tasks.Task OpenScriptInNewTabAsync(string title, string script)
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

            if (Tabs.Count >= MaxTabs)
            {
                _toast(LocalizationManager.F("WaveUI.Editor.Toast.MaxTabs", MaxTabs));
                return;
            }

            var finalTitle = string.IsNullOrWhiteSpace(title) ? FormatUntitledTitle(Tabs.Count + 1) : title;
            var tab = new TabEntry(CreateTabId(), finalTitle, script ?? string.Empty);
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
            if (Tabs.Count >= MaxTabs)
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

                    var fade = new DoubleAnimation
                    {
                        From = tabRoot.Opacity,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(140),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.HoldEnd,
                    };
                    tabRoot.BeginAnimation(UIElement.OpacityProperty, fade);

                    await System.Threading.Tasks.Task.Delay(150);
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
                    Filter = "Script Files (*.luau;*.txt)|*.luau;*.txt|All Files (*.*)|*.*",
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
                        Filter = "Luau (*.luau)|*.luau|Text (*.txt)|*.txt|All Files (*.*)|*.*",
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
    }
}
