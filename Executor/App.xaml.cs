using System.Configuration;
using System.Data;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Executor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal const string DefaultWaveFontFamily = "Segoe UI";
        internal const string DefaultWaveHeadingFontFamily = "Comic Sans MS";
        internal const string DefaultWaveTitleFontFamily = "Arial Black";
        internal const string DefaultWaveMonoFontFamily = "Consolas";
        internal const string DefaultWaveSubtitleFontFamily = "Bahnschrift";
        internal const string DefaultWaveBodyAltFontFamily = "Roboto";
        private const string MultiInstanceKey = "allow_multi_instance";
        private const string SingleInstanceMutexName = "Wave.Executor.SingleInstance";
        private static Mutex? _singleInstanceMutex;

        internal static void ApplyWaveFont(string? fontName)
        {
            var hasCustom = !string.IsNullOrWhiteSpace(fontName);
            var resolved = hasCustom
                ? fontName!.Trim()
                : DefaultWaveFontFamily;
            var heading = hasCustom ? resolved : DefaultWaveHeadingFontFamily;
            var title = hasCustom ? resolved : DefaultWaveTitleFontFamily;
            var mono = hasCustom ? resolved : DefaultWaveMonoFontFamily;
            var subtitle = hasCustom ? resolved : DefaultWaveSubtitleFontFamily;
            var alt = hasCustom ? resolved : DefaultWaveBodyAltFontFamily;

            try
            {
                if (Current == null)
                {
                    return;
                }

                ApplyFontResource(Current, "WaveFontFamily", resolved);
                ApplyFontResource(Current, "WaveHeadingFontFamily", heading);
                ApplyFontResource(Current, "WaveTitleFontFamily", title);
                ApplyFontResource(Current, "WaveMonoFontFamily", mono);
                ApplyFontResource(Current, "WaveSubtitleFontFamily", subtitle);
                ApplyFontResource(Current, "WaveBodyAltFontFamily", alt);
            }
            catch
            {
                try
                {
                    if (Current != null)
                    {
                        ApplyFontResource(Current, "WaveFontFamily", DefaultWaveFontFamily);
                        ApplyFontResource(Current, "WaveHeadingFontFamily", DefaultWaveHeadingFontFamily);
                        ApplyFontResource(Current, "WaveTitleFontFamily", DefaultWaveTitleFontFamily);
                        ApplyFontResource(Current, "WaveMonoFontFamily", DefaultWaveMonoFontFamily);
                        ApplyFontResource(Current, "WaveSubtitleFontFamily", DefaultWaveSubtitleFontFamily);
                        ApplyFontResource(Current, "WaveBodyAltFontFamily", DefaultWaveBodyAltFontFamily);
                    }
                }
                catch
                {
                }
            }
        }

        private static void ApplyFontResource(Application app, string key, string value)
        {
            app.Resources[key] = new FontFamily(value);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Initialize();

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                {
                    try
                    {
                        if (args.ExceptionObject is Exception ex)
                        {
                            Logger.Exception("UnhandledException", ex);
                        }
                        else
                        {
                            Logger.Error("UnhandledException", "Non-Exception: " + (args.ExceptionObject?.ToString() ?? "<null>"));
                        }
                    }
                    catch
                    {
                    }
                };

                DispatcherUnhandledException += (_, args) =>
                {
                    try
                    {
                        Logger.Exception("DispatcherUnhandledException", args.Exception);
                    }
                    catch
                    {
                    }
                };

                TaskScheduler.UnobservedTaskException += (_, args) =>
                {
                    try
                    {
                        Logger.Exception("UnobservedTaskException", args.Exception);
                    }
                    catch
                    {
                    }
                };
            }
            catch
            {
            }

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var cfg = ConfigManager.ReadConfig();
            var lang = ConfigManager.Get(cfg, "language");
            var fontRaw = ConfigManager.Get(cfg, "font");
            ApplyWaveFont(fontRaw);
            LocalizationManager.Load(lang);

            var allowMultiInstance = ParseBool(ConfigManager.Get(cfg, MultiInstanceKey), fallback: false);
            if (!TryAcquireSingleInstance(allowMultiInstance))
            {
                try
                {
                    var prompt = new MultiInstanceWindow
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    };
                    prompt.ShowDialog();
                }
                catch
                {
                }

                Shutdown();
                return;
            }

            var theme = ConfigManager.Get(cfg, "theme");
            var opacityRaw = ConfigManager.Get(cfg, "opacity");
            var topmostRaw = ConfigManager.Get(cfg, "topmost");
            var targetOpacity = 1.0;
            if (!string.IsNullOrWhiteSpace(opacityRaw)
                && double.TryParse(opacityRaw.Trim(), out var parsedOpacity))
            {
                targetOpacity = Math.Max(0.1, Math.Min(1.0, parsedOpacity));
            }

            var targetTopmost = false;
            if (!string.IsNullOrWhiteSpace(topmostRaw)
                && bool.TryParse(topmostRaw.Trim(), out var parsedTopmost))
            {
                targetTopmost = parsedTopmost;
            }

            if (string.IsNullOrWhiteSpace(lang) || string.IsNullOrWhiteSpace(theme))
            {
                var startOnTheme = !string.IsNullOrWhiteSpace(lang) && string.IsNullOrWhiteSpace(theme);

                var setup = new SetupWindow(cfg, startOnTheme)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };

                var ok = setup.ShowDialog();
                if (ok != true)
                {
                    Shutdown();
                    return;
                }

                lang = ConfigManager.Get(cfg, "language");
                LocalizationManager.Load(lang);
            }

            var main = new MainWindow
            {
                TargetOpacity = targetOpacity,
            };
            main.Topmost = targetTopmost;
            MainWindow = main;

            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            main.Opacity = 0;

            RoutedEventHandler? onLoaded = null;
            onLoaded = (_, _) =>
            {
                main.Loaded -= onLoaded;

                var root = main.Content as UIElement;

                var scale = new ScaleTransform(0.985, 0.985);
                var translate = new TranslateTransform(0, 10);
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(translate);

                if (root != null)
                {
                    root.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    root.RenderTransform = group;
                }

                var sb = new Storyboard();

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = targetOpacity,
                    Duration = System.TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(fadeIn, main);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Window.OpacityProperty));

                var sx = new DoubleAnimation
                {
                    From = 0.985,
                    To = 1,
                    Duration = System.TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(sx, scale);
                Storyboard.SetTargetProperty(sx, new PropertyPath(ScaleTransform.ScaleXProperty));

                var sy = new DoubleAnimation
                {
                    From = 0.985,
                    To = 1,
                    Duration = System.TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(sy, scale);
                Storyboard.SetTargetProperty(sy, new PropertyPath(ScaleTransform.ScaleYProperty));

                var ty = new DoubleAnimation
                {
                    From = 10,
                    To = 0,
                    Duration = System.TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(ty, translate);
                Storyboard.SetTargetProperty(ty, new PropertyPath(TranslateTransform.YProperty));

                sb.Children.Add(fadeIn);
                sb.Children.Add(sx);
                sb.Children.Add(sy);
                sb.Children.Add(ty);

                sb.Completed += (_, _) =>
                {
                    main.BeginAnimation(Window.OpacityProperty, null);
                    main.Opacity = targetOpacity;

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;

                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    translate.Y = 0;
                };

                sb.Begin();
            };

            main.Loaded += onLoaded;

            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch
            {
            }

            base.OnExit(e);
        }

        private static bool TryAcquireSingleInstance(bool allowMultiInstance)
        {
            if (allowMultiInstance)
            {
                return true;
            }

            try
            {
                var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
                if (!createdNew)
                {
                    mutex.Dispose();
                    return false;
                }

                _singleInstanceMutex = mutex;
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static bool ParseBool(string? value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (bool.TryParse(value.Trim(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

}
