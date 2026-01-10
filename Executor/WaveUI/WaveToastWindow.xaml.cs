using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Executor.WaveUI
{
    public partial class WaveToastWindow : Window
    {
        private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ReflowDelay = TimeSpan.FromSeconds(1);

        private const int MaxToasts = 10;

        private const double StackSpacing = 8;
        private const double WindowRightMargin = 16;
        private const double WindowBottomMargin = 16;
        private const double MinToastWindowHeight = 56;
        private const double OverlapPeekOffset = 10;
        private const double WindowTopPadding = 18;

        public ObservableCollection<ToastEntry> Toasts { get; } = new();

        private int _toastSequence;
        private DispatcherTimer? _reflowTimer;

        public WaveToastWindow()
        {
            InitializeComponent();
            DataContext = this;
            Opacity = 1;
        }

        public void ShowToast(string title, string message)
        {
            _toastSequence++;

            if (Toasts.Count >= MaxToasts && Toasts.Count > 0)
            {
                BeginHide(Toasts[0]);
            }

            var toast = new ToastEntry(_toastSequence, title, message);
            Toasts.Add(toast);

            if (!IsVisible)
            {
                Show();
            }

            UpdateLayout();
            UpdateWindowPlacement();

            ReflowToasts(animated: false);
        }

        private void OverlapToasts()
        {
            double maxToastHeight = 0;

            // Newest toast stays at the bottom/front (Y=0, highest Z).
            // Older toasts are behind it but peek upward (negative Y).
            for (var peekIndex = 0; peekIndex < Toasts.Count; peekIndex++)
            {
                var toast = Toasts[Toasts.Count - 1 - peekIndex];
                toast.ZIndex = 1000 + toast.Id;

                if (toast.RootElement == null)
                {
                    continue;
                }

                Panel.SetZIndex(toast.RootElement, toast.ZIndex);

                var translate = EnsureTranslate(toast.RootElement);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.Y = -(OverlapPeekOffset * peekIndex);

                maxToastHeight = Math.Max(maxToastHeight, GetToastHeight(toast));
            }

            var overlapHeight = maxToastHeight + (OverlapPeekOffset * Math.Max(0, Toasts.Count - 1));
            overlapHeight = Math.Max(MinToastWindowHeight, overlapHeight);

            if (overlapHeight > 0)
            {
                var area = SystemParameters.WorkArea;
                var maxHeight = Math.Max(120, area.Height - (WindowBottomMargin * 2));
                Height = Math.Min(maxHeight, overlapHeight);
                UpdateLayout();
                UpdateWindowPlacement();
            }
        }

        private void RestartReflowTimer()
        {
            _reflowTimer?.Stop();

            _reflowTimer = new DispatcherTimer { Interval = ReflowDelay };
            _reflowTimer.Tick += (_, _) =>
            {
                _reflowTimer?.Stop();
                _reflowTimer = null;
                ReflowToasts(animated: true);
            };
            _reflowTimer.Start();
        }

        private void ReflowToasts(bool animated)
        {
            double offset = 0;

            for (var i = Toasts.Count - 1; i >= 0; i--)
            {
                var toast = Toasts[i];
                toast.ZIndex = 1000 + toast.Id;

                var targetY = -offset;
                offset += GetToastHeight(toast) + StackSpacing;

                if (toast.RootElement == null)
                {
                    continue;
                }

                Panel.SetZIndex(toast.RootElement, toast.ZIndex);

                var translate = EnsureTranslate(toast.RootElement);

                if (!animated)
                {
                    translate.Y = targetY;
                    continue;
                }

                translate.BeginAnimation(TranslateTransform.YProperty, null);
                var anim = new DoubleAnimation
                {
                    From = translate.Y,
                    To = targetY,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
                anim.Completed += (_, _) => translate.Y = targetY;
                translate.BeginAnimation(TranslateTransform.YProperty, anim);
            }

            var desiredHeight = Math.Max(0, offset - StackSpacing);
            desiredHeight = Math.Max(MinToastWindowHeight, desiredHeight);
            desiredHeight += WindowTopPadding;
            var area = SystemParameters.WorkArea;
            var maxHeight = Math.Max(120, area.Height - (WindowBottomMargin * 2));
            Height = Math.Min(maxHeight, desiredHeight);

            UpdateLayout();
            UpdateWindowPlacement();
        }

        private void UpdateWindowPlacement()
        {
            var area = SystemParameters.WorkArea;

            if (ActualWidth <= 0)
            {
                return;
            }

            Left = area.Right - ActualWidth - WindowRightMargin;
            Top = area.Bottom - Height - WindowBottomMargin;
        }

        private static TranslateTransform EnsureTranslate(UIElement element)
        {
            if (element.RenderTransform is TranslateTransform t)
            {
                return t;
            }

            var created = new TranslateTransform(0, 0);
            element.RenderTransform = created;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            return created;
        }

        private void ToastItemRoot_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement root)
            {
                return;
            }

            if (root.DataContext is not ToastEntry toast)
            {
                return;
            }

            toast.RootElement = root;
            Panel.SetZIndex(root, toast.ZIndex);

            root.Opacity = 0;

            var translate = EnsureTranslate(root);
            translate.X = 24;

            root.UpdateLayout();
            toast.MeasuredHeight = Math.Max(0, Math.Max(root.ActualHeight, root.DesiredSize.Height));

            root.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (toast.IsHiding || toast.RootElement == null)
                {
                    return;
                }

                toast.MeasuredHeight = Math.Max(0, Math.Max(root.ActualHeight, root.DesiredSize.Height));

                ReflowToasts(animated: false);
            }), DispatcherPriority.Loaded);

            StartShowAnimation(root, translate);
            StartProgressAnimation(root);
            StartHideTimer(toast);
        }

        private void StartShowAnimation(UIElement root, TranslateTransform translate)
        {
            root.BeginAnimation(OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.XProperty, null);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                FillBehavior = FillBehavior.HoldEnd,
            };

            var slideIn = new DoubleAnimation
            {
                From = translate.X,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            fadeIn.Completed += (_, _) => root.Opacity = 1;
            slideIn.Completed += (_, _) => translate.X = 0;

            root.BeginAnimation(OpacityProperty, fadeIn);
            translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private void StartHideTimer(ToastEntry toast)
        {
            toast.HideTimer?.Stop();

            toast.HideTimer = new DispatcherTimer { Interval = ToastDuration + TimeSpan.FromMilliseconds(280) };
            toast.HideTimer.Tick += (_, _) =>
            {
                toast.HideTimer?.Stop();
                toast.HideTimer = null;
                BeginHide(toast);
            };
            toast.HideTimer.Start();
        }

        private static double GetToastHeight(ToastEntry toast)
        {
            if (toast.MeasuredHeight > 0)
            {
                return toast.MeasuredHeight;
            }

            if (toast.RootElement == null)
            {
                return MinToastWindowHeight;
            }

            var h = Math.Max(toast.RootElement.ActualHeight, toast.RootElement.DesiredSize.Height);
            return Math.Max(MinToastWindowHeight, h);
        }

        private void BeginHide(ToastEntry toast)
        {
            if (toast.IsHiding)
            {
                return;
            }

            toast.IsHiding = true;

            toast.HideTimer?.Stop();
            toast.HideTimer = null;

            if (toast.RootElement == null)
            {
                Toasts.Remove(toast);
                ReflowToasts(animated: false);
                if (Toasts.Count == 0) Hide();
                return;
            }

            var root = toast.RootElement;
            var translate = EnsureTranslate(root);

            root.BeginAnimation(OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.XProperty, null);

            var fadeOut = new DoubleAnimation
            {
                From = root.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            fadeOut.Completed += (_, _) => root.Opacity = 0;

            fadeOut.Completed += (_, _) =>
            {
                Toasts.Remove(toast);
                ReflowToasts(animated: false);
                if (Toasts.Count == 0)
                {
                    Hide();
                }
            };

            root.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ToastClose_OnClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ToastEntry toast)
            {
                return;
            }

            BeginHide(toast);
        }

        private static void StartProgressAnimation(DependencyObject root)
        {
            var track = FindChildByName<Border>(root, "ProgressTrack");
            var progress = FindChildByName<Border>(root, "ToastProgress");
            if (track == null || progress == null)
            {
                return;
            }

            track.Visibility = Visibility.Visible;
            track.BeginAnimation(UIElement.OpacityProperty, null);
            track.Opacity = 1;

            progress.BeginAnimation(FrameworkElement.WidthProperty, null);
            progress.Width = 0;

            track.UpdateLayout();
            var targetWidth = track.ActualWidth;
            if (targetWidth <= 0)
            {
                targetWidth = 292;
            }

            var anim = new DoubleAnimation
            {
                From = 0,
                To = targetWidth,
                Duration = ToastDuration,
                BeginTime = TimeSpan.FromMilliseconds(180),
                FillBehavior = FillBehavior.HoldEnd,
            };
            anim.Completed += (_, _) => progress.Width = targetWidth;
            progress.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T fe && string.Equals(fe.Name, name, StringComparison.Ordinal))
                {
                    return fe;
                }

                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (ActualWidth > 0 && ActualHeight > 0 && IsVisible)
            {
                UpdateWindowPlacement();
            }
        }

        public sealed class ToastEntry : INotifyPropertyChanged
        {
            private int _zIndex;
            private double _measuredHeight;
            private bool _isRunning;

            internal ToastEntry(int id, string title, string message)
            {
                Id = id;
                Title = title;
                Message = message;
                _zIndex = 1000 + id;
            }

            public int Id { get; }
            public string Title { get; }
            public string Message { get; }

            internal DispatcherTimer? HideTimer { get; set; }
            internal bool IsHiding { get; set; }
            internal bool IsRunning 
            { 
                get => _isRunning; 
                set 
                { 
                    if (value == _isRunning) return; 
                    _isRunning = value; 
                    OnPropertyChanged(); 
                } 
            }

            internal FrameworkElement? RootElement { get; set; }

            public int ZIndex
            {
                get => _zIndex;
                set
                {
                    if (value == _zIndex) return;
                    _zIndex = value;
                    OnPropertyChanged();
                }
            }

            public double MeasuredHeight
            {
                get => _measuredHeight;
                set
                {
                    if (Math.Abs(value - _measuredHeight) < 0.1) return;
                    _measuredHeight = value;
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