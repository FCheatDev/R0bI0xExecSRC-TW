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
using Executor;

namespace Executor.WaveUI.WaveViews
{
    public partial class ScriptsView : UserControl, INotifyPropertyChanged
    {
        private const int MaxPerPage = 20;
        private static readonly HttpClient Http = new();

        private readonly Action<string> _toast;
        private readonly Action<string, string> _openInEditor;
        private readonly Action<string, string>? _executeInEditor;

        private bool _loaded;
        private bool _isLoading;
        private int _page = 1;
        private int _totalPages = 1;
        private readonly DispatcherTimer _scriptsSearchTimer;
        private string _scriptsSearchText = string.Empty;

        private string _scriptsRightTitle = "SCRIPTS";
        private string _scriptsRightSubtitle = "";
        private string _copyButtonLabel = "Copy";
        private string _executeButtonLabel = "Execute";
        private string _scriptStatusKeyLabel = "Key = ";
        private string _scriptStatusModeLabel = "Mode = ";
        private string _scriptStatusVerifyLabel = "Verify = ";

        private string _keyRequiredText = "Required";
        private string _keyNotRequiredText = "Not Required";
        private string _modeFreeText = "Free";
        private string _modePaidText = "Paid";
        private string _verifiedText = "Verified";
        private string _unverifiedText = "Unverified";
        private string _statusUnknownText = "-";

        private string _selectedScriptKeyStatus = "-";
        private string _selectedScriptModeStatus = "-";
        private string _selectedScriptVerifyStatus = "-";

        private ScriptCard? _selectedScript;

        public string ScriptsRightTitle
        {
            get => _scriptsRightTitle;
            private set
            {
                if (value == _scriptsRightTitle)
                {
                    return;
                }
                _scriptsRightTitle = value;
                OnPropertyChanged();
            }
        }

        public string ScriptsRightSubtitle
        {
            get => _scriptsRightSubtitle;
            private set
            {
                if (value == _scriptsRightSubtitle)
                {
                    return;
                }
                _scriptsRightSubtitle = value;
                OnPropertyChanged();
            }
        }

        public string CopyButtonLabel
        {
            get => _copyButtonLabel;
            private set
            {
                if (value == _copyButtonLabel)
                {
                    return;
                }
                _copyButtonLabel = value;
                OnPropertyChanged();
            }
        }

        public string ExecuteButtonLabel
        {
            get => _executeButtonLabel;
            private set
            {
                if (value == _executeButtonLabel)
                {
                    return;
                }
                _executeButtonLabel = value;
                OnPropertyChanged();
            }
        }

        public string ScriptStatusKeyLabel
        {
            get => _scriptStatusKeyLabel;
            private set
            {
                if (value == _scriptStatusKeyLabel)
                {
                    return;
                }

                _scriptStatusKeyLabel = value;
                OnPropertyChanged();
            }
        }

        public string ScriptStatusModeLabel
        {
            get => _scriptStatusModeLabel;
            private set
            {
                if (value == _scriptStatusModeLabel)
                {
                    return;
                }

                _scriptStatusModeLabel = value;
                OnPropertyChanged();
            }
        }

        public string ScriptStatusVerifyLabel
        {
            get => _scriptStatusVerifyLabel;
            private set
            {
                if (value == _scriptStatusVerifyLabel)
                {
                    return;
                }

                _scriptStatusVerifyLabel = value;
                OnPropertyChanged();
            }
        }

        public string SelectedScriptKeyStatus
        {
            get => _selectedScriptKeyStatus;
            private set
            {
                if (value == _selectedScriptKeyStatus)
                {
                    return;
                }

                _selectedScriptKeyStatus = value;
                OnPropertyChanged();
            }
        }

        public string SelectedScriptModeStatus
        {
            get => _selectedScriptModeStatus;
            private set
            {
                if (value == _selectedScriptModeStatus)
                {
                    return;
                }

                _selectedScriptModeStatus = value;
                OnPropertyChanged();
            }
        }

