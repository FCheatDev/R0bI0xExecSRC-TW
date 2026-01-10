using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Executor.WaveUI.WaveViews
{
    public partial class EditorView : UserControl
    {
        private readonly Action<string> _toast;
        private bool _isExplorerOpen = true;

        private bool _monacoInitialized;
        private const string MonacoHostName = "monaco";
        private const string DefaultScriptText = "loadstring(game:HttpGet('https://raw.githubusercontent.com/...'))";

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
        }

        private void InitializeTabs()
        {
            Tabs.Clear();

            Tabs.Add(new TabEntry(CreateTabId(), "Untitled 4", ""));
            Tabs.Add(new TabEntry(CreateTabId(), "$UNC", ""));
            Tabs.Add(new TabEntry(CreateTabId(), "UNC", ""));
            Tabs.Add(new TabEntry(CreateTabId(), "Unnamed ESP", DefaultScriptText));

            _activeTab = Tabs[^1];
            _activeTab.IsActive = true;
        }

        private static string CreateTabId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private async void EditorView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_monacoInitialized)
            {
                return;
            }

            _monacoInitialized = true;

            try
            {
                await MonacoView.EnsureCoreWebView2Async();

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                var monacoFolder = ResolveMonacoFolder(baseDir);
                if (monacoFolder == null)
                {
                    _toast("Monaco assets not found (Assets\\Monaco, Assets\\waveUI\\Monaco, or waveUI\\Monaco1)." );
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
                _toast($"Monaco init failed: {ex.Message}");
            }
        }

        private static string? ResolveMonacoFolder(string baseDir)
        {
            var candidates = new[]
            {
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

        private void AddTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _ = AddTabAsync();
        }

        private async System.Threading.Tasks.Task AddTabAsync()
        {
            if (Tabs.Count >= MaxTabs)
            {
                _toast($"Max tabs is {MaxTabs}.");
                return;
            }

            var nextNumber = Tabs.Count + 1;
            var tab = new TabEntry(CreateTabId(), $"Untitled {nextNumber}", string.Empty);
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

            if (Tabs.Count <= 1)
            {
                _toast("At least one tab must remain.");
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

        private static bool TryExecuteWithVelocity(string script, out string? error)
        {
            error = null;

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? apiType = null;
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (string.Equals(t.Name, "SpashAPIVelocity", StringComparison.Ordinal))
                            {
                                apiType = t;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if (apiType == null)
                    {
                        continue;
                    }

                    var method = apiType.GetMethod("ExecuteScript", new[] { typeof(string) });
                    if (method == null)
                    {
                        error = "SpashAPIVelocity.ExecuteScript(string) not found.";
                        return false;
                    }

                    method.Invoke(null, new object?[] { script });
                    return true;
                }

                error = "SpashAPIVelocity type not found in loaded assemblies.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
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

        private async System.Threading.Tasks.Task ExecuteCurrentTabAsync()
        {
            try
            {
                if (MonacoView?.CoreWebView2 == null)
                {
                    _toast("Monaco is not ready.");
                    return;
                }

                var script = await GetEditorTextAsync();
                _activeTab.Content = script;

                if (!TryExecuteWithVelocity(script, out var error))
                {
                    _toast(error ?? "Execute failed.");
                    return;
                }

                _toast("Executed.");
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

        public sealed class TabEntry : INotifyPropertyChanged
        {
            private string _title;
            private string _content;
            private bool _isActive;
            private bool _isEditing;
            private string _editingTitle;

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

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
