using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using NA_ManagerShortcut.Models;

namespace NA_ManagerShortcut.Services
{
    public class NetworkAdapterService
    {
        public event EventHandler<string>? StatusChanged;
        public event EventHandler? AdaptersUpdated;

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
            catch { }
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

        private async Task<bool> ToggleAdapterAsync(string deviceId, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId}'");
                    
                    var adapters = searcher.Get();
                    foreach (ManagementObject adapter in adapters)
                    {
                        var result = adapter.InvokeMethod(enable ? "Enable" : "Disable", null);
                        var success = Convert.ToUInt32(result) == 0;
                        
                        StatusChanged?.Invoke(this, success 
                            ? $"Adapter {(enable ? "enabled" : "disabled")} successfully" 
                            : $"Failed to {(enable ? "enable" : "disable")} adapter");
                        
                        if (success) AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                        return success;
                    }
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Error toggling adapter: {ex.Message}");
                }
                return false;
            });
        }

        public async Task<bool> SetStaticIpAsync(string deviceId, string ipAddress, string subnetMask, 
            string gateway, string primaryDns, string secondaryDns)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    
                    var configs = searcher.Get();
                    foreach (ManagementObject config in configs)
                    {
                        var index = config["Index"]?.ToString();
                        var adapterSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapter WHERE Index = {index} AND DeviceID = '{deviceId}'");
                        
                        if (adapterSearcher.Get().Count == 0) continue;

                        var ipAddresses = new[] { ipAddress };
                        var subnetMasks = new[] { subnetMask };
                        var gateways = new[] { gateway };
                        var gatewayMetrics = new[] { (ushort)1 };

                        var inParams = config.GetMethodParameters("EnableStatic");
                        inParams["IPAddress"] = ipAddresses;
                        inParams["SubnetMask"] = subnetMasks;
                        var result = config.InvokeMethod("EnableStatic", inParams, null);

                        if (Convert.ToUInt32(result["ReturnValue"]) == 0)
                        {
                            inParams = config.GetMethodParameters("SetGateways");
                            inParams["DefaultIPGateway"] = gateways;
                            inParams["GatewayCostMetric"] = gatewayMetrics;
                            result = config.InvokeMethod("SetGateways", inParams, null);

                            if (Convert.ToUInt32(result["ReturnValue"]) == 0)
                            {
                                var dnsServers = string.IsNullOrEmpty(secondaryDns) 
                                    ? new[] { primaryDns } 
                                    : new[] { primaryDns, secondaryDns };
                                
                                inParams = config.GetMethodParameters("SetDNSServerSearchOrder");
                                inParams["DNSServerSearchOrder"] = dnsServers;
                                result = config.InvokeMethod("SetDNSServerSearchOrder", inParams, null);

                                var success = Convert.ToUInt32(result["ReturnValue"]) == 0;
                                StatusChanged?.Invoke(this, success 
                                    ? "Static IP configured successfully" 
                                    : "Failed to set DNS servers");
                                
                                if (success) AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                                return success;
                            }
                        }

                        StatusChanged?.Invoke(this, "Failed to set static IP");
                        return false;
                    }
                }
                catch (Exception ex)
                {
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
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    
                    var configs = searcher.Get();
                    foreach (ManagementObject config in configs)
                    {
                        var index = config["Index"]?.ToString();
                        var adapterSearcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_NetworkAdapter WHERE Index = {index} AND DeviceID = '{deviceId}'");
                        
                        if (adapterSearcher.Get().Count == 0) continue;

                        var result = config.InvokeMethod("EnableDHCP", null);
                        if (Convert.ToUInt32(result) == 0)
                        {
                            config.InvokeMethod("SetDNSServerSearchOrder", null);
                            StatusChanged?.Invoke(this, "DHCP enabled successfully");
                            AdaptersUpdated?.Invoke(this, EventArgs.Empty);
                            return true;
                        }

                        StatusChanged?.Invoke(this, "Failed to enable DHCP");
                        return false;
                    }
                }
                catch (Exception ex)
                {
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
                    return false;
                }
            });
        }
    }
}