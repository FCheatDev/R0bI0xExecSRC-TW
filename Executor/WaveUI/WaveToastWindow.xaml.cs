using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Executor.WaveUI
{
    public partial class WaveToastWindow : Window
    {
        private readonly TranslateTransform _translate = new(24, 0);
        private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);
        private int _toastSequence;
        private DispatcherTimer? _hideTimer;
        private double _progressTargetWidth;
        private bool _isShowing;

        public WaveToastWindow()
        {
            InitializeComponent();
            Opacity = 0;
            RootGrid.RenderTransform = _translate;
        }

        public void ShowToast(string title, string message)
        {
            _toastSequence++;
            var seq = _toastSequence;

            var startDelay = TimeSpan.FromMilliseconds(180);

            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastProgress.Width = 0;

            // 停止所有動畫
            BeginAnimation(OpacityProperty, null);
            _translate.BeginAnimation(TranslateTransform.XProperty, null);
            ToastProgress.BeginAnimation(WidthProperty, null);
            ProgressTrack.BeginAnimation(OpacityProperty, null);

            // 停止舊的計時器
            _hideTimer?.Stop();
            _hideTimer = null;

            // 重置狀態
            _isShowing = true;
            Opacity = 0;
            _translate.X = 24;

            // 先顯示視窗
            if (!IsVisible)
            {
                Show();
            }

            // 強制更新佈局
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Arrange(new Rect(DesiredSize));
            UpdateLayout();

            // 定位視窗
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;

            // 淡入動畫
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                FillBehavior = FillBehavior.HoldEnd,
            };

            // 滑入動畫
            var slideIn = new DoubleAnimation
            {
                From = 24,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            fadeIn.Completed += (_, _) =>
            {
                if (seq != _toastSequence) return;

                Opacity = 1;
                _translate.X = 0;
            };

            // 獲取進度條寬度
            UpdateLayout();
            var targetWidth = ProgressTrack.ActualWidth;
            if (targetWidth <= 0)
            {
                targetWidth = 292;
            }
            _progressTargetWidth = targetWidth;

            // 進度條動畫
            var progress = new DoubleAnimation
            {
                From = 0,
                To = targetWidth,
                Duration = ToastDuration,
                BeginTime = startDelay,
                FillBehavior = FillBehavior.HoldEnd,
            };

            // 開始動畫
            BeginAnimation(OpacityProperty, fadeIn);
            _translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
            ToastProgress.BeginAnimation(WidthProperty, progress);

            // 設置自動隱藏計時器
            _hideTimer = new DispatcherTimer
            {
                Interval = ToastDuration + startDelay + TimeSpan.FromMilliseconds(100)
            };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer?.Stop();
                if (seq != _toastSequence || !_isShowing) return;

                BeginHide(seq);
            };
            _hideTimer.Start();
        }

        private void BeginHide(int seq)
        {
            if (seq != _toastSequence || !_isShowing)
            {
                return;
            }

            _isShowing = false;

            // 停止所有動畫
            BeginAnimation(OpacityProperty, null);
            _translate.BeginAnimation(TranslateTransform.XProperty, null);
            ToastProgress.BeginAnimation(WidthProperty, null);

            // 設置進度條為滿
            ToastProgress.Width = _progressTargetWidth;

            var fadeOut = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            var slideOut = new DoubleAnimation
            {
                From = _translate.X,
                To = -24,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            fadeOut.Completed += (_, _) =>
            {
                if (seq != _toastSequence) return;

                Opacity = 0;
                _translate.X = 24;
                Hide();
            };

            BeginAnimation(OpacityProperty, fadeOut);
            _translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (ActualWidth > 0 && ActualHeight > 0 && _isShowing)
            {
                var area = SystemParameters.WorkArea;
                Left = area.Right - ActualWidth - 16;
                Top = area.Bottom - ActualHeight - 16;
            }
        }
    }
}