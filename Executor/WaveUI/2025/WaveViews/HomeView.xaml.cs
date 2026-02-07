using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Executor;

namespace Executor.WaveUI.WaveViews
{
    public partial class HomeView : UserControl
    {
        private List<RecentScriptItem> _recentScripts = new();
        private string _workspaceDirectory = string.Empty;
        private string _tabsStatePath = string.Empty;
        private const string TabsStateFileName = "editor_tabs.json";
        
        public HomeView()
        {
            InitializeComponent();
            Loaded += HomeView_Loaded;
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWorkspace();
            ApplyLanguage();
            ApplyFontSettings();
            LoadRecentScripts();
            
            // 監聽語言變更事件
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void InitializeWorkspace()
        {
            try
            {
                // 使用與 EditorView 相同的工作區路徑
                _workspaceDirectory = Path.Combine(AppPaths.AppDirectory, "workspace");
                _tabsStatePath = Path.Combine(_workspaceDirectory, TabsStateFileName);
                Directory.CreateDirectory(_workspaceDirectory);
                
                System.Diagnostics.Debug.WriteLine($"Workspace directory: {_workspaceDirectory}");
                System.Diagnostics.Debug.WriteLine($"Tabs state path: {_tabsStatePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize workspace: {ex.Message}");
            }
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => {
                ApplyLanguage();
            }));
        }

        private void ApplyLanguage()
        {
            if (!IsLoaded) return;

            // Welcome section
            WebsiteText.Text = GetLocalizedString("WaveUI.Home.Website", "Website");
            StoreText.Text = GetLocalizedString("WaveUI.Home.Store", "Store");

            // Subscription section
            SubscriptionText.Text = GetLocalizedString("WaveUI.Home.Subscription", "SUBSCRIPTION");
            KeyExpiryText.Text = GetLocalizedString("WaveUI.Home.KeyExpiry", "Key Expiry:");
            PlanText.Text = GetLocalizedString("WaveUI.Home.Plan", "Plan:");

            // Widgets section
            WidgetsText.Text = GetLocalizedString("WaveUI.Home.Widgets", "WIDGETS");
            ReleaseText.Text = GetLocalizedString("WaveUI.Home.Widget.Release", "Release");
            ReleaseDescText.Text = GetLocalizedString("WaveUI.Home.Widget.ReleaseDesc", "Wave is finally available to the public, after\nmany months!");
            StatusText.Text = GetLocalizedString("WaveUI.Home.Widget.Status", "Status");
            StatusDescText.Text = GetLocalizedString("WaveUI.Home.Widget.StatusDesc", "Wave is currently undetected, and up-to\ndate.");
            DiscordText.Text = GetLocalizedString("WaveUI.Home.Widget.Discord", "Discord");
            DiscordDescText.Text = GetLocalizedString("WaveUI.Home.Widget.DiscordDesc", "Join our discord server for further updates,\nand a community.");

            // Recent section
            RecentText.Text = GetLocalizedString("WaveUI.Home.Recent", "RECENT");
            EditedText.Text = GetLocalizedString("WaveUI.Home.Edited", "Edited");
            EndOfListText.Text = GetLocalizedString("WaveUI.Home.End", "You've reached the end of the list");

            // Welcome message
            var welcomeKey = GetLocalizedString("WaveUI.Home.Welcome", "Welcome to Wave,");
            var username = Environment.UserName;
            WelcomeText.Text = $"{welcomeKey}\n{username}";
        }

        private string GetLocalizedString(string key, string defaultValue)
        {
            try
            {
                var result = LocalizationManager.T(key);
                return string.IsNullOrEmpty(result) ? defaultValue : result;
            }
            catch
            {
                return defaultValue;
            }
        }

        private void ApplyFontSettings()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var selectedFont = cfg.TryGetValue("font", out var fontValue) ? fontValue : string.Empty;
                
                FontFamily defaultFont = new FontFamily("Arial Black");
                FontFamily currentFont = string.IsNullOrEmpty(selectedFont) ? defaultFont : new FontFamily(selectedFont);
                
