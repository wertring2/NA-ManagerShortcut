using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using NA_ManagerShortcut.Models;

namespace NA_ManagerShortcut.Services
{
    public class NetworkAdapterServiceFixed
    {
        private readonly DebugMonitor _debugMonitor;
        
        public event EventHandler<string>? StatusChanged;
        public event EventHandler? AdaptersUpdated;
        
        public NetworkAdapterServiceFixed()
        {
            _debugMonitor = DebugMonitor.Instance;
        }

        public async Task<bool> ToggleAdapterAsync(string deviceId, bool enable)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Try multiple methods in order of preference
            var methods = new List<Func<Task<bool>>>
            {
                () => ToggleViaWMIAsync(deviceId, enable),
                () => ToggleViaNetshAsync(deviceId, enable),
                () => ToggleViaPowerShellAsync(deviceId, enable),
                () => ToggleViaDevconAsync(deviceId, enable)
            };

            foreach (var method in methods)
            {
                try
                {
                    _debugMonitor.LogEvent($"Attempting method {methods.IndexOf(method) + 1} of {methods.Count}", EventType.Info);
                    
                    var result = await method();
                    if (result)
                    {
                        _debugMonitor.LogNetworkOperation(
                            enable ? "EnableAdapter" : "DisableAdapter",
                            deviceId,
                            true,
                            stopwatch.Elapsed);
                        
                        StatusChanged?.Invoke(this, $"Adapter {(enable ? "enabled" : "disabled")} successfully");
                        AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogEvent($"Method {methods.IndexOf(method) + 1} failed: {ex.Message}", EventType.Warning);
                }
            }

            _debugMonitor.LogNetworkOperation(
                enable ? "EnableAdapter" : "DisableAdapter",
                deviceId,
                false,
                stopwatch.Elapsed);
            
            StatusChanged?.Invoke(this, $"Failed to {(enable ? "enable" : "disable")} adapter");
            return false;
        }

        private async Task<bool> ToggleViaWMIAsync(string deviceId, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _debugMonitor.LogEvent($"WMI: Attempting to {(enable ? "enable" : "disable")} device {deviceId}", EventType.Info);
                    
                    // Method 1: Using Win32_NetworkAdapter
                    using (var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'"))
                    {
                        var adapters = searcher.Get();
                        foreach (ManagementObject adapter in adapters)
                        {
                            try
                            {
                                // Get the correct method
                                var methodName = enable ? "Enable" : "Disable";
                                
                                // Try direct invocation first
                                var result = adapter.InvokeMethod(methodName, null);
                                
                                uint returnValue = 0;
                                if (result != null)
                                {
                                    returnValue = Convert.ToUInt32(result);
                                }
                                
                                _debugMonitor.LogEvent($"WMI Result: {returnValue}", EventType.Info);
                                
                                if (returnValue == 0)
                                {
                                    return true;
                                }
                                else if (returnValue == 5)
                                {
                                    _debugMonitor.LogEvent("Access Denied - Trying alternative WMI approach", EventType.Warning);
                                    
                                    // Try via WMI with different query
                                    return TryAlternativeWMI(deviceId, enable);
                                }
                            }
                            catch (ManagementException me)
                            {
                                _debugMonitor.LogEvent($"WMI Management Exception: {me.Message}", EventType.Error);
                                
                                // Try NetworkAdapterConfiguration approach
                                return TryNetworkConfigurationApproach(deviceId, enable);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Method"] = "WMI",
                        ["DeviceId"] = deviceId 
                    });
                }
                
                return false;
            });
        }

