using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Executor
{
    public partial class SetupWindow : Window
    {
        private readonly Dictionary<string, string> _config;
        private readonly List<ThemeItem> _allThemes;
        private List<ThemeItem> _filteredThemes;
        private int _stepIndex;

        public SetupWindow(Dictionary<string, string> config)
            : this(config, startOnTheme: false)
        {
        }

        public SetupWindow(Dictionary<string, string> config, bool startOnTheme)
        {
            InitializeComponent();
            _config = config;

            // 主題配色方案 - 擴展到10個主題
            var palette = new[]
            {
                (Color)ColorConverter.ConvertFromString("#2D7DFF"), // Wave - 藍色
                (Color)ColorConverter.ConvertFromString("#2AA6FF"), // Synapse X - 淺藍
                (Color)ColorConverter.ConvertFromString("#6A5CFF"), // KRNL - 紫色
                (Color)ColorConverter.ConvertFromString("#19C3B3"), // Fluxus - 青色
                (Color)ColorConverter.ConvertFromString("#FF6B6B"), // Arceus X - 紅色
                (Color)ColorConverter.ConvertFromString("#FFB84D"), // Delta - 橙色
                (Color)ColorConverter.ConvertFromString("#4ECDC4"), // Codex - 薄荷綠
                (Color)ColorConverter.ConvertFromString("#A855F7"), // Evon - 紫紅
                (Color)ColorConverter.ConvertFromString("#10B981"), // Electron - 綠色
                (Color)ColorConverter.ConvertFromString("#F59E0B"), // Hydrogen - 金色
            };

            // 主題名稱列表 - 擴展到10個
            var names = new List<string>
            {
                "Wave",
                "Synapse X",
                "KRNL",
                "Fluxus",
                "Arceus X",
                "Delta",
                "Codex",
                "Evon",
                "Electron",
                "Hydrogen",
            };

            _allThemes = names
                .Select((n, i) => new ThemeItem(n, palette[i % palette.Length]))
                .ToList();

            _filteredThemes = _allThemes.ToList();

            ThemeSearch.Text = "";
            ThemeList.ItemsSource = _filteredThemes;
            ThemeList.SelectedIndex = 0;

            var existingTheme = ConfigManager.Get(_config, "theme");
            if (!string.IsNullOrWhiteSpace(existingTheme))
            {
                var idx = _filteredThemes.FindIndex(t => string.Equals(t.Name, existingTheme, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    ThemeList.SelectedIndex = idx;
                }
            }

            var existingLang = ConfigManager.Get(_config, "language");
            if (string.Equals(existingLang, "en", StringComparison.OrdinalIgnoreCase))
            {
                LangEn.IsChecked = true;
            }
            else
            {
                LangZh.IsChecked = true;
            }

            LocalizationManager.Load(existingLang);
            ApplyLanguage();

            _stepIndex = startOnTheme ? 1 : 0;
            UpdateStepUI(animated: false);
        }

        private void LanguageRadio_OnChecked(object sender, RoutedEventArgs e)
        {
            var lang = SelectedLanguageCode;
            LocalizationManager.Load(lang);
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            TitleText.Text = LocalizationManager.T("setup.title");
            LanguageHeaderText.Text = LocalizationManager.T("setup.choose_language");
            ThemeHeaderText.Text = LocalizationManager.T("setup.choose_theme");
            ThemeSearchHint.Text = LocalizationManager.T("setup.search_hint");
            LangZh.Content = LocalizationManager.T("setup.lang_zh");
            LangEn.Content = LocalizationManager.T("setup.lang_en");
            BackButton.Content = LocalizationManager.T("setup.back");
            NextButton.Content = LocalizationManager.T("setup.next");
            FinishButton.Content = LocalizationManager.T("setup.finish");
            Title = LocalizationManager.T("setup.window_title");
        }

        public string? SelectedLanguageCode
        {
            get
            {
                if (LangEn.IsChecked == true)
                {
                    return "en";
                }

                if (LangZh.IsChecked == true)
                {
                    return "zh";
                }

                return null;
            }
        }

        public string? SelectedTheme
        {
            get
            {
                return (ThemeList.SelectedItem as ThemeItem)?.Name;
            }
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

        private void BackButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_stepIndex <= 0)
            {
                return;
            }

            _stepIndex--;
            UpdateStepUI(animated: true);
        }

        private void NextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_stepIndex == 0)
            {
                var lang = SelectedLanguageCode;
                if (string.IsNullOrWhiteSpace(lang))
                {
                    ShowSnackbar(LocalizationManager.T("setup.msg_choose_language"));
                    return;
                }

                ConfigManager.Set(_config, "language", lang);
                _stepIndex = 1;
                UpdateStepUI(animated: true);
                return;
            }
        }

        private void FinishButton_OnClick(object sender, RoutedEventArgs e)
        {
            var theme = SelectedTheme;
            if (string.IsNullOrWhiteSpace(theme))
            {
                ShowSnackbar(LocalizationManager.T("setup.msg_select_theme"));
                return;
            }

            ConfigManager.Set(_config, "theme", theme);

            try
            {
                ConfigManager.WriteConfig(_config);

                BackButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                FinishButton.IsEnabled = false;

                BeginAnimation(OpacityProperty, null);

                var fadeOut = new DoubleAnimation
                {
                    From = Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };

                fadeOut.Completed += (_, _) =>
                {
                    DialogResult = true;
                    Close();
                };

                BeginAnimation(OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                ShowSnackbar(LocalizationManager.F("setup.msg_write_failed", ex.Message));
            }
        }

        private void ThemeSearch_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var q = ThemeSearch.Text?.Trim() ?? "";
            if (q.Length == 0)
            {
                _filteredThemes = _allThemes.ToList();
            }
            else
            {
                _filteredThemes = _allThemes.Where(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ThemeList.ItemsSource = _filteredThemes;
            if (_filteredThemes.Count > 0)
            {
                ThemeList.SelectedIndex = 0;
            }
        }

        private void ThemeList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_stepIndex == 1)
            {
                // Keep finish enabled only when a theme is selected
                FinishButton.IsEnabled = SelectedTheme != null;
            }
        }

        private void ThemeList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            var sv = FindScrollViewer(listBox);
            if (sv == null)
            {
                return;
            }

            const double pixelsPerEvent = 24;
            var direction = e.Delta > 0 ? -1 : 1;
            var target = sv.VerticalOffset + (direction * pixelsPerEvent);
            if (target < 0)
            {
                target = 0;
            }
            else if (target > sv.ScrollableHeight)
            {
                target = sv.ScrollableHeight;
            }

            sv.ScrollToVerticalOffset(target);
            e.Handled = true;
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv)
            {
                return sv;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void UpdateStepUI(bool animated)
        {
            var isLang = _stepIndex == 0;

            BackButton.Visibility = isLang ? Visibility.Collapsed : Visibility.Visible;
            BackButton.IsEnabled = !isLang;
            NextButton.Visibility = isLang ? Visibility.Visible : Visibility.Collapsed;
            FinishButton.Visibility = isLang ? Visibility.Collapsed : Visibility.Visible;

            if (isLang)
            {
                StepTheme.Visibility = Visibility.Collapsed;
                StepLanguage.Visibility = Visibility.Visible;

                StepLanguage.Opacity = 1;
                StepLanguageTransform.Y = 0;

                if (animated)
                {
                    AnimateIn(StepLanguage, StepLanguageTransform);
                }

                return;
            }

            StepLanguage.Visibility = Visibility.Collapsed;
            StepTheme.Visibility = Visibility.Visible;
            StepTheme.Opacity = 1;
            StepThemeTransform.Y = 0;

            if (animated)
            {
                AnimateIn(StepTheme, StepThemeTransform);
            }

            FinishButton.IsEnabled = SelectedTheme != null;
        }

        private static void AnimateIn(UIElement element, System.Windows.Media.TranslateTransform transform)
        {
            transform.Y = 10;
            element.Opacity = 0;

            var sb = new Storyboard();

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

            var slide = new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(slide, transform);
            Storyboard.SetTargetProperty(slide, new PropertyPath("Y"));

            sb.Children.Add(fade);
            sb.Children.Add(slide);
            sb.Begin();
        }

        private void ShowSnackbar(string message)
        {
            SnackbarText.Text = message;

            var sb = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fadeIn, Snackbar);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(2000),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fadeOut, Snackbar);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            sb.Begin();
        }
    }

    public sealed class ThemeItem
    {
        public ThemeItem(string name, Color accentColor)
        {
            Name = name;
            LogoText = CreateLogoText(name);
            AccentBrush = new SolidColorBrush(accentColor);
            AccentBrush.Freeze();

            ImageUri = TryLoadImage(name);
        }

        public string Name { get; }

        public string LogoText { get; }

        public Brush AccentBrush { get; }

        public ImageSource? ImageUri { get; }

        private static ImageSource? TryLoadImage(string name)
        {
            var safeName = ToSafeFileName(name);
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var themeDir = Path.Combine(baseDir, "Assets", "WaveUI");

            try
            {
                Directory.CreateDirectory(themeDir);
            }
            catch
            {
                // ignore
            }

            var candidates = new List<string>
            {
                safeName + ".png",
                safeName + ".jpg",
                safeName + ".jpeg",
            };

            // 處理 Synapse X 的拼寫變體
            if (safeName.Contains("synapse", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(safeName.Replace("synapse", "synpase", StringComparison.OrdinalIgnoreCase) + ".png");
                candidates.Add(safeName.Replace("synapse", "synpase", StringComparison.OrdinalIgnoreCase) + ".jpg");
            }
            else if (safeName.Contains("synpase", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(safeName.Replace("synpase", "synapse", StringComparison.OrdinalIgnoreCase) + ".png");
                candidates.Add(safeName.Replace("synpase", "synapse", StringComparison.OrdinalIgnoreCase) + ".jpg");
            }

            string? fullPath = null;
            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var p = Path.Combine(themeDir, candidate);
                if (File.Exists(p))
                {
                    fullPath = p;
                    break;
                }
            }

            if (fullPath == null)
            {
                var legacyThemeDir = Path.Combine(baseDir, "Assets", "themes");
                foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var p = Path.Combine(legacyThemeDir, candidate);
                    if (File.Exists(p))
                    {
                        fullPath = p;
                        break;
                    }
                }
            }

            if (fullPath == null)
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string ToSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unknown";
            }

            var chars = name
                .Trim()
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();

            var s = new string(chars);
            while (s.Contains("__", StringComparison.Ordinal))
            {
                s = s.Replace("__", "_", StringComparison.Ordinal);
            }

            return s.Trim('_');
        }

        private static string CreateLogoText(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name
                .Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            if (parts.Length == 0)
            {
                return "?";
            }

            if (parts.Length == 1)
            {
                var p = parts[0];
                return p.Length >= 2
                    ? (char.ToUpperInvariant(p[0]).ToString() + char.ToUpperInvariant(p[1]))
                    : char.ToUpperInvariant(p[0]).ToString();
            }

            var first = parts[0];
            var last = parts[^1];

            var c1 = first.Length > 0 ? char.ToUpperInvariant(first[0]) : '?';
            var c2 = last.Length > 0 ? char.ToUpperInvariant(last[0]) : '?';
            return c1.ToString() + c2;
        }
    }
}