using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Executor.WaveUI.WaveViews
{
    public partial class EditorView : UserControl
    {
        private readonly Action<string> _toast;
        private bool _isExplorerOpen = true;

        private const double ExplorerWidth = 280;
        private const double ExplorerGap = 10;
        private static readonly Thickness ExplorerOpenMargin = new(0, 0, ExplorerWidth + ExplorerGap, 0);
        private static readonly Thickness ExplorerClosedMargin = new(0, 0, 0, 0);

        public EditorView(Action<string> toast)
        {
            InitializeComponent();
            _toast = toast;

            ApplyExplorerState(animated: false);
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
            _toast("User authorized, successfully started Wave!");
        }

        private void Clear_OnClick(object sender, RoutedEventArgs e)
        {
            EditorBox.Clear();
        }
    }
}
