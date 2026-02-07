using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Executor;
using Executor.WaveUI;

namespace Executor.WaveUI.WaveViews
{
    public partial class KeySystemView : UserControl
    {
        private readonly Action _onVerified;
        private DispatcherTimer? _verifyTimer;
        private DispatcherTimer? _postVerifyTimer;
        private DispatcherTimer? _autoVerifyDelayTimer;
        private bool _isVerifying;
        private const string SkipLoadAppKey = "WaveUI_skip_load_app";
        private const string WaveKey = "key";

        private void ApplyLanguage()
        {
            try
            {
                if (SkipButtonText != null)
                {
                    SkipButtonText.Text = LocalizationManager.T("WaveUI.Common.Skip");
                }

                if (TitleText != null)
                {
                    TitleText.Text = LocalizationManager.T("WaveUI.KeySystem.Title");
                }

                if (SubtitleText != null)
                {
                    SubtitleText.Text = LocalizationManager.T("WaveUI.KeySystem.Subtitle");
                }

                if (LoginButtonText != null)
                {
                    LoginButtonText.Text = LocalizationManager.T("WaveUI.KeySystem.Login");
                }

                if (GetKeyButtonText != null)
                {
                    GetKeyButtonText.Text = LocalizationManager.T("WaveUI.KeySystem.GetKey");
                }

                if (VerifyingText != null)
                {
                    VerifyingText.Text = LocalizationManager.T("WaveUI.KeySystem.Verifying");
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
                var raw = ConfigManager.Get(cfg, SkipLoadAppKey);
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

        private bool ValidateKey(out string? error)
        {
            error = null;
            var entered = KeyBox?.Text?.Trim() ?? "";
            if (entered.Length == 0)
            {
                error = LocalizationManager.T("WaveUI.KeySystem.Error.EnterKey");
                return false;
            }

            try
            {
                var cfg = ConfigManager.ReadConfig();
                var expected = ConfigManager.Get(cfg, "license_key") ?? ConfigManager.Get(cfg, WaveKey);
                if (!string.IsNullOrWhiteSpace(expected))
                {
                    if (!string.Equals(expected.Trim(), entered, StringComparison.Ordinal))
                    {
                        error = LocalizationManager.T("WaveUI.KeySystem.Error.InvalidKey");
                        return false;
                    }

                    return true;
                }
            }
            catch
            {
            }
            return true;
        }

        private void PersistKeyIfNeeded()
        {
            var entered = KeyBox?.Text?.Trim() ?? "";
            if (entered.Length == 0)
            {
                return;
            }

            try
            {
                var cfg = ConfigManager.ReadConfig();
                var licenseKey = ConfigManager.Get(cfg, "license_key");
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    return;
                }

                var existing = ConfigManager.Get(cfg, WaveKey) ?? "";
                if (string.Equals(existing, entered, StringComparison.Ordinal))
                {
                    return;
                }

                ConfigManager.Set(cfg, WaveKey, entered);
                ConfigManager.WriteConfig(cfg);
            }
            catch
            {
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WaveToastService.Show(LocalizationManager.T("WaveUI.Common.Error"), ex.Message);
            }
        }

        private void YouTube_OnClick(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.youtube.com/@AzureaXD");
        }

        private void Discord_OnClick(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://dsc.gg/ax-exec");
        }

        private void Global_OnClick(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://axproject.dpdns.org/");
        }

        private void StartVerifyFlow()
        {
            if (_isVerifying)
            {
                return;
            }

            _isVerifying = true;
            VerifyOverlay.Visibility = Visibility.Visible;
            VerifyOverlay.BeginAnimation(OpacityProperty, null);
            VerifyOverlay.Opacity = 0;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            VerifyOverlay.BeginAnimation(OpacityProperty, fadeIn);

            _verifyTimer?.Stop();
            _postVerifyTimer?.Stop();

            _verifyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _verifyTimer.Tick += (_, _) =>
            {
                _verifyTimer?.Stop();

                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };

                fadeOut.Completed += (_, _) =>
                {
                    VerifyOverlay.Visibility = Visibility.Collapsed;
                    LockIcon.IconName = "unlock";

                    _postVerifyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1050) };
                    _postVerifyTimer.Tick += (_, _) =>
                    {
                        _postVerifyTimer?.Stop();
                        _isVerifying = false;
                        _onVerified();
                    };
                    _postVerifyTimer.Start();
                };

                VerifyOverlay.BeginAnimation(OpacityProperty, fadeOut);
            };
            _verifyTimer.Start();
        }

        private void TryAutoVerifyFromConfig()
        {
            try
            {
                var cfg = ConfigManager.ReadConfig();
                var expected = ConfigManager.Get(cfg, "license_key") ?? ConfigManager.Get(cfg, WaveKey);
                if (string.IsNullOrWhiteSpace(expected))
                {
                    return;
                }

                if (KeyBox != null)
                {
                    KeyBox.Text = expected.Trim();
                }

                PersistKeyIfNeeded();

                _autoVerifyDelayTimer?.Stop();
                _autoVerifyDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                _autoVerifyDelayTimer.Tick += (_, _) =>
                {
                    _autoVerifyDelayTimer?.Stop();
                    StartVerifyFlow();
                };
                _autoVerifyDelayTimer.Start();
            }
            catch
            {
            }
        }

        public KeySystemView(Action onVerified)
        {
            InitializeComponent();
            _onVerified = onVerified;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            ApplySkipVisibility();

            var rt = new RotateTransform(0, 26, 26);
            SpinnerArc.RenderTransform = rt;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(900),
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, anim);

            TryAutoVerifyFromConfig();
        }

        private void SkipButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _verifyTimer?.Stop();
                _verifyTimer = null;

                _postVerifyTimer?.Stop();
                _postVerifyTimer = null;

                _autoVerifyDelayTimer?.Stop();
                _autoVerifyDelayTimer = null;

                _isVerifying = false;

                if (VerifyOverlay != null)
                {
                    VerifyOverlay.BeginAnimation(OpacityProperty, null);
                    VerifyOverlay.Opacity = 0;
                    VerifyOverlay.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
            }

            try
            {
                _onVerified();
            }
            catch
            {
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _verifyTimer?.Stop();
            _verifyTimer = null;

            _postVerifyTimer?.Stop();
            _postVerifyTimer = null;

            _autoVerifyDelayTimer?.Stop();
            _autoVerifyDelayTimer = null;

            _isVerifying = false;
        }

        private void Login_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ValidateKey(out var error))
            {
                WaveToastService.Show(LocalizationManager.T("WaveUI.Common.Error"), error ?? LocalizationManager.T("WaveUI.KeySystem.Error.InvalidKey"));
                return;
            }

            PersistKeyIfNeeded();
            StartVerifyFlow();
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
    }
}
