using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Executor.WaveUI
{
    public partial class WaveShell : UserControl
    {
        private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);
        private string _activeTab = "Home";
        private bool _booted;
        private bool _isNavigating;
        private string? _pendingPage;
        private string? _currentPage;
        private bool _isWindowAnimating;
        private Rect? _restoreBounds;
        private bool _isZoomed;
        private bool _loadHooked;
        private bool _loadCompleted;
        private bool _mainPagesPreloadStarted;

        public WaveShell()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_booted)
            {
                return;
            }

            _booted = true;
            ShowLoadThenKey();
        }

        private void ShowLoadThenKey()
        {
            NavigateTo("Load");
        }

        private void BeginPreloadMainTabPages()
        {
            if (_mainPagesPreloadStarted)
            {
                return;
            }

            _mainPagesPreloadStarted = true;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var pages = new[] { "Home", "Editor", "Scripts", "Clients", "Settings" };
                var index = 0;

                void ScheduleNext()
                {
                    if (index >= pages.Length)
                    {
                        return;
                    }

                    var page = pages[index++];
                    if (!_pages.ContainsKey(page))
                    {
                        _pages[page] = CreateControlForPage(page);
                    }

                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ScheduleNext));
                }

                ScheduleNext();
            }));
        }

        private void SetActiveTab(string? tab)
        {
            TabHome.IsChecked = string.Equals(tab, "Home", StringComparison.OrdinalIgnoreCase);
            TabEditor.IsChecked = string.Equals(tab, "Editor", StringComparison.OrdinalIgnoreCase);
            TabScripts.IsChecked = string.Equals(tab, "Scripts", StringComparison.OrdinalIgnoreCase);
            TabClients.IsChecked = string.Equals(tab, "Clients", StringComparison.OrdinalIgnoreCase);
            TabSettings.IsChecked = string.Equals(tab, "Settings", StringComparison.OrdinalIgnoreCase);

            var active = (Brush)FindResource("WaveBlue");
            var inactive = (Brush)FindResource("WaveText");

            TabHomeText.Foreground = TabHome.IsChecked == true ? active : inactive;
            TabEditorText.Foreground = TabEditor.IsChecked == true ? active : inactive;
            TabScriptsText.Foreground = TabScripts.IsChecked == true ? active : inactive;
            TabClientsText.Foreground = TabClients.IsChecked == true ? active : inactive;
            TabSettingsText.Foreground = TabSettings.IsChecked == true ? active : inactive;

            TabHomeIcon.TintBrush = TabHome.IsChecked == true ? active : inactive;
            TabEditorIcon.TintBrush = TabEditor.IsChecked == true ? active : inactive;
            TabScriptsIcon.TintBrush = TabScripts.IsChecked == true ? active : inactive;
            TabClientsIcon.TintBrush = TabClients.IsChecked == true ? active : inactive;
            TabSettingsIcon.TintBrush = TabSettings.IsChecked == true ? active : inactive;

            AnimateTabText(TabHomeTextScale, TabHome.IsChecked == true);
            AnimateTabText(TabEditorTextScale, TabEditor.IsChecked == true);
            AnimateTabText(TabScriptsTextScale, TabScripts.IsChecked == true);
            AnimateTabText(TabClientsTextScale, TabClients.IsChecked == true);
            AnimateTabText(TabSettingsTextScale, TabSettings.IsChecked == true);

            _activeTab = tab ?? "";
        }

        private static void AnimateTabText(System.Windows.Media.ScaleTransform transform, bool show)
        {
            var to = show ? 1.0 : 0.0;
            var anim = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(show ? 160 : 240),
                EasingFunction = new QuadraticEase { EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn },
            };
            transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
        }

        private void TopNavQuick_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b)
            {
                return;
            }

            var page = b.Tag as string;
            if (string.IsNullOrWhiteSpace(page))
            {
                return;
            }

            if (string.Equals(_activeTab, page, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetActiveTab(page);
            NavigateTo(page);
        }

        private void Tab_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb)
            {
                return;
            }

            var tab = tb.Tag as string;
            if (string.IsNullOrWhiteSpace(tab))
            {
                return;
            }

            if (string.Equals(_activeTab, tab, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeTab = tab;
            SetActiveTab(tab);
            NavigateTo(tab);
        }

        private void NavigateTo(string page)
        {
            if (_isNavigating)
            {
                _pendingPage = page;
                return;
            }

            if (string.Equals(_currentPage, page, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var animateTransition = ShouldAnimateTabTransition(_currentPage, page);

            UpdateTopBarVisibility(page);

            if (!_pages.TryGetValue(page, out var control))
            {
                control = CreateControlForPage(page);
                _pages[page] = control;
            }

            HookLoadCompletedIfNeeded(page, control);

            _currentPage = page;

            Action? afterTransition = null;
            if (string.Equals(page, "Key", StringComparison.OrdinalIgnoreCase))
            {
                afterTransition = null;
            }

            SetPageContentAnimated(control, animateTransition, afterTransition);
        }

        private UserControl CreateControlForPage(string page)
        {
            return page.Trim().ToLowerInvariant() switch
            {
                "load" => new WaveViews.LoadView(),
                "key" => new WaveViews.KeySystemView(OnKeyVerified),
                "home" => new WaveViews.HomeView(),
                "editor" => new WaveViews.EditorView(ShowToast),
                "scripts" => new WaveViews.ScriptsView(ShowToast, OpenScriptInEditor, ExecuteScriptInEditor),
                "clients" => new WaveViews.ClientsView(),
                "settings" => new WaveViews.SettingsView(),
                _ => new WaveViews.HomeView(),
            };
        }

        private void OpenScriptInEditor(string title, string script)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_pages.TryGetValue("Editor", out var control) || control is not WaveViews.EditorView editor)
                {
                    editor = new WaveViews.EditorView(ShowToast);
                    _pages["Editor"] = editor;
                }

                try
                {
                    editor.OpenScriptInNewTab(title, script);
                }
                catch (Exception ex)
                {
                    ShowToast(ex.Message);
                }

                SetActiveTab("Editor");
                NavigateTo("Editor");
            });
        }

        private void ExecuteScriptInEditor(string title, string script)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_pages.TryGetValue("Editor", out var control) || control is not WaveViews.EditorView editor)
                {
                    editor = new WaveViews.EditorView(ShowToast);
                    _pages["Editor"] = editor;
                }

                try
                {
                    editor.OpenScriptInNewTab(title, script);
                }
                catch (Exception ex)
                {
                    ShowToast(ex.Message);
                }

                SetActiveTab("Editor");
                NavigateTo("Editor");

                var attempts = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += (_, _) =>
                {
                    attempts++;

                    try
                    {
                        if (editor.IsMonacoReady)
                        {
                            timer.Stop();
                            editor.ExecuteCurrentTab();
                            return;
                        }
                    }
                    catch
                    {
                    }

                    if (attempts >= 20)
                    {
                        timer.Stop();
                        ShowToast("Monaco is not ready.");
                    }
                };
                timer.Start();
            });
        }

        private void HookLoadCompletedIfNeeded(string page, UserControl control)
        {
            if (_loadHooked)
            {
                return;
            }

            if (!string.Equals(page, "Load", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (control is not WaveViews.LoadView load)
            {
                return;
            }

            _loadHooked = true;
            load.LoadCompleted += () =>
            {
                if (_loadCompleted)
                {
                    return;
                }

                _loadCompleted = true;
                Dispatcher.Invoke(() =>
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        NavigateTo("Key");
                    };
                    timer.Start();
                });
            };
        }

        private static bool ShouldAnimateTabTransition(string? fromPage, string toPage)
        {
            return (IsMainTabPage(fromPage) && IsMainTabPage(toPage))
                || IsAuthPage(fromPage)
                || IsAuthPage(toPage);
        }

        private static bool IsAuthPage(string? page)
        {
            if (string.IsNullOrWhiteSpace(page))
            {
                return false;
            }

            var key = page.Trim().ToLowerInvariant();
            return key is ("load" or "key");
        }

        private static bool IsMainTabPage(string? page)
        {
            if (string.IsNullOrWhiteSpace(page))
            {
                return false;
            }

            var key = page.Trim().ToLowerInvariant();
            return key is ("home" or "editor" or "scripts" or "clients" or "settings");
        }

        private void SetPageContentAnimated(UserControl control, bool animate, Action? afterTransition)
        {
            if (PageHost.Content == null)
            {
                PageHost.BeginAnimation(OpacityProperty, null);

                if (!animate)
                {
                    PageHost.Opacity = 1;
                    PageHost.Content = control;
                    afterTransition?.Invoke();
                    return;
                }

                PageHost.Opacity = 0;
                PageHost.Content = control;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };

                fadeIn.Completed += (_, _) =>
                {
                    PageHost.Opacity = 1;
                    PageHost.BeginAnimation(OpacityProperty, null);
                    afterTransition?.Invoke();
                };

                PageHost.BeginAnimation(OpacityProperty, fadeIn);
                return;
            }

            if (!animate)
            {
                PageHost.BeginAnimation(OpacityProperty, null);
                PageHost.Opacity = 1;
                PageHost.Content = control;
                afterTransition?.Invoke();
                return;
            }

            _isNavigating = true;

            PageHost.BeginAnimation(OpacityProperty, null);
            if (PageHost.Opacity is < 0 or > 1)
            {
                PageHost.Opacity = 1;
            }

            var fadeOut = new DoubleAnimation
            {
                From = PageHost.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            fadeOut.Completed += (_, _) =>
            {
                PageHost.Opacity = 0;
                PageHost.BeginAnimation(OpacityProperty, null);
                PageHost.Content = control;

                var fadeIn = new DoubleAnimation
                {
                    From = PageHost.Opacity,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };

                fadeIn.Completed += (_, _) =>
                {
                    PageHost.Opacity = 1;
                    PageHost.BeginAnimation(OpacityProperty, null);
                    _isNavigating = false;

                    afterTransition?.Invoke();

                    if (!string.IsNullOrWhiteSpace(_pendingPage))
                    {
                        var next = _pendingPage;
                        _pendingPage = null;
                        NavigateTo(next);
                    }
                };

                PageHost.BeginAnimation(OpacityProperty, fadeIn);
            };

            PageHost.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void UpdateTopBarVisibility(string page)
        {
            var key = page.Trim().ToLowerInvariant();
            var hideNav = key is ("load" or "key");
            TopBar.Visibility = Visibility.Visible;
            TopBarRow.Height = new GridLength(44);
            TopNav.Visibility = hideNav ? Visibility.Collapsed : Visibility.Visible;

            TopBar.Background = Brushes.Black;
            ShellRoot.Background = Brushes.Black;
        }

        private void OnKeyVerified()
        {
            SetActiveTab("Home");
            NavigateTo("Home");
        }

        public void ShowToast(string message)
        {
            WaveToastService.Show("Info", message);
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null || _isWindowAnimating)
            {
                return;
            }

            _isWindowAnimating = true;
            var (scale, translate) = EnsureWindowTransforms(w);

            w.BeginAnimation(Window.OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);

            var sb = new Storyboard();

            var fade = new DoubleAnimation
            {
                From = w.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };
            Storyboard.SetTarget(fade, w);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Window.OpacityProperty));

            var sx = new DoubleAnimation
            {
                From = scale.ScaleX,
                To = 0.985,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(sx, scale);
            Storyboard.SetTargetProperty(sx, new PropertyPath(ScaleTransform.ScaleXProperty));

            var sy = new DoubleAnimation
            {
                From = scale.ScaleY,
                To = 0.985,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(sy, scale);
            Storyboard.SetTargetProperty(sy, new PropertyPath(ScaleTransform.ScaleYProperty));

            var ty = new DoubleAnimation
            {
                From = translate.Y,
                To = translate.Y + 6,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(ty, translate);
            Storyboard.SetTargetProperty(ty, new PropertyPath(TranslateTransform.YProperty));

            sb.Children.Add(fade);
            sb.Children.Add(sx);
            sb.Children.Add(sy);
            sb.Children.Add(ty);

            sb.Completed += (_, _) =>
            {
                w.Close();
            };

            sb.Begin();
        }

        private void Minimize_OnClick(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null || _isWindowAnimating)
            {
                return;
            }

            if (w.WindowState == WindowState.Minimized)
            {
                return;
            }

            _isWindowAnimating = true;
            var (scale, translate) = EnsureWindowTransforms(w);

            w.BeginAnimation(Window.OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);

            var sb = new Storyboard();

            var fade = new DoubleAnimation
            {
                From = w.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fade, w);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Window.OpacityProperty));

            var ty = new DoubleAnimation
            {
                From = translate.Y,
                To = translate.Y + 10,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };
            Storyboard.SetTarget(ty, translate);
            Storyboard.SetTargetProperty(ty, new PropertyPath(TranslateTransform.YProperty));

            sb.Children.Add(fade);
            sb.Children.Add(ty);

            sb.Completed += (_, _) =>
            {
                w.WindowState = WindowState.Minimized;
                w.BeginAnimation(Window.OpacityProperty, null);
                w.Opacity = 1;
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.Y = 0;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
                _isWindowAnimating = false;
            };

            sb.Begin();
        }

        private void Maximize_OnClick(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null || _isWindowAnimating)
            {
                return;
            }

            if (w.WindowState != WindowState.Normal)
            {
                w.WindowState = WindowState.Normal;
            }

            var screen = SystemParameters.WorkArea;

            if (!_isZoomed)
            {
                _restoreBounds = new Rect(w.Left, w.Top, w.Width, w.Height);
            }

            var to = !_isZoomed
                ? new Rect(screen.Left, screen.Top, screen.Width, screen.Height)
                : (_restoreBounds ?? new Rect(w.Left, w.Top, w.Width, w.Height));

            _isWindowAnimating = true;

            w.BeginAnimation(Window.OpacityProperty, null);

            var sb = new Storyboard();
            var halfMs = 250;

            var fadeOut = new DoubleAnimation
            {
                From = w.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(halfMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            Storyboard.SetTarget(fadeOut, w);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Window.OpacityProperty));
            sb.Children.Add(fadeOut);

            fadeOut.Completed += (_, _) =>
            {
                w.Left = to.Left;
                w.Top = to.Top;
                w.Width = to.Width;
                w.Height = to.Height;

                w.BeginAnimation(Window.OpacityProperty, null);
                w.Opacity = 0;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(halfMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };

                fadeIn.Completed += (_, _) =>
                {
                    w.BeginAnimation(Window.OpacityProperty, null);
                    w.Opacity = 1;
                    _isZoomed = !_isZoomed;
                    _isWindowAnimating = false;
                };

                w.BeginAnimation(Window.OpacityProperty, fadeIn);
            };

            sb.Begin();
        }

        private static (ScaleTransform scale, TranslateTransform translate) EnsureWindowTransforms(Window w)
        {
            var root = w.Content as UIElement;
            if (root == null)
            {
                return (new ScaleTransform(1, 1), new TranslateTransform(0, 0));
            }

            root.RenderTransformOrigin = new Point(0.5, 1);

            if (root.RenderTransform is TransformGroup g
                && g.Children.Count >= 2
                && g.Children[0] is ScaleTransform s
                && g.Children[1] is TranslateTransform t)
            {
                return (s, t);
            }

            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            root.RenderTransform = group;
            return (scale, translate);
        }

        private static void AnimateWindowBounds(Window w, Rect to, int durationMs, IEasingFunction easing, Action? completed = null)
        {
            w.BeginAnimation(Window.LeftProperty, null);
            w.BeginAnimation(Window.TopProperty, null);
            w.BeginAnimation(Window.WidthProperty, null);
            w.BeginAnimation(Window.HeightProperty, null);

            var sb = new Storyboard();

            var left = new DoubleAnimation { From = w.Left, To = to.Left, Duration = TimeSpan.FromMilliseconds(durationMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
            var top = new DoubleAnimation { From = w.Top, To = to.Top, Duration = TimeSpan.FromMilliseconds(durationMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
            var width = new DoubleAnimation { From = w.Width, To = to.Width, Duration = TimeSpan.FromMilliseconds(durationMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
            var height = new DoubleAnimation { From = w.Height, To = to.Height, Duration = TimeSpan.FromMilliseconds(durationMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };

            Storyboard.SetTarget(left, w);
            Storyboard.SetTargetProperty(left, new PropertyPath(Window.LeftProperty));
            Storyboard.SetTarget(top, w);
            Storyboard.SetTargetProperty(top, new PropertyPath(Window.TopProperty));
            Storyboard.SetTarget(width, w);
            Storyboard.SetTargetProperty(width, new PropertyPath(Window.WidthProperty));
            Storyboard.SetTarget(height, w);
            Storyboard.SetTargetProperty(height, new PropertyPath(Window.HeightProperty));

            sb.Children.Add(left);
            sb.Children.Add(top);
            sb.Children.Add(width);
            sb.Children.Add(height);

            sb.Completed += (_, _) =>
            {
                w.Left = to.Left;
                w.Top = to.Top;
                w.Width = to.Width;
                w.Height = to.Height;

                w.BeginAnimation(Window.LeftProperty, null);
                w.BeginAnimation(Window.TopProperty, null);
                w.BeginAnimation(Window.WidthProperty, null);
                w.BeginAnimation(Window.HeightProperty, null);

                completed?.Invoke();
            };

            sb.Begin();
        }

        private void TopBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            var w = Window.GetWindow(this);
            if (w == null)
            {
                return;
            }

            try
            {
                w.DragMove();
            }
            catch
            {
            }
        }

        private void TabSettingsIcon_Loaded(object sender, RoutedEventArgs e)
        {
             
        }
    }
}