        public string SelectedScriptVerifyStatus
        {
            get => _selectedScriptVerifyStatus;
            private set
            {
                if (value == _selectedScriptVerifyStatus)
                {
                    return;
                }

                _selectedScriptVerifyStatus = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ScriptCard> Scripts { get; } = new();

        public ScriptsView(Action<string> toast, Action<string, string> openInEditor, Action<string, string>? executeInEditor = null)
        {
            InitializeComponent();

            _toast = toast;
            _openInEditor = openInEditor;
            _executeInEditor = executeInEditor;

            DataContext = this;

            _scriptsSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _scriptsSearchTimer.Tick += (_, _) =>
            {
                _scriptsSearchTimer.Stop();
                _ = RefreshAsync(resetPage: true);
            };

            Loaded += ScriptsView_OnLoaded;
            Unloaded += ScriptsView_OnUnloaded;
        }

        private void ScriptsView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            _scriptsSearchTimer.Stop();
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(ApplyLanguage));
        }

        private void ApplyLanguage()
        {
            ScriptsRightTitle = LocalizationManager.T("WaveUI.Scripts.Right.Title");
            ScriptsRightSubtitle = LocalizationManager.T("WaveUI.Scripts.Right.Subtitle");
            CopyButtonLabel = LocalizationManager.T("WaveUI.Scripts.Card.Copy");
            ExecuteButtonLabel = LocalizationManager.T("WaveUI.Scripts.Card.Execute");

            ScriptStatusKeyLabel = LocalizationManager.T("WaveUI.Scripts.Status.KeyLabel");
            ScriptStatusModeLabel = LocalizationManager.T("WaveUI.Scripts.Status.ModeLabel");
            ScriptStatusVerifyLabel = LocalizationManager.T("WaveUI.Scripts.Status.VerifyLabel");

            _keyRequiredText = LocalizationManager.T("WaveUI.Scripts.Status.KeyRequired");
            _keyNotRequiredText = LocalizationManager.T("WaveUI.Scripts.Status.KeyNotRequired");
            _modeFreeText = LocalizationManager.T("WaveUI.Scripts.Status.ModeFree");
            _modePaidText = LocalizationManager.T("WaveUI.Scripts.Status.ModePaid");
            _verifiedText = LocalizationManager.T("WaveUI.Scripts.Status.Verified");
            _unverifiedText = LocalizationManager.T("WaveUI.Scripts.Status.Unverified");
            _statusUnknownText = LocalizationManager.T("WaveUI.Scripts.Status.Unknown");

            if (ScriptsSearchPlaceholderText != null)
            {
                ScriptsSearchPlaceholderText.Text = LocalizationManager.T("WaveUI.Editor.Explorer.SearchHint");
            }

            UpdateSelectedScriptStatus(_selectedScript);
        }

        private void UpdateSelectedScriptStatus(ScriptCard? card)
        {
            _selectedScript = card;
            SelectedScriptKeyStatus = FormatKeyStatus(card?.KeyRequired);
            SelectedScriptModeStatus = FormatModeStatus(card?.ScriptType);
            SelectedScriptVerifyStatus = FormatVerifyStatus(card?.Verified);
        }

        private string FormatKeyStatus(bool? required)
        {
            if (required == null)
            {
                return _statusUnknownText;
            }

            return required.Value ? _keyRequiredText : _keyNotRequiredText;
        }

        private string FormatModeStatus(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return _statusUnknownText;
            }

            if (string.Equals(mode, "free", StringComparison.OrdinalIgnoreCase))
            {
                return _modeFreeText;
            }

            if (string.Equals(mode, "paid", StringComparison.OrdinalIgnoreCase))
            {
                return _modePaidText;
            }

            return _statusUnknownText;
        }

        private string FormatVerifyStatus(bool? verified)
        {
            if (verified == null)
            {
                return _statusUnknownText;
            }

            return verified.Value ? _verifiedText : _unverifiedText;
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
                var emptyText = LocalizationManager.T("WaveUI.Scripts.Toast.ScriptEmpty");
                _toast(emptyText);
                return;
            }

