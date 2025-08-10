using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using NA_ManagerShortcut.Commands;
using NA_ManagerShortcut.Models;
using NA_ManagerShortcut.Services;

namespace NA_ManagerShortcut.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly NetworkAdapterService _adapterService;
        private readonly ProfileManager _profileManager;
        private readonly DispatcherTimer _refreshTimer;
        
        private ObservableCollection<NetworkAdapterInfo> _networkAdapters = new();
        private ObservableCollection<NetworkProfile> _profiles = new();
        private NetworkAdapterInfo? _selectedAdapter;
        private NetworkProfile? _selectedProfile;
        private string _searchText = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoading;
        private bool _showConfigPanel;
        private bool _showProfilePanel;

        public ObservableCollection<NetworkAdapterInfo> NetworkAdapters
        {
            get => _networkAdapters;
            set => SetProperty(ref _networkAdapters, value);
        }

        public ObservableCollection<NetworkAdapterInfo> FilteredAdapters
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return NetworkAdapters;

                var filtered = NetworkAdapters.Where(a =>
                    a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.IpAddress.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return new ObservableCollection<NetworkAdapterInfo>(filtered);
            }
        }

        public ObservableCollection<NetworkProfile> Profiles
        {
            get => _profiles;
            set => SetProperty(ref _profiles, value);
        }

        public NetworkAdapterInfo? SelectedAdapter
        {
            get => _selectedAdapter;
            set
            {
                if (SetProperty(ref _selectedAdapter, value))
                {
                    OnPropertyChanged(nameof(IsAdapterSelected));
                    ((RelayCommand)ToggleAdapterCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ConfigureAdapterCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ResetAdapterCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public NetworkProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    ((RelayCommand)ApplyProfileCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteProfileCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(FilteredAdapters));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool ShowConfigPanel
        {
            get => _showConfigPanel;
            set => SetProperty(ref _showConfigPanel, value);
        }

        public bool ShowProfilePanel
        {
            get => _showProfilePanel;
            set => SetProperty(ref _showProfilePanel, value);
        }

        public bool IsAdapterSelected => SelectedAdapter != null;

        public ICommand RefreshCommand { get; }
        public ICommand ToggleAdapterCommand { get; }
        public ICommand ConfigureAdapterCommand { get; }
        public ICommand ResetAdapterCommand { get; }
        public ICommand FlushDnsCommand { get; }
        public ICommand ApplyProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand ImportProfileCommand { get; }
        public ICommand ExportProfileCommand { get; }
        public ICommand ToggleConfigPanelCommand { get; }
        public ICommand ToggleProfilePanelCommand { get; }

        public event EventHandler? ProfilePanelRequested;

        public MainViewModel()
        {
            _adapterService = new NetworkAdapterService();
            _profileManager = new ProfileManager();
            
            _adapterService.StatusChanged += OnStatusChanged;
            _adapterService.AdaptersUpdated += async (s, e) => await RefreshAdaptersAsync();
            _profileManager.ProfilesChanged += (s, e) => LoadProfiles();

            RefreshCommand = new RelayCommand(async _ => await RefreshAdaptersAsync());
            ToggleAdapterCommand = new RelayCommand(async _ => await ToggleSelectedAdapterAsync(), 
                _ => SelectedAdapter != null);
            ConfigureAdapterCommand = new RelayCommand(_ => ShowConfigPanel = !ShowConfigPanel, 
                _ => SelectedAdapter != null);
            ResetAdapterCommand = new RelayCommand(async _ => await ResetSelectedAdapterAsync(), 
                _ => SelectedAdapter != null);
            FlushDnsCommand = new RelayCommand(async _ => await FlushDnsAsync());
            ApplyProfileCommand = new RelayCommand(async _ => await ApplySelectedProfileAsync(), 
                _ => SelectedProfile != null);
            SaveProfileCommand = new RelayCommand(async _ => await SaveCurrentConfigurationAsync());
            DeleteProfileCommand = new RelayCommand(async _ => await DeleteSelectedProfileAsync(), 
                _ => SelectedProfile != null);
            ImportProfileCommand = new RelayCommand(async _ => await ImportProfileAsync());
            ExportProfileCommand = new RelayCommand(async _ => await ExportSelectedProfileAsync());
            ToggleConfigPanelCommand = new RelayCommand(_ => ShowConfigPanel = !ShowConfigPanel);
            ToggleProfilePanelCommand = new RelayCommand(_ => 
            {
                ShowProfilePanel = !ShowProfilePanel;
                ProfilePanelRequested?.Invoke(this, EventArgs.Empty);
            });

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshAdaptersAsync();

            Task.Run(async () =>
            {
                await RefreshAdaptersAsync();
                LoadProfiles();
            });
        }

        private async Task RefreshAdaptersAsync()
        {
            IsLoading = true;
            try
            {
                var adapters = await _adapterService.GetNetworkAdaptersAsync();
                App.Current?.Dispatcher.Invoke(() =>
                {
                    NetworkAdapters.Clear();
                    foreach (var adapter in adapters)
                    {
                        NetworkAdapters.Add(adapter);
                    }
                    OnPropertyChanged(nameof(FilteredAdapters));
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleSelectedAdapterAsync()
        {
            if (SelectedAdapter == null) return;
            
            if (SelectedAdapter.IsEnabled)
                await _adapterService.DisableAdapterAsync(SelectedAdapter.DeviceId);
            else
                await _adapterService.EnableAdapterAsync(SelectedAdapter.DeviceId);
        }

        private async Task ResetSelectedAdapterAsync()
        {
            if (SelectedAdapter == null) return;
            await _adapterService.ResetAdapterAsync(SelectedAdapter.DeviceId);
        }

        private async Task FlushDnsAsync()
        {
            await _adapterService.FlushDnsAsync();
        }

        private async Task ApplySelectedProfileAsync()
        {
            if (SelectedProfile == null) return;
            await _profileManager.ApplyProfileAsync(SelectedProfile.Id, _adapterService);
        }

        private async Task SaveCurrentConfigurationAsync()
        {
            if (NetworkAdapters.Count == 0) return;

            var profile = new NetworkProfile
            {
                Name = $"Profile {DateTime.Now:yyyy-MM-dd HH:mm}",
                Description = "Current network configuration"
            };

            foreach (var adapter in NetworkAdapters)
            {
                var config = new AdapterConfiguration
                {
                    AdapterName = adapter.Name,
                    AdapterDeviceId = adapter.DeviceId,
                    UseDhcp = adapter.IsDhcpEnabled,
                    IpAddress = adapter.IpAddress,
                    SubnetMask = adapter.SubnetMask,
                    DefaultGateway = adapter.DefaultGateway
                };

                if (!string.IsNullOrEmpty(adapter.DnsServers))
                {
                    var dns = adapter.DnsServers.Split(',').Select(d => d.Trim()).ToArray();
                    config.PreferredDns = dns.Length > 0 ? dns[0] : string.Empty;
                    config.AlternateDns = dns.Length > 1 ? dns[1] : string.Empty;
                }

                profile.AdapterConfigurations.Add(config);
            }

            await _profileManager.SaveProfileAsync(profile);
            LoadProfiles();
        }

        private async Task DeleteSelectedProfileAsync()
        {
            if (SelectedProfile == null) return;
            await _profileManager.DeleteProfileAsync(SelectedProfile.Id);
        }

        private async Task ImportProfileAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Network Profile"
            };

            if (dialog.ShowDialog() == true)
            {
                await _profileManager.ImportProfileAsync(dialog.FileName);
            }
        }

        private async Task ExportSelectedProfileAsync()
        {
            if (SelectedProfile == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Export Network Profile",
                FileName = $"{SelectedProfile.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                await _profileManager.ExportProfileAsync(SelectedProfile.Id, dialog.FileName);
            }
        }

        private void LoadProfiles()
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                Profiles.Clear();
                foreach (var profile in _profileManager.GetProfiles())
                {
                    Profiles.Add(profile);
                }
            });
        }

        private void OnStatusChanged(object? sender, string message)
        {
            App.Current?.Dispatcher.Invoke(() => StatusMessage = message);
        }

        public void StartAutoRefresh()
        {
            _refreshTimer.Start();
        }

        public void StopAutoRefresh()
        {
            _refreshTimer.Stop();
        }
    }
}