                // 套用到所有文字元素
                WelcomeText.FontFamily = currentFont;
                WebsiteText.FontFamily = currentFont;
                StoreText.FontFamily = currentFont;
                SubscriptionText.FontFamily = currentFont;
                KeyExpiryText.FontFamily = currentFont;
                PlanText.FontFamily = currentFont;
                WidgetsText.FontFamily = currentFont;
                ReleaseText.FontFamily = currentFont;
                ReleaseDescText.FontFamily = currentFont;
                StatusText.FontFamily = currentFont;
                StatusDescText.FontFamily = currentFont;
                DiscordText.FontFamily = currentFont;
                DiscordDescText.FontFamily = currentFont;
                RecentText.FontFamily = currentFont;
                EditedText.FontFamily = currentFont;
                EndOfListText.FontFamily = currentFont;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply font settings: {ex.Message}");
            }
        }

        private void LoadRecentScripts()
        {
            try
            {
                _recentScripts.Clear();
                
                // 從儲存的 tabs 狀態檔案讀取未儲存的腳本
                var savedTabs = LoadTabsState();
                
                System.Diagnostics.Debug.WriteLine($"Loaded {savedTabs.Count} tabs from file");

                var rng = new Random();

                foreach (var tab in savedTabs.Take(10))
                {
                    var lastEdited = DateTime.Now;

                    if (!string.IsNullOrWhiteSpace(tab.FilePath))
                    {
                        try
                        {
                            if (File.Exists(tab.FilePath))
                            {
                                lastEdited = File.GetLastWriteTime(tab.FilePath);
                            }
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        lastEdited = DateTime.Now.AddMinutes(-rng.Next(1, 60));
                    }

                    _recentScripts.Add(new RecentScriptItem
                    {
                        Id = tab.Id,
                        Title = tab.Title,
                        Content = tab.Content,
                        LastEdited = lastEdited,
                    });

                    System.Diagnostics.Debug.WriteLine($"Added recent script: {tab.Title}");
                }
                
                RefreshRecentScriptsUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load recent scripts: {ex.Message}");
            }
        }

        private List<TabStateEntry> LoadTabsState()
        {
            try
            {
                if (!File.Exists(_tabsStatePath))
                {
                    return new List<TabStateEntry>();
                }

                var json = File.ReadAllText(_tabsStatePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<TabStateEntry>();
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var state = JsonSerializer.Deserialize<TabsState>(json, options);
                
                return state?.Tabs ?? new List<TabStateEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tabs state: {ex.Message}");
                return new List<TabStateEntry>();
            }
        }

        private void RefreshRecentScriptsUI()
        {
            try
            {
                // 清除現有的腳本項目（保留 EndOfListText）
                var itemsToRemove = new List<UIElement>();
                foreach (UIElement child in RecentScriptsPanel.Children)
                {
                    if (child is Border)
                    {
                        itemsToRemove.Add(child);
                    }
                }
                
                foreach (var item in itemsToRemove)
                {
                    RecentScriptsPanel.Children.Remove(item);
                }

                // 添加新的腳本項目
                for (var i = 0; i < _recentScripts.Count; i++)
                {
                    var script = _recentScripts[i];
                    var scriptBorder = CreateScriptItem(script);
                    if (i == _recentScripts.Count - 1)
                    {
                        scriptBorder.Margin = new Thickness(scriptBorder.Margin.Left, scriptBorder.Margin.Top, scriptBorder.Margin.Right, 0);
                    }
                    RecentScriptsPanel.Children.Insert(RecentScriptsPanel.Children.Count - 1, scriptBorder); // 插入到 EndOfListText 之前
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh recent scripts UI: {ex.Message}");
            }
        }

        private Border CreateScriptItem(RecentScriptItem script)
        {
            var backgroundBrush = new SolidColorBrush(Color.FromRgb(14, 14, 14));
            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = backgroundBrush,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = script.Id,
                ClipToBounds = true
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var iconBorder = new Border
            {
                Width = 34,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(24, 40, 47)),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(0)
            };
            
            var iconText = new TextBlock
            {
                Text = "📄",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;

            var textStackPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            
            var titleText = new TextBlock
            {
                Text = script.Title,
                Foreground = new SolidColorBrush(Color.FromRgb(237, 237, 237)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };
            
            var timeText = new TextBlock
            {
                Text = GetRelativeTimeString(script.LastEdited),
                Foreground = new SolidColorBrush(Color.FromRgb(93, 96, 102)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            
            textStackPanel.Children.Add(titleText);
            textStackPanel.Children.Add(timeText);
            
            stackPanel.Children.Add(iconBorder);
            stackPanel.Children.Add(textStackPanel);
            
            Grid.SetColumn(stackPanel, 0);
            grid.Children.Add(stackPanel);

            // 創建 Edit 按鈕
            var editButton = new Border
            {
                Name = $"EditBtn_{script.Id}",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0ea5e9")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                RenderTransform = new TranslateTransform(60, 0), // 初始隱藏在右側
                Opacity = 0,
                Tag = script.Id
            };
            
            var editText = new TextBlock
            {
                Text = "Edit",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                FontFamily = new FontFamily("Arial Black")
            };
            editButton.Child = editText;
            
            editButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                NavigateToEditorTab(script.Id);
            };

            Grid.SetColumn(editButton, 1);
            grid.Children.Add(editButton);

            border.Child = grid;

            // 滑鼠進入事件 - 顯示 Edit 按鈕
            border.MouseEnter += (s, e) => 
            {
                AnimateRecentItemBackground(backgroundBrush, Color.FromRgb(26, 26, 26), EasingMode.EaseOut);
                ShowEditButtonAnimation(editButton);
            };

            // 滑鼠離開事件 - 隱藏 Edit 按鈕
            border.MouseLeave += (s, e) => 
            {
                AnimateRecentItemBackground(backgroundBrush, Color.FromRgb(14, 14, 14), EasingMode.EaseIn);
                HideEditButtonAnimation(editButton);
            };

            return border;
        }

        private static void AnimateRecentItemBackground(SolidColorBrush brush, Color toColor, EasingMode easingMode)
        {
            var colorAnim = new ColorAnimation
            {
                To = toColor,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = easingMode }
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }

        private void ShowEditButtonAnimation(Border editButton)
        {
            var transform = editButton.RenderTransform as TranslateTransform ?? new TranslateTransform(60, 0);
            editButton.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            var fadeAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(100)
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            editButton.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void HideEditButtonAnimation(Border editButton)
        {
            var transform = editButton.RenderTransform as TranslateTransform ?? new TranslateTransform(0, 0);
            editButton.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                To = 60,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            var fadeAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(80)
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            editButton.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void NavigateToEditorTab(string scriptId)
        {
            try
            {
                // 找到 WaveShell 並切換到 Editor 頁面
                var window = Window.GetWindow(this);
                if (window == null) return;

                Executor.WaveUI.WaveShell? shell = null;
                if (window.Content is Executor.WaveUI.WaveShell shellControl)
                {
                    shell = shellControl;
                }
                else
                {
                    shell = FindVisualChild<Executor.WaveUI.WaveShell>(window, string.Empty);
                }

                if (shell == null) return;

                var shellType = shell.GetType();
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                
                // 先設定 Tab 為 Editor
                var setActiveTab = shellType.GetMethod("SetActiveTab", bindingFlags);
                if (setActiveTab != null)
                {
                    setActiveTab.Invoke(shell, new object[] { "Editor" });
                }
                
                // 導航到 Editor 頁面
                var navigateTo = shellType.GetMethod("NavigateTo", bindingFlags);
                if (navigateTo != null)
                {
                    navigateTo.Invoke(shell, new object[] { "Editor" });
                }
                
                // 嘗試找到 EditorView 並設定對應的 tab
                Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        // 尋找 EditorView 實例
                        var editorView = FindEditorView(shell);
                        if (editorView != null)
                        {
                            // 找到對應的 tab 並設為活動
                            SetActiveTabById(editorView, scriptId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to set active tab: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to navigate to editor tab: {ex.Message}");
            }
        }

        private object? FindEditorView(DependencyObject shell)
        {
            try
            {
                // 嘗試通過不同的方式找到 EditorView
                var shellType = shell.GetType();
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                var pagesField = shellType.GetField("_pages", bindingFlags);
                if (pagesField?.GetValue(shell) is System.Collections.IDictionary pages)
                {
                    if (pages.Contains("Editor"))
                    {
                        return pages["Editor"];
                    }
                }
                
                // 嘗試找到 EditorView 屬性或欄位
                var editorViewProperty = shellType.GetProperty("EditorView", bindingFlags);
                if (editorViewProperty != null)
                {
                    return editorViewProperty.GetValue(shell);
                }
                
                var editorViewField = shellType.GetField("_editorView", bindingFlags);
                if (editorViewField != null)
                {
                    return editorViewField.GetValue(shell);
                }
                
                var editorByType = FindVisualChild<EditorView>(shell, string.Empty);
                if (editorByType != null)
                {
                    return editorByType;
                }

                // 嘗試在視覺樹中查找
                return FindVisualChild<FrameworkElement>(shell, "EditorView");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to find EditorView: {ex.Message}");
                return null;
            }
        }

        private void SetActiveTabById(object editorView, string scriptId)
        {
            try
            {
                var editorType = editorView.GetType();
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                
                // 獲取 Tabs 集合
                var tabsProperty = editorType.GetProperty("Tabs", bindingFlags);
                if (tabsProperty != null)
                {
                    var tabs = tabsProperty.GetValue(editorView) as System.Collections.IEnumerable;
                    if (tabs != null)
                    {
                        // 尋找對應的 tab
                        foreach (var tab in tabs)
                        {
                            var tabType = tab.GetType();
                            var idProperty = tabType.GetProperty("Id", bindingFlags);
                            if (idProperty != null && string.Equals(idProperty.GetValue(tab)?.ToString(), scriptId, StringComparison.Ordinal))
                            {
                                var switchToTabAsync = editorType.GetMethod("SwitchToTabAsync", bindingFlags);
                                if (switchToTabAsync != null)
                                {
                                    _ = switchToTabAsync.Invoke(editorView, new object[] { tab });
                                    System.Diagnostics.Debug.WriteLine($"Successfully activated tab: {scriptId}");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set active tab by ID: {ex.Message}");
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T && (string.IsNullOrEmpty(name) || (child as FrameworkElement)?.Name == name))
                {
                    return (T)child;
                }
                
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private void ShowEditButton(string scriptId, Border parentBorder)
        {
            // 已改用內建動畫方式
        }

        private void HideEditButton(string scriptId)
        {
            // 已改用內建動畫方式
        }

        private string GetRelativeTimeString(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalMinutes < 1)
                return "Just now";
            if (span.TotalMinutes < 60)
                return $"Edited {(int)span.TotalMinutes} mins ago";
            if (span.TotalHours < 24)
                return $"Edited {(int)span.TotalHours} hours ago";
            return $"Edited {span.Days} days ago";
        }

        private void RecentScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 根據內容是否超出視窗來控制滾輪顯示
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                var showScrollBar = scrollViewer.ScrollableHeight > 0;
                // 這裡可以根據需要調整滾輪的顯示邏輯
                // WPF 的 Auto 模式已經處理了這個邏輯
            }
        }

        private void Website_OnClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://axproject.dpdns.org/wave",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open website: {ex.Message}");
            }
        }

        private void Store_OnClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://axproject.dpdns.org/wave/buy",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open store: {ex.Message}");
            }
        }
    }

    public class RecentScriptItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime LastEdited { get; set; }
    }

    // Tabs state classes for deserializing saved tabs
    public class TabsState
    {
        public string? ActiveTabId { get; set; }
        public List<TabStateEntry> Tabs { get; set; } = new();
    }

    public class TabStateEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public string? FilePath { get; set; }
    }
}
