using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using NA_ManagerShortcut.Models;
using NA_ManagerShortcut.Services;

namespace NA_ManagerShortcut.Views
{
    public partial class ConfigurationWindow : Window
    {
        private NetworkAdapterInfo _adapter;
        private readonly NetworkAdapterServiceFixed _adapterService;
        
        public event EventHandler? ConfigurationApplied;

        public string AdapterName => _adapter?.Name ?? "Unknown";
        public string AdapterDescription => _adapter?.Description ?? "";

        public ConfigurationWindow(NetworkAdapterInfo adapter)
        {
            InitializeComponent();
            DataContext = this;
            _adapter = adapter;
            _adapterService = new NetworkAdapterServiceFixed();
            LoadCurrentConfiguration();
        }

        public void UpdateAdapter(NetworkAdapterInfo adapter)
        {
            _adapter = adapter;
            LoadCurrentConfiguration();
        }

        private void LoadCurrentConfiguration()
        {
            if (_adapter == null) return;

            if (_adapter.IsDhcpEnabled)
            {
                DhcpRadio.IsChecked = true;
                DhcpDnsRadio.IsChecked = true;
            }
            else
            {
                StaticRadio.IsChecked = true;
                IpAddressBox.Text = _adapter.IpAddress;
                SubnetMaskBox.Text = _adapter.SubnetMask;
                DefaultGatewayBox.Text = _adapter.DefaultGateway;
                
                if (!string.IsNullOrEmpty(_adapter.DnsServers))
                {
                    StaticDnsRadio.IsChecked = true;
                    var dns = _adapter.DnsServers.Split(',');
                    if (dns.Length > 0) PreferredDnsBox.Text = dns[0].Trim();
                    if (dns.Length > 1) AlternateDnsBox.Text = dns[1].Trim();
                }
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_adapter == null) return;

            try
            {
                bool success;
                
                if (DhcpRadio.IsChecked == true)
                {
                    success = await _adapterService.EnableDhcpAsync(_adapter.DeviceId);
                }
                else
                {
                    // Trim all input values
                    var ipAddress = IpAddressBox.Text?.Trim() ?? "";
                    var subnetMask = SubnetMaskBox.Text?.Trim() ?? "";
                    var defaultGateway = DefaultGatewayBox.Text?.Trim() ?? "";
                    
                    // IP Address and Subnet Mask are required
                    if (!ValidateIpAddress(ipAddress))
                    {
                        MessageBox.Show($"Please enter a valid IP address.\nCurrent value: '{IpAddressBox.Text}'", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    if (!ValidateIpAddress(subnetMask))
                    {
                        MessageBox.Show($"Please enter a valid subnet mask.\nCurrent value: '{SubnetMaskBox.Text}'", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    // Default Gateway is optional but must be valid if provided
                    if (!string.IsNullOrWhiteSpace(defaultGateway) && !ValidateIpAddress(defaultGateway))
                    {
                        MessageBox.Show($"Please enter a valid default gateway address.\nCurrent value: '{DefaultGatewayBox.Text}'", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var primaryDns = StaticDnsRadio.IsChecked == true ? PreferredDnsBox.Text?.Trim() ?? "" : "";
                    var secondaryDns = StaticDnsRadio.IsChecked == true ? AlternateDnsBox.Text?.Trim() ?? "" : "";

                    if (!string.IsNullOrEmpty(primaryDns) && !ValidateIpAddress(primaryDns))
                    {
                        MessageBox.Show($"Please enter a valid primary DNS address.\nCurrent value: '{PreferredDnsBox.Text}'", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!string.IsNullOrEmpty(secondaryDns) && !ValidateIpAddress(secondaryDns))
                    {
                        MessageBox.Show($"Please enter a valid secondary DNS address.\nCurrent value: '{AlternateDnsBox.Text}'", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    success = await _adapterService.SetStaticIpAsync(
                        _adapter.DeviceId,
                        ipAddress,
                        subnetMask,
                        defaultGateway,
                        primaryDns,
                        secondaryDns);
                }

                if (success)
                {
                    ConfigurationApplied?.Invoke(this, EventArgs.Empty);
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to apply configuration. Please check your settings and try again.", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;
            
            // Remove any extra spaces
            ipAddress = ipAddress.Trim();
            
            // Try to parse the IP address
            if (IPAddress.TryParse(ipAddress, out var parsedIp))
            {
                // Make sure it's IPv4 format
                if (parsedIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return true;
                }
            }
            
            return false;
        }

        private void DhcpRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (StaticIpPanel != null)
                StaticIpPanel.IsEnabled = false;
        }

        private void StaticRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (StaticIpPanel != null)
                StaticIpPanel.IsEnabled = true;
        }

        private void DhcpDnsRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (StaticDnsPanel != null)
                StaticDnsPanel.IsEnabled = false;
        }

        private void StaticDnsRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (StaticDnsPanel != null)
                StaticDnsPanel.IsEnabled = true;
        }
    }
}