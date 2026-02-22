using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Executor.WaveUI.WaveViews
{
    public partial class ConsoleView : UserControl
    {
        private readonly ObservableCollection<LogEntry> _all = new();
        private readonly ObservableCollection<LogEntry> _filtered = new();
        private readonly DispatcherTimer _tailTimer;
        private readonly DispatcherTimer _fontSizePopupTimer;
        private ScrollViewer? _logScrollViewer;
        private bool _autoScrollToEnd = true;
        private bool _scrollToBottomVisible;
        private bool _suppressScrollChanged;
        private long _lastPosition;
        private string? _logPath;
        private string _levelFilter = "ALL";
        private string _searchFilter = string.Empty;

        private static readonly SolidColorBrush TimestampBrush = new((Color)ColorConverter.ConvertFromString("#5D6066"));
        private static readonly SolidColorBrush SourceBrush = new((Color)ColorConverter.ConvertFromString("#9AA0A6"));
        private static readonly SolidColorBrush MessageBrush = new((Color)ColorConverter.ConvertFromString("#EDEDED"));
        private static readonly SolidColorBrush ErrorBrush = new((Color)ColorConverter.ConvertFromString("#ef4444"));
        private static readonly SolidColorBrush WarningBrush = new((Color)ColorConverter.ConvertFromString("#facc15"));
        private static readonly SolidColorBrush DefaultLevelBrush = Brushes.White;

        static ConsoleView()
        {
            TimestampBrush.Freeze();
            SourceBrush.Freeze();
            MessageBrush.Freeze();
            ErrorBrush.Freeze();
            WarningBrush.Freeze();
        }

        public ConsoleView()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _tailTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _tailTimer.Tick += (_, _) => TailLogIfNeeded();

            _fontSizePopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _fontSizePopupTimer.Tick += (_, _) => HideFontSizePopup();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LocalizationManager.LanguageChanged += OnLanguageChanged;
            }
            catch
            {
            }

            ApplyLanguage();

            HookLogScrollViewer();

            ResolveLogPath();
            LoadInitial();
            _tailTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LocalizationManager.LanguageChanged -= OnLanguageChanged;
                _tailTimer.Stop();

                if (_logScrollViewer != null)
                {
                    _logScrollViewer.ScrollChanged -= LogScrollViewer_OnScrollChanged;
                    _logScrollViewer = null;
                }
            }
            catch
            {
            }
        }

        private void HookLogScrollViewer()
        {
            try
            {
                if (_logScrollViewer != null)
                {
                    return;
                }

                LogBox.ApplyTemplate();
                LogBox.UpdateLayout();

                _logScrollViewer = FindDescendant<ScrollViewer>(LogBox);
                if (_logScrollViewer != null)
                {
                    _logScrollViewer.ScrollChanged += LogScrollViewer_OnScrollChanged;
                }
            }
            catch
            {
            }
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            try
            {
                var count = VisualTreeHelper.GetChildrenCount(root);
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(root, i);
                    if (child is T t)
                    {
                        return t;
                    }

                    var found = FindDescendant<T>(child);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void LogScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                if (_suppressScrollChanged)
                {
                    return;
                }

                if (_logScrollViewer == null)
                {
                    return;
                }

                var threshold = 2.0;
                var atBottom = _logScrollViewer.VerticalOffset >= _logScrollViewer.ScrollableHeight - threshold;

                if (!atBottom)
                {
                    _autoScrollToEnd = false;
                    SetScrollToBottomVisible(true);
                }
                else
                {
                    _autoScrollToEnd = true;
                    SetScrollToBottomVisible(false);
                }
            }
            catch
            {
            }
        }

        private void SetScrollToBottomVisible(bool visible)
        {
            try
            {
                if (_scrollToBottomVisible == visible)
                {
                    return;
                }

                _scrollToBottomVisible = visible;
                if (visible)
                {
                    ShowScrollToBottomButton();
                }
                else
                {
                    HideScrollToBottomButton();
                }
            }
            catch
            {
            }
        }

        private void ShowScrollToBottomButton()
        {
            try
            {
                if (ScrollToBottomHost.Visibility != Visibility.Visible)
                {
                    ScrollToBottomHost.Visibility = Visibility.Visible;
                }

                ScrollToBottomHost.IsHitTestVisible = true;
                ScrollToBottomHost.BeginAnimation(OpacityProperty, null);
                var fade = new DoubleAnimation
                {
                    From = ScrollToBottomHost.Opacity,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(140),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                ScrollToBottomHost.BeginAnimation(OpacityProperty, fade);
            }
            catch
            {
            }
        }

        private void HideScrollToBottomButton()
        {
            try
            {
                if (ScrollToBottomHost.Visibility != Visibility.Visible)
                {
                    return;
                }

                ScrollToBottomHost.IsHitTestVisible = false;
                ScrollToBottomHost.BeginAnimation(OpacityProperty, null);
                var fade = new DoubleAnimation
                {
                    From = ScrollToBottomHost.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(160),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                fade.Completed += (_, _) =>
                {
                    try
                    {
                        ScrollToBottomHost.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                    }
                };
                ScrollToBottomHost.BeginAnimation(OpacityProperty, fade);
            }
            catch
            {
            }
        }

        private void ScrollToBottomButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoScrollToEnd = true;
                HookLogScrollViewer();
                SmoothScrollToBottom();
                SetScrollToBottomVisible(false);
            }
            catch
            {
            }
        }

        private void SmoothScrollToBottom()
        {
            try
            {
                if (_logScrollViewer == null)
                {
                    LogBox.ScrollToEnd();
                    return;
                }

                var target = _logScrollViewer.ScrollableHeight;
                if (target <= 0)
                {
                    LogBox.ScrollToEnd();
                    return;
                }

                var current = _logScrollViewer.VerticalOffset;
                if (Math.Abs(target - current) < 0.5)
                {
                    LogBox.ScrollToEnd();
                    return;
                }

                var anim = new DoubleAnimation
                {
                    From = current,
                    To = target,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop,
                };

                anim.Completed += (_, _) =>
                {
                    try
                    {
                        _logScrollViewer?.ScrollToVerticalOffset(target);
                        LogBox.ScrollToEnd();
                    }
                    catch
                    {
                    }
                };

                _logScrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
                _logScrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, anim);
            }
            catch
            {
                try
                {
                    LogBox.ScrollToEnd();
                }
                catch
                {
                }
            }
        }

        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedVerticalOffset",
                typeof(double),
                typeof(ConsoleView),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is ScrollViewer sv)
                {
                    sv.ScrollToVerticalOffset((double)e.NewValue);
                }
            }
            catch
            {
            }
        }

        private void SetStatusMessage(string message)
        {
            try
            {
                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(0),
                };

                var p = new Paragraph
                {
                    Margin = new Thickness(0),
                };

                p.Inlines.Add(new Run(message ?? string.Empty)
                {
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9AA0A6")),
                });
                doc.Blocks.Add(p);

                LogBox.Document = doc;
            }
            catch
            {
            }
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(ApplyLanguage));
        }

        private void ApplyLanguage()
        {
            if (!IsLoaded)
            {
                return;
            }

            DetailTitle.Text = LocalizationManager.T("WaveUI.Console.Title");
            SearchPlaceholder.Text = LocalizationManager.T("WaveUI.Console.SearchPlaceholder");

            ScrollToBottomText.Text = LocalizationManager.T("WaveUI.Console.ScrollToBottom");

            FilterAllText.Text = LocalizationManager.T("WaveUI.Console.Filter.All");
            FilterInfoText.Text = LocalizationManager.T("WaveUI.Console.Filter.Info");
            FilterWaringText.Text = LocalizationManager.T("WaveUI.Console.Filter.Waring");
            FilterErrorText.Text = LocalizationManager.T("WaveUI.Console.Filter.Error");
        }

        private void ResolveLogPath()
        {
            try
            {
                _logPath = Logger.LogFilePath;
                if (!string.IsNullOrWhiteSpace(_logPath) && File.Exists(_logPath))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                Logger.Initialize();
            }
            catch
            {
            }

            try
            {
                _logPath = Logger.LogFilePath;
                if (!string.IsNullOrWhiteSpace(_logPath) && File.Exists(_logPath))
                {
                    return;
                }
            }
            catch
            {
            }

            _logPath = null;
        }

        private void LoadInitial()
        {
            _all.Clear();
            _filtered.Clear();
            _lastPosition = 0;

            if (string.IsNullOrWhiteSpace(_logPath) || !File.Exists(_logPath))
            {
                SetStatusMessage(LocalizationManager.T("WaveUI.Console.Status.NoLog"));
                return;
            }

            try
            {
                var lines = ReadTailLines(_logPath, 600);
                foreach (var line in lines)
                {
                    AppendLine(line);
                }

                using (var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    _lastPosition = fs.Length;
                }

                ApplyFilters(scrollToEnd: true);
            }
            catch
            {
            }
        }

        private static List<string> ReadTailLines(string path, int maxLines)
        {
            try
            {
                if (maxLines <= 0)
                {
                    return new List<string>();
                }

                var list = new LinkedList<string>();
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    list.AddLast(line);
                    if (list.Count > maxLines)
                    {
                        list.RemoveFirst();
                    }
                }

                return list.ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void TailLogIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_logPath) || !File.Exists(_logPath))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(_logPath);
                if (fileInfo.Length < _lastPosition)
                {
                    _lastPosition = 0;
                }

                if (fileInfo.Length == _lastPosition)
                {
                    return;
                }

                using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length == _lastPosition)
                {
                    return;
                }

                fs.Seek(_lastPosition, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);

                var had = false;
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    had = true;
                    AppendLine(line);
                }

                _lastPosition = fs.Position;

                if (had)
                {
                    ApplyFilters(scrollToEnd: _autoScrollToEnd);
                }
            }
            catch
            {
            }
        }

        private void AppendLine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var entry = ParseLine(raw);
            if (entry == null)
            {
                return;
            }

            _all.Add(entry);

            if (_all.Count > 2000)
            {
                _all.RemoveAt(0);
            }
        }

        private static readonly Regex LogRegex = new Regex(
            "^\\[(?<ts>[^\\]]+)\\]\\s+\\[(?<lvl>[^\\]]+)\\]\\[(?<src>[^\\]]+)\\]\\s(?<msg>.*)$",
            RegexOptions.Compiled);

        private static LogEntry? ParseLine(string raw)
        {
            try
            {
                var m = LogRegex.Match(raw);
                if (!m.Success)
                {
                    return new LogEntry
                    {
                        Raw = raw,
                        Timestamp = string.Empty,
                        Level = string.Empty,
                        Source = string.Empty,
                        Message = raw,
                        LevelColor = Brushes.White,
                    };
                }

                var ts = m.Groups["ts"].Value;
                var lvl = m.Groups["lvl"].Value;
                var src = m.Groups["src"].Value;
                var msg = m.Groups["msg"].Value;

                return new LogEntry
                {
                    Raw = raw,
                    Timestamp = ts,
                    Level = lvl,
                    Source = src,
                    Message = msg,
                    LevelColor = ResolveLevelBrush(lvl),
                };
            }
            catch
            {
                return null;
            }
        }

        private static Brush ResolveLevelBrush(string level)
        {
            var l = (level ?? string.Empty).Trim();
            if (string.Equals(l, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
            }

            if (string.Equals(l, "WARING", StringComparison.OrdinalIgnoreCase)
                || string.Equals(l, "WARNING", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#facc15"));
            }

            return Brushes.White;
        }

        private void ApplyFilters(bool scrollToEnd)
        {
            IEnumerable<LogEntry> query = _all;

            if (!string.Equals(_levelFilter, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.Level, _levelFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                query = query.Where(x => (x.Raw ?? string.Empty).IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = query.ToList();

            _filtered.Clear();
            foreach (var e in list)
            {
                _filtered.Add(e);
            }

            RenderToDocument(_filtered, scrollToEnd);
        }

        private void RenderToDocument(IEnumerable<LogEntry> entries, bool scrollToEnd)
        {
            try
            {
                HookLogScrollViewer();

                double? restoreOffset = null;
                if (!scrollToEnd && _logScrollViewer != null)
                {
                    restoreOffset = _logScrollViewer.VerticalOffset;
                }

                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(0),
                };

                foreach (var e in entries)
                {
                    var p = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, 6),
                        LineHeight = 0.1,
                    };

                    if (!string.IsNullOrWhiteSpace(e.Timestamp))
                    {
                        p.Inlines.Add(new Run("[") { Foreground = TimestampBrush });
                        p.Inlines.Add(new Run(e.Timestamp) { Foreground = TimestampBrush });
                        p.Inlines.Add(new Run("] ") { Foreground = TimestampBrush });
                    }

                    if (!string.IsNullOrWhiteSpace(e.Level))
                    {
                        p.Inlines.Add(new Run("[") { Foreground = e.LevelColor });
                        p.Inlines.Add(new Run(e.Level) { Foreground = e.LevelColor });
                        p.Inlines.Add(new Run("]") { Foreground = e.LevelColor });
                    }

                    if (!string.IsNullOrWhiteSpace(e.Source))
                    {
                        p.Inlines.Add(new Run("[") { Foreground = SourceBrush });
                        p.Inlines.Add(new Run(e.Source) { Foreground = SourceBrush });
                        p.Inlines.Add(new Run("] ") { Foreground = SourceBrush });
                    }

                    p.Inlines.Add(new Run(e.Message ?? string.Empty) { Foreground = MessageBrush });

                    doc.Blocks.Add(p);
                }

                _suppressScrollChanged = true;
                LogBox.Document = doc;
                LogBox.UpdateLayout();

                HookLogScrollViewer();

                if (scrollToEnd)
                {
                    LogBox.ScrollToEnd();
                }
                else if (restoreOffset.HasValue && _logScrollViewer != null)
                {
                    var offset = restoreOffset.Value;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _suppressScrollChanged = true;
                            _logScrollViewer?.ScrollToVerticalOffset(offset);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            _suppressScrollChanged = false;
                        }
                    }), DispatcherPriority.Loaded);
                }

                _suppressScrollChanged = false;
            }
            catch
            {
                _suppressScrollChanged = false;
            }
        }

        private void SetLevelFilter(string level)
        {
            _levelFilter = level;
            ApplyFilters(scrollToEnd: false);
        }

        private void FilterAll_OnChecked(object sender, RoutedEventArgs e) => SetLevelFilter("ALL");
        private void FilterInfo_OnChecked(object sender, RoutedEventArgs e) => SetLevelFilter("INFO");
        private void FilterWaring_OnChecked(object sender, RoutedEventArgs e) => SetLevelFilter("WARING");
        private void FilterError_OnChecked(object sender, RoutedEventArgs e) => SetLevelFilter("ERROR");

        private void FontSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                var v = (int)Math.Round(FontSizeSlider.Value);
                FontSizePopupText.Text = v.ToString();

                UpdateFontSizePopupOffset();

                _fontSizePopupTimer.Stop();
                ShowFontSizePopup();
                _fontSizePopupTimer.Start();
            }
            catch
            {
            }
        }

        private void UpdateFontSizePopupOffset()
        {
            try
            {
                FontSizeSlider.ApplyTemplate();

                if (FontSizeSlider.Template.FindName("PART_Track", FontSizeSlider) is not Track track)
                {
                    return;
                }

                var thumb = track.Thumb;
                if (thumb == null)
                {
                    return;
                }

                thumb.ApplyTemplate();
                thumb.UpdateLayout();
                FontSizeSlider.UpdateLayout();

                var pt = thumb.TranslatePoint(new Point(thumb.ActualWidth / 2.0, 0), FontSizeSlider);
                var ptTopLeft = thumb.TranslatePoint(new Point(0, 0), FontSizeSlider);

                var popupW = FontSizePopupBorder.ActualWidth;
                if (double.IsNaN(popupW) || popupW <= 0)
                {
                    popupW = 24;
                }

                var popupHalf = popupW / 2.0;
                FontSizePopup.HorizontalOffset = pt.X - popupHalf;

                var gap = 6.0;
                FontSizePopup.VerticalOffset = ptTopLeft.Y - FontSizePopupBorder.ActualHeight - gap;
            }
            catch
            {
            }
        }

        private void ShowFontSizePopup()
        {
            try
            {
                FontSizePopup.IsOpen = true;

                try
                {
                    FontSizePopupBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    FontSizePopupBorder.Arrange(new Rect(FontSizePopupBorder.DesiredSize));
                    FontSizePopupBorder.UpdateLayout();
                }
                catch
                {
                }

                Dispatcher.BeginInvoke(new Action(UpdateFontSizePopupOffset), DispatcherPriority.Loaded);

                FontSizePopupBorder.BeginAnimation(OpacityProperty, null);
                FontSizePopupTranslate.BeginAnimation(TranslateTransform.YProperty, null);

                var fade = new DoubleAnimation
                {
                    From = FontSizePopupBorder.Opacity,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(120),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                FontSizePopupBorder.BeginAnimation(OpacityProperty, fade);

                var move = new DoubleAnimation
                {
                    From = FontSizePopupTranslate.Y,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(120),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                FontSizePopupTranslate.BeginAnimation(TranslateTransform.YProperty, move);
            }
            catch
            {
            }
        }

        private void HideFontSizePopup()
        {
            try
            {
                _fontSizePopupTimer.Stop();

                var fade = new DoubleAnimation
                {
                    From = FontSizePopupBorder.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(140),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                fade.Completed += (_, _) =>
                {
                    try
                    {
                        FontSizePopup.IsOpen = false;
                    }
                    catch
                    {
                    }
                };
                FontSizePopupBorder.BeginAnimation(OpacityProperty, fade);

                var move = new DoubleAnimation
                {
                    From = FontSizePopupTranslate.Y,
                    To = 6,
                    Duration = TimeSpan.FromMilliseconds(140),
                    FillBehavior = FillBehavior.HoldEnd,
                };
                FontSizePopupTranslate.BeginAnimation(TranslateTransform.YProperty, move);
            }
            catch
            {
            }
        }

        private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = SearchBox.Text ?? string.Empty;
            ApplyFilters(scrollToEnd: false);
        }

        private sealed class LogEntry
        {
            public string? Raw { get; set; }
            public string Timestamp { get; set; } = string.Empty;
            public string Level { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public Brush LevelColor { get; set; } = Brushes.White;
        }
    }
}
