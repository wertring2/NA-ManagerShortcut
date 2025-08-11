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
        private readonly NetworkAdapterServiceFixed _adapterService;
        private readonly ProfileManager _profileManager;
        private readonly AdapterPreferencesService _preferencesService;
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
                var adapters = ShowHiddenAdapters 
                    ? NetworkAdapters.AsEnumerable() 
                    : NetworkAdapters.Where(a => !a.IsHidden);
                
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    adapters = adapters.Where(a =>
                        a.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        a.IpAddress.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }

                return new ObservableCollection<NetworkAdapterInfo>(adapters.ToList());
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
        public ICommand RenameAdapterCommand { get; }
        public ICommand ToggleHideAdapterCommand { get; }
        public ICommand ShowHiddenAdaptersCommand { get; }

        private bool _showHiddenAdapters;
        public bool ShowHiddenAdapters
        {
            get => _showHiddenAdapters;
            set 
            { 
                SetProperty(ref _showHiddenAdapters, value);
                OnPropertyChanged(nameof(FilteredAdapters));
            }
        }

        public event EventHandler? ProfilePanelRequested;

        public MainViewModel()
        {
            _adapterService = new NetworkAdapterServiceFixed();
            _profileManager = new ProfileManager();
            _preferencesService = new AdapterPreferencesService();
            
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
            RenameAdapterCommand = new RelayCommand(async param => await RenameAdapterAsync(param),
                _ => SelectedAdapter != null);
            ToggleHideAdapterCommand = new RelayCommand(async param => await ToggleHideAdapterAsync(param));
            ShowHiddenAdaptersCommand = new RelayCommand(_ => ShowHiddenAdapters = !ShowHiddenAdapters);

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // Faster refresh rate
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
                var newAdapters = await _adapterService.GetNetworkAdaptersAsync();
                
                // Load preferences for each adapter
                foreach (var adapter in newAdapters)
                {
                    var pref = _preferencesService.GetPreference(adapter.DeviceId);
                    if (pref != null)
                    {
                        adapter.CustomName = pref.CustomName;
                        adapter.IsHidden = pref.IsHidden;
                    }
                }
                
                App.Current?.Dispatcher.Invoke(() =>
                {
                    // Smart Refresh: Only update if there are actual changes
                    UpdateAdaptersSmartly(newAdapters);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateAdaptersSmartly(List<NetworkAdapterInfo> newAdapters)
        {
            // Check if we need to update at all
            bool needsUpdate = false;
            
            // Check if adapter count changed
            if (NetworkAdapters.Count != newAdapters.Count)
            {
                needsUpdate = true;
            }
            else
            {
                // Check each adapter for changes
                for (int i = 0; i < NetworkAdapters.Count; i++)
                {
                    var existing = NetworkAdapters.FirstOrDefault(a => a.DeviceId == newAdapters[i].DeviceId);
                    if (existing == null)
                    {
                        needsUpdate = true;
                        break;
                    }
                    
                    // Always update traffic statistics
                    UpdateTrafficStatistics(existing, newAdapters[i]);
                    
                    // Check if important properties changed (but NOT just stats)
                    if (HasSignificantChanges(existing, newAdapters[i]))
                    {
                        // Update only the changed properties without replacing the object
                        UpdateAdapterProperties(existing, newAdapters[i]);
                    }
                }
            }
            
            // Only rebuild collection if structure changed
            if (needsUpdate)
            {
                NetworkAdapters.Clear();
                foreach (var adapter in newAdapters)
                {
                    NetworkAdapters.Add(adapter);
                }
                OnPropertyChanged(nameof(FilteredAdapters));
            }
        }
        
        private bool HasSignificantChanges(NetworkAdapterInfo existing, NetworkAdapterInfo newAdapter)
        {
            // Check only significant properties (not stats that change frequently)
            return existing.IsEnabled != newAdapter.IsEnabled ||
                   existing.Status != newAdapter.Status ||
                   existing.IpAddress != newAdapter.IpAddress ||
                   existing.SubnetMask != newAdapter.SubnetMask ||
                   existing.DefaultGateway != newAdapter.DefaultGateway ||
                   existing.DnsServers != newAdapter.DnsServers ||
                   existing.IsDhcpEnabled != newAdapter.IsDhcpEnabled;
        }
        
        private void UpdateTrafficStatistics(NetworkAdapterInfo existing, NetworkAdapterInfo newAdapter)
        {
            // Always update traffic stats to keep them current
            existing.BytesReceived = newAdapter.BytesReceived;
            existing.BytesSent = newAdapter.BytesSent;
            existing.Speed = newAdapter.Speed;
            
            // Preserve custom preferences
            newAdapter.CustomName = existing.CustomName;
            newAdapter.IsHidden = existing.IsHidden;
        }
        
        private void UpdateAdapterProperties(NetworkAdapterInfo existing, NetworkAdapterInfo newAdapter)
        {
            // Update properties without triggering unnecessary bindings
            // Skip IsEnabled update if it hasn't changed to prevent animation
            if (existing.IsEnabled != newAdapter.IsEnabled)
            {
                existing.IsEnabled = newAdapter.IsEnabled;
            }
            
            existing.Status = newAdapter.Status;
            existing.IpAddress = newAdapter.IpAddress;
            existing.SubnetMask = newAdapter.SubnetMask;
            existing.DefaultGateway = newAdapter.DefaultGateway;
            existing.DnsServers = newAdapter.DnsServers;
            existing.IsDhcpEnabled = newAdapter.IsDhcpEnabled;
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

        private async Task RenameAdapterAsync(object? parameter)
        {
            if (parameter is NetworkAdapterInfo adapter)
            {
                var dialog = new System.Windows.Controls.TextBox();
                var window = new System.Windows.Window
                {
                    Title = "Rename Adapter",
                    Content = new System.Windows.Controls.StackPanel
                    {
                        Margin = new System.Windows.Thickness(10),
                        Children =
                        {
                            new System.Windows.Controls.TextBlock { Text = $"Enter new name for {adapter.Name}:", Margin = new System.Windows.Thickness(0, 0, 0, 10) },
                            new System.Windows.Controls.TextBox { Text = adapter.CustomName, Width = 300, Name = "NameTextBox" },
                            new System.Windows.Controls.StackPanel
                            {
                                Orientation = System.Windows.Controls.Orientation.Horizontal,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                                Margin = new System.Windows.Thickness(0, 10, 0, 0),
                                Children =
                                {
                                    new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new System.Windows.Thickness(0, 0, 5, 0), IsDefault = true },
                                    new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true }
                                }
                            }
                        }
                    },
                    Width = 350,
                    Height = 150,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    ResizeMode = System.Windows.ResizeMode.NoResize
                };

                var stackPanel = (System.Windows.Controls.StackPanel)window.Content;
                var textBox = stackPanel.Children.OfType<System.Windows.Controls.TextBox>().First();
                var buttonPanel = (System.Windows.Controls.StackPanel)stackPanel.Children[2];
                var okButton = (System.Windows.Controls.Button)buttonPanel.Children[0];
                var cancelButton = (System.Windows.Controls.Button)buttonPanel.Children[1];

                okButton.Click += async (s, e) =>
                {
                    adapter.CustomName = textBox.Text;
                    await _preferencesService.SetCustomNameAsync(adapter.DeviceId, textBox.Text);
                    OnPropertyChanged(nameof(FilteredAdapters));
                    window.DialogResult = true;
                };

                cancelButton.Click += (s, e) => window.DialogResult = false;

                window.ShowDialog();
            }
        }

        private async Task ToggleHideAdapterAsync(object? parameter)
        {
            if (parameter is NetworkAdapterInfo adapter)
            {
                adapter.IsHidden = !adapter.IsHidden;
                await _preferencesService.SetHiddenAsync(adapter.DeviceId, adapter.IsHidden);
                OnPropertyChanged(nameof(FilteredAdapters));
            }
        }
    }
}