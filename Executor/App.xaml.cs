using System.Configuration;
using System.Data;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var cfg = ConfigManager.ReadConfig();
            var lang = ConfigManager.Get(cfg, "language");
            var theme = ConfigManager.Get(cfg, "theme");

            LocalizationManager.Load(lang);

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

            var main = new MainWindow();
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
                    To = 1,
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
                    main.Opacity = 1;

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
    }

}
