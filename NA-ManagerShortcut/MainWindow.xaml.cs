using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NA_ManagerShortcut.Commands;
using NA_ManagerShortcut.Models;
using NA_ManagerShortcut.Services;
using NA_ManagerShortcut.ViewModels;
using NA_ManagerShortcut.Views;

namespace NA_ManagerShortcut
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private HwndSource? _hwndSource;
        private const int HOTKEY_ID = 9000;
        private ConfigurationWindow? _configWindow;
        private ProfileWindow? _profileWindow;
        private DebugWindow? _debugWindow;
        private readonly ClaudeCodeInterface _claudeInterface;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_WIN = 0x0008;
        private const uint VK_N = 0x4E;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _claudeInterface = new ClaudeCodeInterface();
            
            _viewModel.ProfilePanelRequested += (s, e) => ShowProfileWindow();
            
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            
            SetupDebugHotkey();
            
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
            
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_WIN, VK_N);
            
            _viewModel.StartAutoRefresh();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindow();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (IsVisible)
                    HideWindow();
                else
                    ShowWindow();
                    
                handled = true;
            }
            
            return IntPtr.Zero;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                HideWindow();
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Do you want to exit the application?", 
                "Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                ExitApplication();
            }
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private void HideWindow()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) => Hide();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ExitApplication()
        {
            _viewModel.StopAutoRefresh();
            
            if (_hwndSource != null)
            {
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
            }
            
            TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void AdapterItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is NetworkAdapterInfo adapter)
            {
                _viewModel.SelectedAdapter = adapter;
            }
        }

        private async void ToggleAdapter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Tag is NetworkAdapterInfo adapter)
            {
                e.Handled = true;
                toggle.IsEnabled = false; // Disable button during operation
                
                // Start debug monitoring
                var debugMonitor = DebugMonitor.Instance;
                debugMonitor.StartMonitoring(true);
                
                try
                {
                    debugMonitor.LogEvent($"Toggle adapter clicked: {adapter.Name} (Current state: {adapter.IsEnabled})", 
                        EventType.Info, new Dictionary<string, object>
                        {
                            ["AdapterName"] = adapter.Name,
                            ["DeviceId"] = adapter.DeviceId,
                            ["CurrentState"] = adapter.IsEnabled,
                            ["TargetState"] = !adapter.IsEnabled
                        });
                    
                    var service = new NetworkAdapterServiceFixed();
                    bool success;
                    
                    if (adapter.IsEnabled)
                    {
                        debugMonitor.LogEvent($"Attempting to disable adapter: {adapter.Name}", EventType.Info);
                        success = await service.DisableAdapterAsync(adapter.DeviceId);
                    }
                    else
                    {
                        debugMonitor.LogEvent($"Attempting to enable adapter: {adapter.Name}", EventType.Info);
                        success = await service.EnableAdapterAsync(adapter.DeviceId);
                    }
                    
                    debugMonitor.LogEvent($"Toggle result: {(success ? "Success" : "Failed")}", 
                        success ? EventType.Info : EventType.Error,
                        new Dictionary<string, object>
                        {
                            ["Success"] = success,
                            ["AdapterName"] = adapter.Name
                        });
                    
                    if (!success)
                    {
                        // Revert toggle state if operation failed
                        toggle.IsChecked = adapter.IsEnabled;
                        MessageBox.Show($"Failed to {(adapter.IsEnabled ? "disable" : "enable")} adapter {adapter.Name}.\nPlease check if running as Administrator.", 
                            "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    
                    // Add delay to allow adapter state to update
                    await Task.Delay(1000);
                    
                    _viewModel.RefreshCommand.Execute(null);
                }
                catch (Exception ex)
                {
                    debugMonitor.LogException(ex, new Dictionary<string, object>
                    {
                        ["Operation"] = "ToggleAdapter",
                        ["AdapterName"] = adapter.Name
                    });
                    
                    toggle.IsChecked = adapter.IsEnabled;
                    MessageBox.Show($"Error toggling adapter: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    toggle.IsEnabled = true; // Re-enable button
                }
            }
        }

        private void ConfigureIP_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                _viewModel.SelectedAdapter = adapter;
                ShowConfigurationWindow(adapter);
            }
        }

        private async void ResetAdapter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                var service = new NetworkAdapterServiceFixed();
                await service.ResetAdapterAsync(adapter.DeviceId);
                _viewModel.RefreshCommand.Execute(null);
            }
        }

        private async void RenewIP_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                var service = new NetworkAdapterServiceFixed();
                await service.RenewIpAddressAsync(adapter.Name);
                _viewModel.RefreshCommand.Execute(null);
            }
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                var details = new StringBuilder();
                details.AppendLine($"Name: {adapter.Name}");
                details.AppendLine($"Description: {adapter.Description}");
                details.AppendLine($"Status: {adapter.Status}");
                details.AppendLine($"IP Address: {adapter.IpAddress}");
                details.AppendLine($"Subnet Mask: {adapter.SubnetMask}");
                details.AppendLine($"Default Gateway: {adapter.DefaultGateway}");
                details.AppendLine($"DNS Servers: {adapter.DnsServers}");
                details.AppendLine($"MAC Address: {adapter.MacAddress}");
                details.AppendLine($"DHCP Enabled: {adapter.IsDhcpEnabled}");
                details.AppendLine($"Speed: {adapter.FormattedSpeed}");
                details.AppendLine($"Bytes Received: {adapter.FormattedBytesReceived}");
                details.AppendLine($"Bytes Sent: {adapter.FormattedBytesSent}");
                
                Clipboard.SetText(details.ToString());
                _viewModel.StatusMessage = "Details copied to clipboard";
            }
        }

        private void ShowConfigurationWindow(NetworkAdapterInfo adapter)
        {
            if (_configWindow == null || !_configWindow.IsLoaded)
            {
                _configWindow = new ConfigurationWindow(adapter);
                _configWindow.Owner = this;
                _configWindow.ConfigurationApplied += (s, e) =>
                {
                    _viewModel.RefreshCommand.Execute(null);
                };
            }
            else
            {
                _configWindow.UpdateAdapter(adapter);
            }
            
            _configWindow.Show();
            _configWindow.Activate();
        }

        private void ShowProfileWindow()
        {
            if (_profileWindow == null || !_profileWindow.IsLoaded)
            {
                _profileWindow = new ProfileWindow(_viewModel);
                _profileWindow.Owner = this;
            }
            
            _profileWindow.Show();
            _profileWindow.Activate();
        }
        
        private void SetupDebugHotkey()
        {
            try
            {
                KeyBinding debugBinding = new KeyBinding(
                    new RelayCommand(_ => ShowDebugWindow()),
                    new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift));
                InputBindings.Add(debugBinding);
            }
            catch { }
        }
        
        private void ShowDebugWindow()
        {
            if (_debugWindow == null || !_debugWindow.IsLoaded)
            {
                _debugWindow = new DebugWindow();
                _debugWindow.Owner = this;
                _debugWindow.Closed += (s, e) => _debugWindow = null;
            }
            
            _debugWindow.Show();
            _debugWindow.Activate();
        }
        
    }
}