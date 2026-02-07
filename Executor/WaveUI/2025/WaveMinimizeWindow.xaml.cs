using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace Executor.WaveUI
{
    public partial class WaveMinimizeWindow : Window
    {
        private Point _dragStart;
        private Point _windowStart;

        private Vector _dragVector;
        private DateTime _dragStartTime;
        private DateTime _lastDragMoveTime;
        private bool _dragTimingActive;
        private bool _isDragging;
        private bool _isPressed;
        private bool _hasMoved;
        private const double DragThreshold = 5;
        private const int LongPressMs = 220;
        private const int ThrowDurationMs = 420;
        private const int ThrowMinDurationMs = 160;
        private const double MaxThrowDistance = 300;
        private const double ThrowMultiplier = 1.2;
        private const double MinThrowDistance = 12;
        private const double MinThrowSpeed = 0.08;
        private const int ThrowIdleThresholdMs = 120;
        private const double BounceDamping = 0.5;
        private const int PulseDurationMs = 1000;
        private const int PulseVisibleMs = 500;
        public const string EffectPulse = "pulse";
        public const string EffectFade = "fade";
        private bool _hasCustomPosition;
        private readonly DispatcherTimer _holdTimer;
        private readonly Stopwatch _pulseStopwatch = new();
        private TimeSpan _pulseFrameInterval = TimeSpan.FromMilliseconds(1000.0 / DefaultFrameRate);
        private TimeSpan _lastPulseFrame;
        private bool _pulseRenderingHooked;
        private DateTime _pulseStart;
        private bool _isPulsing;
        private bool _isFading;
        private double _targetOpacity = 1.0;
        private string _currentEffect = EffectPulse;
        private int _frameRate = DefaultFrameRate;

        private const int DefaultFrameRate = 60;
        private const int MinFrameRate = 30;
        private const int MaxFrameRate = 120;
        private const string FrameRateKey = "WaveUI_small_wave_fps";
        private const string EffectKey = "WaveUI_small_wave_effect";
        private const string PositionLeftKey = "WaveUI_minimize_window_left";
        private const string PositionTopKey = "WaveUI_minimize_window_top";

        public event EventHandler? RestoreRequested;

        public WaveMinimizeWindow()
        {
            InitializeComponent();
            ApplyThemeLogoFromConfig();
            Opacity = 0;
            _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressMs) };
            _holdTimer.Tick += HoldTimer_Tick;
            _pulseStart = DateTime.UtcNow;
            LoadFrameRateFromConfig();
            LoadEffectFromConfig();
        }

        private void ApplyThemeLogoFromConfig()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var theme = ConfigManager.Get(cfg, "theme") ?? string.Empty;
                ThemeLogoIcon.IconName = GetThemeLogoIconName(theme);
            }
            catch
            {
            }
        }

        private static string GetThemeLogoIconName(string? theme)
        {
            var t = (theme ?? string.Empty).Trim();
            if (string.Equals(t, "KRNL", StringComparison.OrdinalIgnoreCase))
            {
                return "krnl";
            }

            if (string.Equals(t, "Synapse X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "SynapseX", StringComparison.OrdinalIgnoreCase))
            {
                return "synx";
            }

            return "wave";
        }

        public void ApplyOpacity(double opacity)
        {
            _targetOpacity = Clamp(opacity, 0.1, 1.0);

            if (Opacity <= 0)
            {
                return;
            }

            BeginAnimation(Window.OpacityProperty, null);
            Opacity = _targetOpacity;

            if (string.Equals(_currentEffect, EffectFade, StringComparison.OrdinalIgnoreCase))
            {
                StartFadeEffect();
            }
        }

        public void ApplyEffect(string? effect)
        {
            var normalized = string.IsNullOrWhiteSpace(effect) ? EffectPulse : effect.Trim();
            _currentEffect = normalized;

            if (string.Equals(normalized, EffectFade, StringComparison.OrdinalIgnoreCase))
            {
                StopPulse();
                StartFadeEffect();
                return;
            }

            StopFadeEffect();
            StartPulse();
        }

        public void ApplyFrameRate(int fps)
        {
            var clamped = ClampFrameRate(fps);
            _frameRate = clamped;
            UpdatePulseFrameInterval();
        }

        private void LoadFrameRateFromConfig()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var raw = ConfigManager.Get(cfg, FrameRateKey);
                if (int.TryParse(raw?.Trim(), out var fps))
                {
                    ApplyFrameRate(fps);
                    return;
                }
            }
            catch
            {
            }

            ApplyFrameRate(DefaultFrameRate);
        }

        private void LoadEffectFromConfig()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var raw = ConfigManager.Get(cfg, EffectKey);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var effect = raw.Trim();
                    if (string.Equals(effect, EffectFade, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(effect, EffectPulse, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentEffect = effect;
                        return;
                    }
                }
            }
            catch
            {
            }

            _currentEffect = EffectPulse;
        }

        public void ShowAtDefaultPosition()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var leftStr = ConfigManager.Get(cfg, PositionLeftKey);
                var topStr = ConfigManager.Get(cfg, PositionTopKey);

                if (!string.IsNullOrWhiteSpace(leftStr) && !string.IsNullOrWhiteSpace(topStr))
                {
                    if (double.TryParse(leftStr.Trim(), out var savedLeft) && double.TryParse(topStr.Trim(), out var savedTop))
                    {
                        var area = SystemParameters.WorkArea;
                        Left = Clamp(savedLeft, area.Left, area.Right - Width);
                        Top = Clamp(savedTop, area.Top, area.Bottom - Height);
                        _hasCustomPosition = true;
                        return;
                    }
                }
            }
            catch
            {
            }

            if (_hasCustomPosition)
            {
                return;
            }

            var defaultArea = SystemParameters.WorkArea;
            Left = defaultArea.Right - Width - 24;
            Top = defaultArea.Bottom - Height - 24;
        }

        public void SavePosition()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                ConfigManager.Set(cfg, PositionLeftKey, Left.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                ConfigManager.Set(cfg, PositionTopKey, Top.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                ConfigManager.WriteConfig(cfg);
            }
            catch
            {
            }
        }

        public Task FadeInAsync()
        {
            BeginAnimation(Window.OpacityProperty, null);
            var tcs = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation
            {
                From = Opacity,
                To = _targetOpacity,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            anim.Completed += (_, _) =>
            {
                Opacity = _targetOpacity;
                tcs.TrySetResult(true);

                // 淡入完成後套用特效
                ApplyEffect(_currentEffect);
            };
            BeginAnimation(Window.OpacityProperty, anim);
            return tcs.Task;
        }

        public Task FadeOutAsync()
        {
            BeginAnimation(Window.OpacityProperty, null);
            StopPulse();
            StopFadeEffect();
            var tcs = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation
            {
                From = Opacity,

                To = 0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            anim.Completed += (_, _) =>
            {
                Opacity = 0;
                tcs.TrySetResult(true);
            };
            BeginAnimation(Window.OpacityProperty, anim);
            return tcs.Task;
        }

        private void Root_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                _holdTimer.Stop();
                Root.ReleaseMouseCapture();
                _isPressed = false;
                _isDragging = false;
                _hasMoved = false;
                AnimateScale(1, 120);
                StopPulse();
                RestoreRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var mousePos = e.GetPosition(Root);
            _dragStart = PointToScreen(new Point(mousePos.X, mousePos.Y));
            _windowStart = new Point(Left, Top);
            _dragVector = new Vector(0, 0);
            var now = DateTime.UtcNow;
            _dragStartTime = now;
            _lastDragMoveTime = now;
            _dragTimingActive = false;

            _isPressed = true;
            _isDragging = false;
            _hasMoved = false;
            Root.CaptureMouse();
            Root.Cursor = Cursors.Hand;

            BeginAnimation(Window.LeftProperty, null);
            BeginAnimation(Window.TopProperty, null);
            AnimateScale(0.94, 120);

            _holdTimer.Stop();
            _holdTimer.Start();
        }

        private void Root_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPressed)
            {
                return;
            }


            var mousePos = e.GetPosition(Root);
            var currentPos = PointToScreen(new Point(mousePos.X, mousePos.Y));
            var delta = currentPos - _dragStart;

            // 檢查是否移動超過門檻
            if (!_hasMoved && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
            {
                _hasMoved = true;
                _hasCustomPosition = true;
                _isDragging = true;
                _holdTimer.Stop();
                if (!_dragTimingActive)
                {
                    StartDragTiming(DateTime.UtcNow);
                }
            }

            if (_hasMoved && _isDragging)
            {
                _dragVector = delta;
                UpdateDragTiming(DateTime.UtcNow);
                var area = SystemParameters.WorkArea;
                var newLeft = Clamp(_windowStart.X + delta.X, area.Left, area.Right - Width);
                var newTop = Clamp(_windowStart.Y + delta.Y, area.Top, area.Bottom - Height);
                Left = newLeft;
                Top = newTop;
            }

        }

        private void Root_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPressed)
            {
                return;
            }

            _isPressed = false;
            _holdTimer.Stop();
            Root.ReleaseMouseCapture();
            AnimateScale(1, 160);
            Root.Cursor = Cursors.Hand;

            if (_hasMoved && _isDragging)
            {
                TriggerThrow();
            }

            _isDragging = false;
            _dragTimingActive = false;
        }

        private void HoldTimer_Tick(object? sender, EventArgs e)
        {
            _holdTimer.Stop();

            if (!_isPressed)
            {
                return;
            }

            _isDragging = true;
            Root.Cursor = Cursors.SizeAll;
            _dragStart = PointToScreen(Mouse.GetPosition(Root));
            _dragVector = new Vector(0, 0);
            _hasMoved = false;
            _dragTimingActive = false;

            // 長按開始拖曳時繼續脈衝(如果還沒開始的話)
            if (!_isPulsing)
            {
                StartPulse();
            }
        }

        private void Root_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("TabContextMenuStyle"),
                PlacementTarget = Root,
            };

            var openText = Executor.LocalizationManager.T("WaveUI.SmallWave.Menu.Open");
            var closeText = Executor.LocalizationManager.T("WaveUI.SmallWave.Menu.Close");

            var openItem = new MenuItem
            {
                Header = openText,
                Icon = new WaveIcon { IconName = "maximize_wave", Width = 14, Height = 14, Stretch = Stretch.Uniform },
                Style = (Style)FindResource("TabContextMenuItemStyle"),
            };
            openItem.Click += (_, _) =>
            {
                StopPulse();
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            };
            menu.Items.Add(openItem);

            var separator = new Separator
            {
                Style = (Style)FindResource("TabContextMenuSeparatorStyle"),
            };
            menu.Items.Add(separator);

            var closeItem = new MenuItem
            {
                Header = closeText,
                Icon = new WaveIcon { IconName = "close", Width = 14, Height = 14, Stretch = Stretch.Uniform },
                Style = (Style)FindResource("TabContextMenuItemStyle"),
            };
            closeItem.Click += async (_, _) =>
            {
                await FadeOutAsync();
                Application.Current?.Shutdown();
            };
            menu.Items.Add(closeItem);

            Root.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void TriggerThrow()
        {
            if (!_dragTimingActive)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastDragMoveTime).TotalMilliseconds > ThrowIdleThresholdMs)
            {
                return;
            }

            var dragDistance = _dragVector.Length;
            if (dragDistance < MinThrowDistance)
            {
                return;
            }

            var dragDurationMs = Math.Max(1.0, (now - _dragStartTime).TotalMilliseconds);
            var speed = dragDistance / dragDurationMs;
            if (speed < MinThrowSpeed)
            {
                return;
            }

            var baseTimeMs = Clamp(dragDurationMs * 1.1, ThrowMinDurationMs, ThrowDurationMs);
            var throwDistance = speed * baseTimeMs * ThrowMultiplier;
            throwDistance = Math.Min(throwDistance, MaxThrowDistance);
            if (throwDistance < MinThrowDistance)
            {
                return;
            }

            var direction = _dragVector;
            direction.Normalize();
            var throwVector = direction * throwDistance;

            var durationMs = (int)Math.Round(Clamp(
                ThrowMinDurationMs + (throwDistance / MaxThrowDistance) * (ThrowDurationMs - ThrowMinDurationMs),
                ThrowMinDurationMs,
                ThrowDurationMs));

            var area = SystemParameters.WorkArea;
            BeginAnimation(Window.LeftProperty, null);
            BeginAnimation(Window.TopProperty, null);

            var animX = BuildThrowAnimation(Left, throwVector.X, area.Left, area.Right - Width, durationMs, out var targetLeft);
            var animY = BuildThrowAnimation(Top, throwVector.Y, area.Top, area.Bottom - Height, durationMs, out var targetTop);

            ApplyFrameRateToAnimation(animX);
            ApplyFrameRateToAnimation(animY);

            animX.Completed += (_, _) =>
            {
                BeginAnimation(Window.LeftProperty, null);
                Left = targetLeft;
            };

            animY.Completed += (_, _) =>
            {
                BeginAnimation(Window.TopProperty, null);
                Top = targetTop;
            };

            BeginAnimation(Window.LeftProperty, animX);
            BeginAnimation(Window.TopProperty, animY);
        }

        private void UpdatePulseFrameInterval()
        {
            _pulseFrameInterval = GetFrameInterval();
            _lastPulseFrame = _pulseStopwatch.IsRunning ? _pulseStopwatch.Elapsed : TimeSpan.Zero;
        }

        private TimeSpan GetFrameInterval()
        {
            var fps = Math.Max(1, _frameRate);
            return TimeSpan.FromMilliseconds(1000.0 / fps);
        }

        private static int ClampFrameRate(int fps)
        {
            if (fps < MinFrameRate)
            {
                return MinFrameRate;
            }

            if (fps > MaxFrameRate)
            {
                return MaxFrameRate;
            }

            return fps;
        }

        private void ApplyFrameRateToAnimation(AnimationTimeline? timeline)
        {
            if (timeline == null)
            {
                return;
            }

            Timeline.SetDesiredFrameRate(timeline, _frameRate);
        }

        private void StartDragTiming(DateTime now)
        {
            _dragStartTime = now;
            _lastDragMoveTime = now;
            _dragTimingActive = true;
        }

        private void UpdateDragTiming(DateTime now)
        {
            if (!_dragTimingActive)
            {
                StartDragTiming(now);
                return;
            }

            _lastDragMoveTime = now;
        }

        private AnimationTimeline BuildThrowAnimation(double start, double delta, double min, double max, int durationMs, out double finalValue)
        {
            var rawTarget = start + delta;
            if (rawTarget >= min && rawTarget <= max)
            {
                finalValue = rawTarget;
                return new DoubleAnimation
                {
                    From = start,
                    To = finalValue,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
            }

            if (Math.Abs(delta) < 0.1 || durationMs <= 0)
            {
                finalValue = Clamp(rawTarget, min, max);
                return new DoubleAnimation
                {
                    From = start,
                    To = finalValue,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
            }

            var direction = delta >= 0 ? 1 : -1;
            var wall = direction > 0 ? max : min;
            var distanceToWall = Math.Abs(wall - start);
            var remaining = Math.Max(0.0, Math.Abs(delta) - distanceToWall);
            if (remaining <= 0.1)
            {
                finalValue = Clamp(wall, min, max);
                return new DoubleAnimation
                {
                    From = start,
                    To = finalValue,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
            }

            var bounceDistance = remaining * BounceDamping;
            finalValue = Clamp(wall - direction * bounceDistance, min, max);

            var timeToWallMs = durationMs * (distanceToWall / Math.Abs(delta));
            if (timeToWallMs <= 1 || timeToWallMs >= durationMs)
            {
                return new DoubleAnimation
                {
                    From = start,
                    To = finalValue,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
            }

            var keyframes = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            keyframes.KeyFrames.Add(new LinearDoubleKeyFrame(wall, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(timeToWallMs))));
            keyframes.KeyFrames.Add(new EasingDoubleKeyFrame(finalValue, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });

            return keyframes;
        }

        private void StartPulse()
        {
            if (_isPulsing)
            {
                return;
            }

            _isPulsing = true;
            _pulseStart = DateTime.UtcNow;
            _pulseStopwatch.Restart();
            _lastPulseFrame = TimeSpan.Zero;
            UpdatePulseFrameInterval();
            AttachPulseRendering();
            PulseCanvas?.InvalidateVisual();
        }

        private void StopPulse()
        {
            if (!_isPulsing)
            {
                return;
            }

            _isPulsing = false;
            DetachPulseRendering();
            PulseCanvas?.InvalidateVisual();
        }

        private void StartFadeEffect()
        {
            if (Root == null)
            {
                return;
            }

            _isFading = true;
            var minOpacity = Math.Max(0.1, _targetOpacity * 0.35);
            var maxOpacity = _targetOpacity;

            Root.BeginAnimation(UIElement.OpacityProperty, null);

            var anim = new DoubleAnimation
            {
                From = maxOpacity,
                To = minOpacity,
                Duration = TimeSpan.FromMilliseconds(1000),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            };

            ApplyFrameRateToAnimation(anim);

            Root.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void StopFadeEffect()
        {
            if (!_isFading)
            {
                return;
            }

            _isFading = false;

            if (Root == null)
            {
                return;
            }

            Root.BeginAnimation(UIElement.OpacityProperty, null);
            Root.Opacity = 1;
        }

        private void AttachPulseRendering()
        {
            if (_pulseRenderingHooked)
            {
                return;
            }

            CompositionTarget.Rendering += PulseRendering_Tick;
            _pulseRenderingHooked = true;
        }

        private void DetachPulseRendering()
        {
            if (!_pulseRenderingHooked)
            {
                return;
            }

            CompositionTarget.Rendering -= PulseRendering_Tick;
            _pulseRenderingHooked = false;
            _pulseStopwatch.Stop();
        }

        private void PulseRendering_Tick(object? sender, EventArgs e)
        {
            if (!_isPulsing)
            {
                DetachPulseRendering();
                return;
            }

            if (!_pulseStopwatch.IsRunning)
            {
                _pulseStopwatch.Start();
                _lastPulseFrame = TimeSpan.Zero;
            }

            var elapsed = _pulseStopwatch.Elapsed;
            if (elapsed - _lastPulseFrame < _pulseFrameInterval)
            {
                return;
            }

            _lastPulseFrame = elapsed;
            PulseCanvas?.InvalidateVisual();
        }

        private void PulseCanvas_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (!_isPulsing)
            {
                return;
            }

            var info = e.Info;
            var size = Math.Min(info.Width, info.Height);
            var minRadius = size * 0.22f;
            var maxRadius = size * 0.48f;

            var elapsedMs = _pulseStopwatch.IsRunning
                ? _pulseStopwatch.Elapsed.TotalMilliseconds
                : (DateTime.UtcNow - _pulseStart).TotalMilliseconds;
            var cycle = elapsedMs % PulseDurationMs;

            if (cycle > PulseVisibleMs)
            {
                return;
            }

            var progress = cycle / PulseVisibleMs;
            var radius = minRadius + (maxRadius - minRadius) * (float)progress;
            var alpha = (byte)(Math.Max(0, 1.0 - progress) * 100);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = size * 0.06f,
                Color = new SKColor(0x1D, 0xA1, 0xF2, alpha),
            };

            canvas.DrawCircle(info.Width / 2f, info.Height / 2f, radius, paint);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void AnimateScale(double target, int durationMs)
        {
            if (RootScale == null)
            {
                return;
            }

            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            var anim = new DoubleAnimation
            {
                From = RootScale.ScaleX,
                To = target,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
    }
}