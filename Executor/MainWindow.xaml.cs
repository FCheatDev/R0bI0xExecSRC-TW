using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Executor.WaveUI;

namespace Executor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int ResizeBorderThicknessDip = 6;
        private const double NormalCornerRadius = 3;
        public double TargetOpacity { get; set; } = 1.0;
        private string _currentTheme = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            Opacity = 0;

            try
            {
                Icon = WaveAssets.TryLoadIcon("app");
            }
            catch
            {
            }

            Loaded += (_, _) => BeginFadeIn();
            Loaded += (_, _) => UpdateRoundedChrome();
            SizeChanged += (_, _) => UpdateRoundedChrome();
            StateChanged += (_, _) => UpdateRoundedChrome();
            Closing += (_, _) => SaveTabsStateForExit();
            ApplyLanguage();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;

            SourceInitialized += OnSourceInitialized;

            ApplyTheme();
        }

        private void UpdateRoundedChrome()
        {
            try
            {
                if (RootBorder == null)
                {
                    return;
                }

                var radius = WindowState == WindowState.Maximized ? 0 : NormalCornerRadius;
                RootBorder.CornerRadius = new CornerRadius(radius);

                var w = RootBorder.ActualWidth;
                var h = RootBorder.ActualHeight;
                if (w <= 0 || h <= 0)
                {
                    RootBorder.Clip = null;
                    return;
                }

                var bt = RootBorder.BorderThickness;
                var t = Math.Max(Math.Max(bt.Left, bt.Top), Math.Max(bt.Right, bt.Bottom));
                var half = t / 2.0;
                RootBorder.Clip = new RectangleGeometry(new Rect(-half, -half, w + t, h + t), radius, radius);
            }
            catch
            {
            }
        }

        private void SaveTabsStateForExit()
        {
            try
            {
                if (ThemeHost.Content is WaveShell shell)
                {
                    shell.SaveTabsStateForExit();
                }
            }
            catch
            {
            }
        }

        private void BeginFadeIn()
        {
            try
            {
                BeginAnimation(OpacityProperty, null);
                var fade = new DoubleAnimation
                {
                    From = Opacity,
                    To = TargetOpacity,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
                fade.Completed += (_, _) =>
                {
                    BeginAnimation(OpacityProperty, null);
                    Opacity = TargetOpacity;
                };
                BeginAnimation(OpacityProperty, fade);
            }
            catch
            {
                Opacity = TargetOpacity;
            }
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    source.AddHook(WndProc);
                }
            }
            catch
            {
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST)
            {
                return IntPtr.Zero;
            }

            if (ResizeMode == ResizeMode.NoResize || WindowState != WindowState.Normal)
            {
                return IntPtr.Zero;
            }

            var x = (short)(lParam.ToInt64() & 0xFFFF);
            var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            var screenPt = new Point(x, y);

            Point pt;
            try
            {
                pt = PointFromScreen(screenPt);
            }
            catch
            {
                return IntPtr.Zero;
            }

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return IntPtr.Zero;
            }

            var t = ResizeBorderThicknessDip;

            var left = pt.X >= 0 && pt.X < t;
            var right = pt.X <= w && pt.X > w - t;
            var top = pt.Y >= 0 && pt.Y < t;
            var bottom = pt.Y <= h && pt.Y > h - t;

            if (!left && !right && !top && !bottom)
            {
                return IntPtr.Zero;
            }

            if (top)
            {
                try
                {
                    var hit = InputHitTest(pt) as DependencyObject;
                    while (hit != null)
                    {
                        if (hit is ButtonBase || hit is TextBoxBase || hit is PasswordBox || hit is Thumb)
                        {
                            return IntPtr.Zero;
                        }
                        hit = VisualTreeHelper.GetParent(hit);
                    }
                }
                catch
                {
                }
            }

            if (top && left)
            {
                handled = true;
                return new IntPtr(HTTOPLEFT);
            }

            if (top && right)
            {
                handled = true;
                return new IntPtr(HTTOPRIGHT);
            }

            if (bottom && left)
            {
                handled = true;
                return new IntPtr(HTBOTTOMLEFT);
            }

            if (bottom && right)
            {
                handled = true;
                return new IntPtr(HTBOTTOMRIGHT);
            }

            if (left)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            if (right)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }

            if (top)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }

            if (bottom)
            {
                handled = true;
                return new IntPtr(HTBOTTOM);
            }

            return IntPtr.Zero;
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        internal void ApplyTheme()
        {
            var cfg = ConfigManager.ReadConfig();
            var theme = ConfigManager.Get(cfg, "theme") ?? "";
            _currentTheme = theme;
            UpdateWindowTitle();

            var discordRpcEnabled = true;
            try
            {
                var raw = ConfigManager.Get(cfg, DiscordRpcService.EnabledConfigKey);

                if (!string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw.Trim(), out var parsed))
                {
                    discordRpcEnabled = parsed;
                }
            }
            catch
            {
            }

            if (discordRpcEnabled)
            {
                try
                {
                    DiscordRpcService.ApplyTheme(theme);
                }
                catch
                {
                }
            }
            else
            {
                try
                {
                    DiscordRpcService.Shutdown();
                }
                catch
                {
                }
            }

            if (string.Equals(theme, "Wave", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "WaveUI-2025", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "WaveUI-2026/2", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "Synapse X", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "KRNL", System.StringComparison.OrdinalIgnoreCase))
            {
                TitleBar.Visibility = Visibility.Collapsed;
                TitleBarRow.Height = new GridLength(0);
                ThemeHost.Content = new WaveShell();

                Background = Brushes.Transparent;
                RootBorder.Background = Brushes.Black;
                Width = 1000;
                Height = 600;
                ResizeMode = ResizeMode.CanResize;
                MinWidth = Width;
                MinHeight = Height;
            }
        }

        private void OnLanguageChanged()
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            var version = GetThemeVersion(_currentTheme);
            var name = GetThemeExecutorName(_currentTheme);
            var title = string.IsNullOrWhiteSpace(version)
                ? name
                : $"{name} {version}";
            TitleText.Text = title;
            Title = title;
        }

        private static string GetThemeExecutorName(string? theme)
        {
            var t = (theme ?? string.Empty).Trim();
            if (string.Equals(t, "KRNL", StringComparison.OrdinalIgnoreCase))
            {
                return "KRNL Executor";
            }

            if (string.Equals(t, "Synapse X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "SynapseX", StringComparison.OrdinalIgnoreCase))
            {
                return "Synapse X Executor";
            }

            return "Wave Executor";
        }

        private static string GetThemeVersion(string? theme)
        {
            var t = (theme ?? string.Empty).Trim();
            if (string.Equals(t, "WaveUI-2026/2", StringComparison.OrdinalIgnoreCase))
            {
                return "v2026/2";
            }

            if (string.Equals(t, "WaveUI-2025", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "Wave", StringComparison.OrdinalIgnoreCase))
            {
                return "v2025";
            }

            return string.Empty;
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}