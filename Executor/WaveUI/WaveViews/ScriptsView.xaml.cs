using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Executor.WaveUI.WaveViews
{
    public partial class ScriptsView : UserControl
    {
        private const int MaxPerPage = 20;
        private static readonly HttpClient Http = new();

        private readonly Action<string> _toast;
        private readonly Action<string, string> _openInEditor;
        private readonly Action<string, string>? _executeInEditor;

        private readonly DispatcherTimer _searchDebounceTimer;

        private bool _loaded;
        private bool _isLoading;
        private int _page = 1;
        private int _totalPages = 1;

        private string _searchQuery = string.Empty;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (value == _searchQuery)
                {
                    return;
                }

                _searchQuery = value;
                OnPropertyChanged();

                if (!_loaded)
                {
                    return;
                }

                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            }
        }

        public ObservableCollection<ScriptCard> Scripts { get; } = new();

        public ScriptsView(Action<string> toast, Action<string, string> openInEditor, Action<string, string>? executeInEditor = null)
        {
            InitializeComponent();

            _toast = toast;
            _openInEditor = openInEditor;
            _executeInEditor = executeInEditor;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5),
            };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                _ = RefreshAsync(resetPage: true);
            };

            DataContext = this;

            Loaded += ScriptsView_OnLoaded;
        }

        private void ActionButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Intentionally empty: we no longer suppress Preview events here because it can prevent Button.Click.
        }

        private void CopyScript_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ScriptCard card)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(card.Script))
            {
                _toast("Script is empty.");
                return;
            }

            try
            {
                Clipboard.SetText(card.Script);
                _toast("Copied.");
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private void ExecuteScript_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ScriptCard card)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(card.Script))
            {
                _toast("Script is empty.");
                return;
            }

            try
            {
                if (!TryExecuteWithVelocity(card.Script, out var error))
                {
                    _toast(error ?? "Execute failed.");
                    return;
                }

                _toast("Executed.");
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private static bool TryExecuteWithVelocity(string script, out string? error)
        {
            error = null;

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? apiType = null;
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (string.Equals(t.Name, "SpashAPIVelocity", StringComparison.Ordinal))
                            {
                                apiType = t;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if (apiType == null)
                    {
                        continue;
                    }

                    var method = apiType.GetMethod("ExecuteScript", new[] { typeof(string) });
                    if (method == null)
                    {
                        error = "SpashAPIVelocity.ExecuteScript(string) not found.";
                        return false;
                    }

                    method.Invoke(null, new object?[] { script });
                    return true;
                }

                error = "SpashAPIVelocity type not found in loaded assemblies.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void ScriptsView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _ = RefreshAsync(resetPage: true);
        }

        private async Task RefreshAsync(bool resetPage)
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            try
            {
                if (resetPage)
                {
                    _page = 1;
                }

                var q = (SearchQuery ?? string.Empty).Trim();

                var url = string.IsNullOrWhiteSpace(q)
                    ? BuildFetchUrl(_page, MaxPerPage)
                    : BuildSearchUrl(q, _page, MaxPerPage);

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Executor/1.0");

                using var res = await Http.SendAsync(req);
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("result", out var result))
                {
                    _toast("Invalid response.");
                    return;
                }

                if (result.TryGetProperty("totalPages", out var totalPagesEl)
                    && totalPagesEl.ValueKind == JsonValueKind.Number
                    && totalPagesEl.TryGetInt32(out var tp))
                {
                    _totalPages = Math.Max(1, tp);
                }
                else
                {
                    _totalPages = Math.Max(1, _page);
                }

                if (!result.TryGetProperty("scripts", out var scriptsEl) || scriptsEl.ValueKind != JsonValueKind.Array)
                {
                    Scripts.Clear();
                    return;
                }

                Scripts.Clear();
                foreach (var s in scriptsEl.EnumerateArray())
                {
                    var title = SafeGetString(s, "title") ?? "Untitled";
                    var script = SafeGetString(s, "script") ?? string.Empty;

                    var gameName = string.Empty;
                    var imageUrl = string.Empty;
                    if (s.TryGetProperty("game", out var game) && game.ValueKind == JsonValueKind.Object)
                    {
                        gameName = SafeGetString(game, "name") ?? string.Empty;
                        imageUrl = SafeGetString(game, "imageUrl") ?? string.Empty;
                    }

                    var image = SafeGetString(s, "image");
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        imageUrl = image;
                    }

                    imageUrl = NormalizeImageUrl(imageUrl);

                    Scripts.Add(new ScriptCard
                    {
                        Title = title,
                        GameName = string.IsNullOrWhiteSpace(gameName) ? "Universal" : gameName,
                        ImageUrl = imageUrl,
                        Script = script,
                        HasImage = !string.IsNullOrWhiteSpace(imageUrl),
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                _toast($"Network error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _toast($"Parse error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private static Uri BuildFetchUrl(int page, int max)
        {
            var sb = new StringBuilder();
            sb.Append("https://scriptblox.com/api/script/fetch?");
            sb.Append("page=").Append(Math.Max(1, page));
            sb.Append("&max=").Append(Math.Clamp(max, 1, MaxPerPage));
            return new Uri(sb.ToString());
        }

        private static Uri BuildSearchUrl(string query, int page, int max)
        {
            var sb = new StringBuilder();
            sb.Append("https://scriptblox.com/api/script/search?");
            sb.Append("q=").Append(Uri.EscapeDataString(query));
            sb.Append("&page=").Append(Math.Max(1, page));
            sb.Append("&max=").Append(Math.Clamp(max, 1, MaxPerPage));
            return new Uri(sb.ToString());
        }

        private static string NormalizeImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var trimmed = url.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                return "https:" + trimmed;
            }

            if (trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                return "https://scriptblox.com" + trimmed;
            }

            return trimmed;
        }

        private static string? SafeGetString(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var el))
            {
                return null;
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }

            return null;
        }

        private void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _ = RefreshAsync(resetPage: true);
        }

        private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            _searchDebounceTimer.Stop();
            _ = RefreshAsync(resetPage: true);
        }

        private void ScriptImage_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ScriptCard card)
            {
                return;
            }

            card.HasImage = false;
        }

        private void MissingImage_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            el.Visibility = Visibility.Collapsed;

            if (el.Parent is not Panel panel)
            {
                return;
            }

            foreach (var child in panel.Children)
            {
                if (child is not FrameworkElement fe)
                {
                    continue;
                }

                if (fe.Name is "MissingFallbackIcon" or "MissingFallbackText")
                {
                    fe.Visibility = Visibility.Visible;
                }
            }
        }

        private void PrevPage_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            if (_page <= 1)
            {
                return;
            }

            _page = Math.Max(1, _page - 1);
            _ = RefreshAsync(resetPage: false);
        }

        private void NextPage_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            if (_page >= _totalPages)
            {
                return;
            }

            _page = Math.Min(_totalPages, _page + 1);
            _ = RefreshAsync(resetPage: false);
        }

        private void ScriptCard_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If the click originated from a Button inside the card (Copy/Execute), ignore it.
            if (e.OriginalSource is DependencyObject source)
            {
                var current = source;
                while (current != null)
                {
                    if (current is Button)
                    {
                        return;
                    }
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
            }

            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ScriptCard card)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(card.Script))
            {
                _toast("Script is empty.");
                return;
            }

            // Click is enabled, but intentionally no action for now.
        }

        public sealed class ScriptCard : INotifyPropertyChanged
        {
            public string Title { get; set; } = string.Empty;
            public string GameName { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;
            public string Script { get; set; } = string.Empty;

            private bool _hasImage;

            public bool HasImage
            {
                get => _hasImage;
                set
                {
                    if (value == _hasImage)
                    {
                        return;
                    }

                    _hasImage = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasImage)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