        private bool TryAlternativeWMI(string deviceId, bool enable)
        {
            try
            {
                // Get adapter name first
                string adapterName = GetAdapterNameFromDeviceId(deviceId);
                if (string.IsNullOrEmpty(adapterName))
                    return false;

                // Use MSFT_NetAdapter WMI class (Windows 8+)
                using (var searcher = new ManagementObjectSearcher(@"root\StandardCimv2",
                    $"SELECT * FROM MSFT_NetAdapter WHERE DeviceID = '{deviceId}'"))
                {
                    var adapters = searcher.Get();
                    foreach (ManagementObject adapter in adapters)
                    {
                        var methodName = enable ? "Enable" : "Disable";
                        var result = adapter.InvokeMethod(methodName, null);
                        
                        if (result != null && Convert.ToUInt32(result) == 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _debugMonitor.LogEvent($"Alternative WMI failed: {ex.Message}", EventType.Warning);
            }
            
            return false;
        }

        private bool TryNetworkConfigurationApproach(string deviceId, bool enable)
        {
            try
            {
                // Get the adapter configuration
                using (var adapterSearcher = new ManagementObjectSearcher(
                    $"SELECT Index FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'"))
                {
                    var adapter = adapterSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (adapter != null)
                    {
                        var index = adapter["Index"];
                        
                        using (var configSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Index = {index}"))
                        {
                            var config = configSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                            if (config != null)
                            {
                                if (enable)
                                {
                                    // Try to enable by setting IP
                                    var result = config.InvokeMethod("EnableDHCP", null);
                                    return result != null && Convert.ToUInt32(result) == 0;
                                }
                                else
                                {
                                    // Try to disable by releasing IP
                                    var result = config.InvokeMethod("ReleaseDHCPLease", null);
                                    return result != null && Convert.ToUInt32(result) == 0;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _debugMonitor.LogEvent($"NetworkConfiguration approach failed: {ex.Message}", EventType.Warning);
            }
            
            return false;
        }

        private async Task<bool> ToggleViaNetshAsync(string deviceId, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _debugMonitor.LogEvent($"Netsh: Attempting to {(enable ? "enable" : "disable")} device", EventType.Info);
                    
                    // Get adapter name
                    string adapterName = GetAdapterNameFromDeviceId(deviceId);
                    if (string.IsNullOrEmpty(adapterName))
                    {
                        _debugMonitor.LogEvent("Could not get adapter name for netsh", EventType.Warning);
                        return false;
                    }

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"interface set interface \"{adapterName}\" {(enable ? "enable" : "disable")}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            Verb = "runas"
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);

                    _debugMonitor.LogEvent($"Netsh output: {output}, Error: {error}, ExitCode: {process.ExitCode}", EventType.Info);

                    return process.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Method"] = "Netsh",
                        ["DeviceId"] = deviceId 
                    });
                    return false;
                }
            });
        }

        private async Task<bool> ToggleViaPowerShellAsync(string deviceId, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _debugMonitor.LogEvent($"PowerShell: Attempting to {(enable ? "enable" : "disable")} device", EventType.Info);
                    
                    string adapterName = GetAdapterNameFromDeviceId(deviceId);
                    if (string.IsNullOrEmpty(adapterName))
                        return false;

                    var command = enable 
                        ? $"Enable-NetAdapter -Name '{adapterName}' -Confirm:$false"
                        : $"Disable-NetAdapter -Name '{adapterName}' -Confirm:$false";

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-ExecutionPolicy Bypass -Command \"{command}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            Verb = "runas"
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);

                    _debugMonitor.LogEvent($"PowerShell output: {output}, Error: {error}, ExitCode: {process.ExitCode}", EventType.Info);

                    return process.ExitCode == 0 && string.IsNullOrEmpty(error);
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Method"] = "PowerShell",
                        ["DeviceId"] = deviceId 
                    });
                    return false;
                }
            });
        }

        private async Task<bool> ToggleViaDevconAsync(string deviceId, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _debugMonitor.LogEvent($"Devcon: Attempting to {(enable ? "enable" : "disable")} device", EventType.Info);
                    
                    // Check if devcon exists
                    var devconPath = System.IO.Path.Combine(Environment.SystemDirectory, "devcon.exe");
                    if (!System.IO.File.Exists(devconPath))
                    {
                        _debugMonitor.LogEvent("Devcon not found", EventType.Warning);
                        return false;
                    }

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = devconPath,
                            Arguments = $"{(enable ? "enable" : "disable")} \"@{deviceId}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            Verb = "runas"
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    _debugMonitor.LogEvent($"Devcon output: {output}, ExitCode: {process.ExitCode}", EventType.Info);

                    return process.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Method"] = "Devcon",
                        ["DeviceId"] = deviceId 
                    });
                    return false;
                }
            });
        }

        private string GetAdapterNameFromDeviceId(string deviceId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT Name, NetConnectionID FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'"))
                {
                    var adapter = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (adapter != null)
                    {
                        // Try NetConnectionID first (friendly name)
                        var netConnectionId = adapter["NetConnectionID"]?.ToString();
                        if (!string.IsNullOrEmpty(netConnectionId))
                            return netConnectionId;
                        
                        // Fall back to Name
                        return adapter["Name"]?.ToString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _debugMonitor.LogEvent($"Error getting adapter name: {ex.Message}", EventType.Warning);
            }
            
            return string.Empty;
        }

        // Keep the original GetNetworkAdaptersAsync and other methods from original service
        public async Task<List<NetworkAdapterInfo>> GetNetworkAdaptersAsync()
        {
            return await Task.Run(() =>
            {
                var adapters = new List<NetworkAdapterInfo>();

                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True");
                    
                    var adapterObjects = searcher.Get();

                    foreach (ManagementObject adapter in adapterObjects)
                    {
                        var adapterInfo = new NetworkAdapterInfo
                        {
                            Name = adapter["Name"]?.ToString() ?? "Unknown",
                            Description = adapter["Description"]?.ToString() ?? "Unknown",
                            DeviceId = adapter["DeviceID"]?.ToString() ?? "Unknown",
                            MacAddress = adapter["MACAddress"]?.ToString() ?? "N/A",
                            AdapterType = adapter["AdapterType"]?.ToString() ?? "Unknown",
                            Speed = Convert.ToDouble(adapter["Speed"] ?? 0),
                            NetConnectionStatus = Convert.ToInt32(adapter["NetConnectionStatus"] ?? 0)
                        };

                        adapterInfo.IsEnabled = adapterInfo.NetConnectionStatus == 2;
                        adapterInfo.Status = GetConnectionStatus(adapterInfo.NetConnectionStatus);

                        // Get IP configuration
                        var configSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Index = {adapter["Index"]}");
                        
                        var configs = configSearcher.Get();
                        foreach (ManagementObject config in configs)
                        {
                            adapterInfo.IsDhcpEnabled = Convert.ToBoolean(config["DHCPEnabled"] ?? false);
                            
                            var ipAddresses = config["IPAddress"] as string[];
                            if (ipAddresses != null && ipAddresses.Length > 0)
                            {
                                adapterInfo.IpAddress = ipAddresses[0];
                            }

                            var subnetMasks = config["IPSubnet"] as string[];
                            if (subnetMasks != null && subnetMasks.Length > 0)
                            {
                                adapterInfo.SubnetMask = subnetMasks[0];
                            }

                            var gateways = config["DefaultIPGateway"] as string[];
                            if (gateways != null && gateways.Length > 0)
                            {
                                adapterInfo.DefaultGateway = gateways[0];
                            }

                            var dnsServers = config["DNSServerSearchOrder"] as string[];
                            if (dnsServers != null && dnsServers.Length > 0)
                            {
                                adapterInfo.DnsServers = string.Join(", ", dnsServers);
                            }
                        }

                        UpdateNetworkStatistics(adapterInfo);
                        adapters.Add(adapterInfo);
                    }
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> { ["Operation"] = "GetNetworkAdapters" });
                    StatusChanged?.Invoke(this, $"Error getting adapters: {ex.Message}");
                }

                return adapters;
            });
        }

        private void UpdateNetworkStatistics(NetworkAdapterInfo adapter)
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                var netInterface = networkInterfaces.FirstOrDefault(
                    ni => ni.Description == adapter.Description || ni.Name == adapter.Name);

                if (netInterface != null)
                {
                    var stats = netInterface.GetIPv4Statistics();
                    adapter.BytesReceived = stats.BytesReceived;
                    adapter.BytesSent = stats.BytesSent;
                }
            }
            catch (Exception ex) 
            {
                _debugMonitor.LogEvent($"Failed to update network statistics for {adapter.Name}", EventType.Warning);
            }
        }

        private string GetConnectionStatus(int status)
        {
            return status switch
            {
                0 => "Disconnected",
                1 => "Connecting",
                2 => "Connected",
                3 => "Disconnecting",
                4 => "Hardware not present",
                5 => "Hardware disabled",
                6 => "Hardware malfunction",
                7 => "Media disconnected",
                8 => "Authenticating",
                9 => "Authentication succeeded",
                10 => "Authentication failed",
                11 => "Invalid address",
                12 => "Credentials required",
                _ => "Unknown"
            };
        }
        
        public async Task<bool> EnableAdapterAsync(string deviceId)
        {
            return await ToggleAdapterAsync(deviceId, true);
        }

        public async Task<bool> DisableAdapterAsync(string deviceId)
        {
            return await ToggleAdapterAsync(deviceId, false);
        }

        public async Task<bool> SetStaticIpAsync(string deviceId, string ipAddress, string subnetMask, 
            string gateway, string primaryDns, string secondaryDns)
        {
            var stopwatch = Stopwatch.StartNew();
            _debugMonitor.LogEvent("Setting static IP configuration", EventType.Info, new Dictionary<string, object>
            {
                ["DeviceId"] = deviceId,
                ["IP"] = ipAddress,
                ["Subnet"] = subnetMask,
                ["Gateway"] = gateway,
                ["PrimaryDns"] = primaryDns,
                ["SecondaryDns"] = secondaryDns
            });
            
            return await Task.Run(() =>
            {
                try
                {
                    // First find the adapter by DeviceID
                    using var adapterSearcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'");
                    
                    var adapters = adapterSearcher.Get();
                    foreach (ManagementObject adapter in adapters)
                    {
                        var index = adapter["Index"]?.ToString();
                        if (string.IsNullOrEmpty(index)) continue;
                        
                        _debugMonitor.LogEvent($"Found adapter with index: {index}", EventType.Info);
                        
                        // Now get the configuration for this adapter (don't filter by IPEnabled)
                        using var configSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Index = {index}");
                        
                        var configs = configSearcher.Get();
                        foreach (ManagementObject config in configs)
                        {
                            _debugMonitor.LogEvent("Found configuration, attempting to set static IP", EventType.Info);

                            try
                            {
                                // Set static IP and subnet mask
                                var ipAddresses = new[] { ipAddress };
                                var subnetMasks = new[] { subnetMask };
                                
                                var inParams = config.GetMethodParameters("EnableStatic");
                                inParams["IPAddress"] = ipAddresses;
                                inParams["SubnetMask"] = subnetMasks;
                                
                                var result = config.InvokeMethod("EnableStatic", inParams, null);
                                var returnValue = Convert.ToUInt32(result["ReturnValue"]);
                                
                                _debugMonitor.LogEvent($"EnableStatic returned: {returnValue}", 
                                    returnValue == 0 ? EventType.Info : EventType.Error);
                                
                                if (returnValue != 0)
                                {
                                    StatusChanged?.Invoke(this, $"Failed to set static IP. Error code: {returnValue}");
                                    return false;
                                }
                                
                                // Set gateway if provided
                                if (!string.IsNullOrWhiteSpace(gateway))
                                {
                                    var gateways = new[] { gateway };
                                    var gatewayMetrics = new[] { (ushort)1 };
                                    
                                    inParams = config.GetMethodParameters("SetGateways");
                                    inParams["DefaultIPGateway"] = gateways;
                                    inParams["GatewayCostMetric"] = gatewayMetrics;
                                    
                                    result = config.InvokeMethod("SetGateways", inParams, null);
                                    returnValue = Convert.ToUInt32(result["ReturnValue"]);
                                    
                                    _debugMonitor.LogEvent($"SetGateways returned: {returnValue}", 
                                        returnValue == 0 ? EventType.Info : EventType.Warning);
                                }
                                
                                // Set DNS servers if provided
                                if (!string.IsNullOrWhiteSpace(primaryDns))
                                {
                                    var dnsServers = string.IsNullOrWhiteSpace(secondaryDns) 
                                        ? new[] { primaryDns } 
                                        : new[] { primaryDns, secondaryDns };
                                    
                                    inParams = config.GetMethodParameters("SetDNSServerSearchOrder");
                                    inParams["DNSServerSearchOrder"] = dnsServers;
                                    
                                    result = config.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                                    returnValue = Convert.ToUInt32(result["ReturnValue"]);
                                    
                                    _debugMonitor.LogEvent($"SetDNSServerSearchOrder returned: {returnValue}", 
                                        returnValue == 0 ? EventType.Info : EventType.Warning);
                                }
                                
                                StatusChanged?.Invoke(this, "Static IP configured successfully");
                                _debugMonitor.LogNetworkOperation("SetStaticIP", deviceId, true, stopwatch.Elapsed);
                                AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                                return true;
                            }
                            catch (Exception ex)
                            {
                                _debugMonitor.LogEvent($"Error in WMI method: {ex.Message}", EventType.Error);
                                StatusChanged?.Invoke(this, $"Error setting static IP: {ex.Message}");
                                return false;
                            }
                        }
                    }
                    
                    // If we reach here, no adapter was found
                    _debugMonitor.LogEvent($"No adapter found with DeviceID: {deviceId}", EventType.Error);
                    StatusChanged?.Invoke(this, "Network adapter not found");
                    return false;
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Operation"] = "SetStaticIP",
                        ["DeviceId"] = deviceId,
                        ["IP"] = ipAddress
                    });
                    StatusChanged?.Invoke(this, $"Error setting static IP: {ex.Message}");
                }
                return false;
            });
        }

        public async Task<bool> EnableDhcpAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _debugMonitor.LogEvent($"Enabling DHCP for device: {deviceId}", EventType.Info);
                    
                    // First find the adapter by DeviceID
                    using var adapterSearcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'");
                    
                    var adapters = adapterSearcher.Get();
                    foreach (ManagementObject adapter in adapters)
                    {
                        var index = adapter["Index"]?.ToString();
                        if (string.IsNullOrEmpty(index)) continue;
                        
                        _debugMonitor.LogEvent($"Found adapter with index: {index}", EventType.Info);
                        
                        // Get configuration for this adapter
                        using var configSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Index = {index}");
                        
                        var configs = configSearcher.Get();
                        foreach (ManagementObject config in configs)
                        {
                            _debugMonitor.LogEvent("Found configuration, attempting to enable DHCP", EventType.Info);
                            
                            var result = config.InvokeMethod("EnableDHCP", null);
                            var returnValue = Convert.ToUInt32(result);
                            
                            _debugMonitor.LogEvent($"EnableDHCP returned: {returnValue}", 
                                returnValue == 0 ? EventType.Info : EventType.Error);
                            
                            if (returnValue == 0)
                            {
                                // Also reset DNS to DHCP
                                config.InvokeMethod("SetDNSServerSearchOrder", null);
                                StatusChanged?.Invoke(this, "DHCP enabled successfully");
                                AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                                return true;
                            }
                            
                            StatusChanged?.Invoke(this, $"Failed to enable DHCP. Error code: {returnValue}");
                            return false;
                        }
                    }
                    
                    _debugMonitor.LogEvent($"No adapter found with DeviceID: {deviceId}", EventType.Error);
                    StatusChanged?.Invoke(this, "Network adapter not found");
                    return false;
                }
                catch (Exception ex)
                {
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Operation"] = "EnableDHCP",
                        ["DeviceId"] = deviceId 
                    });
                    StatusChanged?.Invoke(this, $"Error enabling DHCP: {ex.Message}");
                }
                return false;
            });
        }

        public async Task<bool> ResetAdapterAsync(string deviceId)
        {
            var disabled = await DisableAdapterAsync(deviceId);
            if (disabled)
            {
                await Task.Delay(2000);
                return await EnableAdapterAsync(deviceId);
            }
            return false;
        }

        public async Task<bool> RenewIpAddressAsync(string adapterName)
        {
            return await RunNetshCommandAsync($"interface ip set address \"{adapterName}\" dhcp");
        }

        public async Task<bool> FlushDnsAsync()
        {
            return await RunNetshCommandAsync("interface ip delete dnscache");
        }

        private async Task<bool> RunNetshCommandAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = arguments,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit(5000);
                    
                    var success = process.ExitCode == 0;
                    StatusChanged?.Invoke(this, success ? "Command executed successfully" : "Command failed");
                    
                    if (success) AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                    return success;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Error running command: {ex.Message}");
                    _debugMonitor.LogException(ex, new Dictionary<string, object> 
                    { 
                        ["Operation"] = "RunNetshCommand",
                        ["Arguments"] = arguments 
                    });
                    return false;
                }
            });
        }
    }
}