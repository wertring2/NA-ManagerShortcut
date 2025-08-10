using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NA_ManagerShortcut.Models;
using NA_ManagerShortcut.Services;
using NA_ManagerShortcut.ViewModels;

namespace NA_ManagerShortcut.Views
{
    public partial class ProfileWindow : Window, INotifyPropertyChanged
    {
        private readonly ProfileManager _profileManager;
        private readonly NetworkAdapterService _adapterService;
        private readonly MainViewModel _mainViewModel;
        private NetworkProfile? _selectedProfile;
        private string _statusMessage = "";

        public ObservableCollection<NetworkProfile> Profiles { get; }
        
        public NetworkProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProfileSelected));
            }
        }

        public bool IsProfileSelected => SelectedProfile != null;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ProfileWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = this;
            
            _mainViewModel = mainViewModel;
            _profileManager = new ProfileManager();
            _adapterService = new NetworkAdapterService();
            
            Profiles = new ObservableCollection<NetworkProfile>();
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            Profiles.Clear();
            foreach (var profile in _profileManager.GetProfiles())
            {
                Profiles.Add(profile);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ProfileItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is NetworkProfile profile)
            {
                SelectedProfile = profile;
            }
        }

        private async void SaveCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveProfileDialog();
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var profile = new NetworkProfile
                {
                    Name = dialog.ProfileName,
                    Description = dialog.ProfileDescription
                };

                foreach (var adapter in _mainViewModel.NetworkAdapters)
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
                        config.PreferredDns = dns.Length > 0 ? dns[0] : "";
                        config.AlternateDns = dns.Length > 1 ? dns[1] : "";
                    }

                    profile.AdapterConfigurations.Add(config);
                }

                if (await _profileManager.SaveProfileAsync(profile))
                {
                    LoadProfiles();
                    StatusMessage = "Profile saved successfully";
                }
                else
                {
                    StatusMessage = "Failed to save profile";
                }
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Network Profile"
            };

            if (dialog.ShowDialog() == true)
            {
                var profile = await _profileManager.ImportProfileAsync(dialog.FileName);
                if (profile != null)
                {
                    LoadProfiles();
                    StatusMessage = "Profile imported successfully";
                }
                else
                {
                    StatusMessage = "Failed to import profile";
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
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
                if (await _profileManager.ExportProfileAsync(SelectedProfile.Id, dialog.FileName))
                {
                    StatusMessage = "Profile exported successfully";
                }
                else
                {
                    StatusMessage = "Failed to export profile";
                }
            }
        }

        private async void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NetworkProfile profile)
            {
                StatusMessage = "Applying profile...";
                
                if (await _profileManager.ApplyProfileAsync(profile.Id, _adapterService))
                {
                    StatusMessage = "Profile applied successfully";
                    _mainViewModel.RefreshCommand.Execute(null);
                }
                else
                {
                    StatusMessage = "Failed to apply profile";
                }
            }
        }

        private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NetworkProfile profile)
            {
                var result = MessageBox.Show($"Delete profile '{profile.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (await _profileManager.DeleteProfileAsync(profile.Id))
                    {
                        LoadProfiles();
                        StatusMessage = "Profile deleted";
                    }
                    else
                    {
                        StatusMessage = "Failed to delete profile";
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}