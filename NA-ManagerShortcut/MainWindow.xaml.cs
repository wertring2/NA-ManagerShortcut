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

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
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
            
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_N);
            
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
                // Prevent multiple clicks
                if (adapter.IsTransitioning)
                {
                    e.Handled = true;
                    return;
                }
                
                e.Handled = true;
                adapter.IsTransitioning = true; // Set transitioning state
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
                    adapter.IsTransitioning = false; // Clear transitioning state
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

        private void RenameAdapter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                _viewModel.RenameAdapterCommand.Execute(adapter);
            }
        }

        private void HideAdapter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is NetworkAdapterInfo adapter)
            {
                _viewModel.ToggleHideAdapterCommand.Execute(adapter);
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

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            var guideWindow = new Window
            {
                Title = "User Guide - Network Adapter Manager",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/favicon.ico"))
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel();

            // Title
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Network Adapter Manager - User Guide",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Hotkeys section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Hotkeys:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "• Ctrl + Alt + N : Show/Hide application window",
                Margin = new Thickness(20, 0, 0, 5)
            });

            // Features section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "\nFeatures:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 10)
            });

            var features = new[]
            {
                "• Enable/Disable network adapters with one click",
                "• Configure static IP or DHCP",
                "• Rename adapters with custom names",
                "• Hide/Show adapters from the list",
                "• Save and apply network profiles",
                "• Real-time network statistics monitoring",
                "• System tray integration",
                "• Always-on-top overlay window"
            };

            foreach (var feature in features)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = feature,
                    Margin = new Thickness(20, 0, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // How to use section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "\nHow to Use:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 10)
            });

            var instructions = new[]
            {
                "1. Toggle Adapter: Click the switch button to enable/disable",
                "2. Configure IP: Right-click adapter → Configure IP",
                "3. Rename Adapter: Right-click adapter → Rename Adapter",
                "4. Hide Adapter: Right-click adapter → Hide Adapter",
                "5. Show Hidden: Check 'Show Hidden' checkbox in title bar",
                "6. Copy Details: Right-click adapter → Copy Details",
                "7. Reset Adapter: Right-click adapter → Reset Adapter"
            };

            foreach (var instruction in instructions)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = instruction,
                    Margin = new Thickness(20, 0, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Requirements
            stackPanel.Children.Add(new TextBlock
            {
                Text = "\nRequirements:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 10)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "• Windows 10/11\n• Administrator privileges\n• .NET 9.0 Runtime",
                Margin = new Thickness(20, 0, 0, 5)
            });

            scrollViewer.Content = stackPanel;
            guideWindow.Content = scrollViewer;
            guideWindow.ShowDialog();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new Window
            {
                Title = "About - Network Adapter Manager",
                Width = 450,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/favicon.ico"))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Logo and title
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };

            headerPanel.Children.Add(new System.Windows.Controls.Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/favicon.ico")),
                Width = 64,
                Height = 64,
                Margin = new Thickness(0, 0, 0, 10)
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Network Adapter Manager",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Version 1.0.0",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });

            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Credits
            var creditsPanel = new StackPanel
            {
                Margin = new Thickness(40, 0, 40, 20)
            };

            creditsPanel.Children.Add(new TextBlock
            {
                Text = "Developed by",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            creditsPanel.Children.Add(new TextBlock
            {
                Text = "Q WAVE COMPANY LIMITED",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                Margin = new Thickness(0, 0, 0, 20)
            });

            creditsPanel.Children.Add(new TextBlock
            {
                Text = "© 2024 Q WAVE COMPANY LIMITED",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            creditsPanel.Children.Add(new TextBlock
            {
                Text = "All Rights Reserved",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            Grid.SetRow(creditsPanel, 1);
            grid.Children.Add(creditsPanel);

            // Close button
            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeButton.Click += (s, args) => aboutWindow.Close();

            Grid.SetRow(closeButton, 2);
            grid.Children.Add(closeButton);

            aboutWindow.Content = grid;
            aboutWindow.ShowDialog();
        }
        
    }
}