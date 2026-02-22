using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Executor.WaveUI;

namespace Executor.WaveUI.WaveViews
{
    public partial class ClientsView : UserControl, INotifyPropertyChanged
    {
        private readonly ObservableCollection<RobloxClient> _clients = new();
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        private string _injectText = "Inject";
        private string _pidLabelText = "PID: ";
        private string _userNameLabelText = " / UserName: ";

        public string InjectText
        {
            get => _injectText;
            private set
            {
                if (_injectText == value) return;
                _injectText = value;
                OnPropertyChanged();
            }
        }

        public string PidLabelText
        {
            get => _pidLabelText;
            private set
            {
                if (_pidLabelText == value) return;
                _pidLabelText = value;
                OnPropertyChanged();
            }
        }

        public string UserNameLabelText
        {
            get => _userNameLabelText;
            private set
            {
                if (_userNameLabelText == value) return;
                _userNameLabelText = value;
                OnPropertyChanged();
            }
        }

        public ClientsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            DataContext = this;
            ClientsItemsControl.ItemsSource = _clients;

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshTimer.Tick += (_, _) => RefreshClients();

            SelectAllButton.Click += SelectAllButton_Click;

            LocalizationManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();
        }

        private void OnLanguageChanged()
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            try
            {
                ClientsTitleText.Text = LocalizationManager.T("WaveUI.Clients.Title");
                ClientsDescText.Text = LocalizationManager.T("WaveUI.Clients.Desc");
                InjectText = LocalizationManager.T("WaveUI.Clients.Inject");
                SelectAllButton.Content = LocalizationManager.T("WaveUI.Clients.SelectAll");
                PidLabelText = LocalizationManager.T("WaveUI.Clients.PidLabel");
                UserNameLabelText = LocalizationManager.T("WaveUI.Clients.UserNameLabel");
                UpdateSelectedCount();
            }
            catch
            {
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshClients();
            _refreshTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
            try
            {
                LocalizationManager.LanguageChanged -= OnLanguageChanged;
            }
            catch
            {
            }
        }

        private void RefreshClients()
        {
            try
            {
                var currentPids = GetRobloxProcessIds();
                var existingPids = _clients.Select(c => c.ProcessId).ToHashSet();

                foreach (var pid in currentPids)
                {
                    if (!existingPids.Contains(pid))
                    {
                        var (processName, userName, avatarUrl) = GetRobloxClientInfo(pid);
                        _clients.Add(new RobloxClient
                        {
                            ProcessId = pid,
                            ProcessName = processName,
                            UserName = userName,
                            AvatarUrl = avatarUrl,
                            IsSelected = false
                        });
                    }
                }

                var toRemove = _clients.Where(c => !currentPids.Contains(c.ProcessId)).ToList();
                foreach (var client in toRemove)
                {
                    _clients.Remove(client);
                }

                UpdateSelectedCount();
            }
            catch
            {
            }
        }

        private static System.Collections.Generic.HashSet<int> GetRobloxProcessIds()
        {
            var pids = new System.Collections.Generic.HashSet<int>();
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        var name = p.ProcessName;
                        if (name.StartsWith("RobloxPlayer", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("RobloxStudio", StringComparison.OrdinalIgnoreCase))
                        {
                            pids.Add(p.Id);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return pids;
        }

        private static (string processName, string userName, string avatarUrl) GetRobloxClientInfo(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                var processName = p.ProcessName;
                var userName = GetUserNameFromProcess(p);
                var avatarUrl = $"https://www.roblox.com/headshot-thumbnail/image?userId=0&width=352&height=352&format=png";
                return (processName, userName, avatarUrl);
            }
            catch
            {
                return ("Unknown", "Unknown", "");
            }
        }

        private static string GetUserNameFromProcess(Process p)
        {
            try
            {
                var cmdLine = GetProcessCommandLine(p.Id);
                if (!string.IsNullOrEmpty(cmdLine))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cmdLine, @"-t\s+(\S+)|--username[=:\s]+(\S+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    }
                }
            }
            catch
            {
            }
            return "Unknown";
        }

        private static string GetProcessCommandLine(int pid)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
                foreach (var obj in searcher.Get())
                {
                    return obj["CommandLine"]?.ToString() ?? "";
                }
            }
            catch
            {
            }
            return "";
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            var allSelected = _clients.All(c => c.IsSelected);
            foreach (var client in _clients)
            {
                client.IsSelected = !allSelected;
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            var count = _clients.Count(c => c.IsSelected);
            var selectedText = count == 1 
                ? LocalizationManager.T("WaveUI.Clients.SelectedOne")
                : string.Format(LocalizationManager.T("WaveUI.Clients.SelectedMany"), count);
            SelectedCountText.Text = selectedText;
        }

        private void InjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RobloxClient client)
            {
                try
                {
                    var result = API.AttachAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    if (result.Success)
                    {
                        WaveToastService.Show(
                            LocalizationManager.T("WaveUI.Common.Success"),
                            LocalizationManager.T("WaveUI.Clients.Injected"));
                    }
                    else
                    {
                        WaveToastService.Show(
                            LocalizationManager.T("WaveUI.Common.Error"),
                            result.Message ?? "Injection failed");
                    }
                }
                catch (Exception ex)
                {
                    WaveToastService.Show(
                        LocalizationManager.T("WaveUI.Common.Error"),
                        ex.Message);
                }
            }
        }
    }

    public class RobloxClient : INotifyPropertyChanged
    {
        private int _processId;
        private string _processName = string.Empty;
        private string _userName = string.Empty;
        private string _avatarUrl = string.Empty;
        private bool _isSelected;

        public int ProcessId
        {
            get => _processId;
            set { _processId = value; OnPropertyChanged(); }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