            try
            {
                Clipboard.SetText(card.Script);
                _toast(LocalizationManager.T("WaveUI.Scripts.Toast.Copied"));
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private async void ExecuteScript_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el)
            {
                return;
            }

            if (el.DataContext is not ScriptCard card)
            {
                return;
            }

            try
            {
                RobloxRuntime.Initialize();
            }
            catch
            {
            }

            try
            {
                RobloxRuntime.RefreshRunningState();
            }
            catch
            {
            }

            if (!RobloxRuntime.IsRobloxRunning)
            {
                Executor.WaveUI.WaveToastService.ShowPrompt(
                    LocalizationManager.T("WaveUI.Common.Info"),
                    LocalizationManager.T("WaveUI.Roblox.Prompt.Open"),
                    LocalizationManager.T("WaveUI.Common.Yes"),
                    LocalizationManager.T("WaveUI.Common.No"),
                    () => _ = RobloxRuntime.TryLaunchRoblox(),
                    null);
                return;
            }

            if (string.IsNullOrWhiteSpace(card.Script))
            {
                var emptyText = LocalizationManager.T("WaveUI.Scripts.Toast.ScriptEmpty");
                _toast(emptyText);
                return;
            }

            try
            {
                if (!global::Executor.API.IsAttached())
                {
                    var attach = await global::Executor.API.AttachAsync(System.Threading.CancellationToken.None);
                    if (!attach.Success)
                    {
                        if (string.Equals(attach.Provider, "Xeno", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        _toast(attach.Message);
                        return;
                    }
                }

                var result = await global::Executor.API.ExecuteScriptAsync(card.Script, System.Threading.CancellationToken.None);

                if (!result.Success && string.Equals(result.Provider, "Xeno", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _toast(result.Message);
            }
            catch (Exception ex)
            {
                _toast(ex.Message);
            }
        }

        private void ScriptsView_OnLoaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _scriptsSearchText = ScriptsSearchBox?.Text ?? string.Empty;
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
                    UpdateSelectedScriptStatus(null);
                }

                var url = BuildFetchUrl(_page, MaxPerPage, _scriptsSearchText);

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Executor/1.0");

                using var res = await Http.SendAsync(req);
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("result", out var result))
                {
                    _toast(LocalizationManager.T("WaveUI.Scripts.Toast.InvalidResponse"));
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
                    var title = SafeGetString(s, "title") ?? LocalizationManager.T("WaveUI.Common.Untitled");
                    var script = SafeGetString(s, "script") ?? string.Empty;
                    var keyRequired = SafeGetBool(s, "key");
                    var verified = SafeGetBool(s, "verified");
                    var scriptType = SafeGetString(s, "scriptType") ?? SafeGetString(s, "mode");

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
                        KeyRequired = keyRequired,
                        Verified = verified,
                        ScriptType = scriptType ?? string.Empty,
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

        private static Uri BuildFetchUrl(int page, int max, string? searchText)
        {
            var sb = new StringBuilder();
            var normalizedSearch = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSearch))
            {
                sb.Append("https://scriptblox.com/api/script/fetch?");
            }
            else
            {
                sb.Append("https://scriptblox.com/api/script/search?q=");
                sb.Append(Uri.EscapeDataString(normalizedSearch));
                sb.Append('&');
            }
            sb.Append("page=").Append(Math.Max(1, page));
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

        private static bool? SafeGetBool(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var el))
            {
                return null;
            }

            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when el.TryGetInt32(out var num) => num != 0,
                _ => null,
            };
        }

        private void ScriptCard_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

            UpdateSelectedScriptStatus(card);
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

        private void ScriptsSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _scriptsSearchText = ScriptsSearchBox?.Text ?? string.Empty;
            _scriptsSearchTimer.Stop();
            _scriptsSearchTimer.Start();
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

        public sealed class ScriptCard : INotifyPropertyChanged
        {
            public string Title { get; set; } = string.Empty;
            public string GameName { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;
            public string Script { get; set; } = string.Empty;
            public bool? KeyRequired { get; set; }
            public bool? Verified { get; set; }
            public string ScriptType { get; set; } = string.Empty;

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
