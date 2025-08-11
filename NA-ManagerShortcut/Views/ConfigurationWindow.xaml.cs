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
                    if (!ValidateIpAddress(IpAddressBox.Text) ||
                        !ValidateIpAddress(SubnetMaskBox.Text) ||
                        !ValidateIpAddress(DefaultGatewayBox.Text))
                    {
                        MessageBox.Show("Please enter valid IP addresses.", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var primaryDns = StaticDnsRadio.IsChecked == true ? PreferredDnsBox.Text : "";
                    var secondaryDns = StaticDnsRadio.IsChecked == true ? AlternateDnsBox.Text : "";

                    if (!string.IsNullOrEmpty(primaryDns) && !ValidateIpAddress(primaryDns))
                    {
                        MessageBox.Show("Please enter a valid primary DNS address.", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!string.IsNullOrEmpty(secondaryDns) && !ValidateIpAddress(secondaryDns))
                    {
                        MessageBox.Show("Please enter a valid secondary DNS address.", "Validation Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    success = await _adapterService.SetStaticIpAsync(
                        _adapter.DeviceId,
                        IpAddressBox.Text,
                        SubnetMaskBox.Text,
                        DefaultGatewayBox.Text,
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
            return IPAddress.TryParse(ipAddress, out _);
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