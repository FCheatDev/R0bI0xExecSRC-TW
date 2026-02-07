using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Executor;

namespace Executor.WaveUI.WaveViews
{
    public partial class LoadView : UserControl
    {
        private DispatcherTimer? _sequenceTimer;
        private bool _finished;

        private const double BarWidth = 220;
        private static readonly TimeSpan PulseRunDuration = TimeSpan.FromMilliseconds(220);

        public event Action? LoadCompleted;
        public event Action? SkipRequested;

        public LoadView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            ApplySkipVisibility();

            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);
            ProgressFill.Width = 0;

            _finished = false;

            _sequenceTimer?.Stop();
            _sequenceTimer = null;

            RunToTenPercent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _sequenceTimer?.Stop();
            _sequenceTimer = null;
        }

        private void ApplyLanguage()
        {
            try
            {
                if (SkipButtonText != null)
                {
                    SkipButtonText.Text = LocalizationManager.T("WaveUI.Common.Skip");
                }
            }
            catch
            {
            }
        }

        private void ApplySkipVisibility()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var raw = ConfigManager.Get(cfg, "WaveUI_skip_load_app");
                var enabled = string.Equals(raw?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

                if (SkipButton != null)
                {
                    SkipButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch
            {
            }
        }

        private void RunToTenPercent()
        {
            var target = BarWidth * 0.1;

            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);

            var anim = new DoubleAnimation
            {
                From = ProgressFill.Width,
                To = target,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            anim.Completed += (_, _) =>
            {
                ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);
                ProgressFill.Width = target;

                _sequenceTimer?.Stop();
                _sequenceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                _sequenceTimer.Tick += (_, _) =>
                {
                    _sequenceTimer?.Stop();
                    RunToFull();
                };
                _sequenceTimer.Start();
            };

            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private void RunToFull()
        {
            var target = BarWidth;

            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);

            var anim = new DoubleAnimation
            {
                From = ProgressFill.Width,
                To = target,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };

            anim.Completed += (_, _) =>
            {
                ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);
                ProgressFill.Width = target;

                _sequenceTimer?.Stop();
                _sequenceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                _sequenceTimer.Tick += (_, _) =>
                {
                    _sequenceTimer?.Stop();
                    _sequenceTimer = null;

                    if (_finished)
                    {
                        return;
                    }

                    _finished = true;
                    LoadCompleted?.Invoke();
                };
                _sequenceTimer.Start();
            };

            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private void Root_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void SkipButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;

            try
            {
                _sequenceTimer?.Stop();
                _sequenceTimer = null;
            }
            catch
            {
            }

            try
            {
                SkipRequested?.Invoke();
            }
            catch
            {
            }
        }
    }
}
