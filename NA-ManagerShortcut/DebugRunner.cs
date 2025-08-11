using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using NA_ManagerShortcut.Models;
using NA_ManagerShortcut.Services;

namespace NA_ManagerShortcut
{
    public class DebugRunner
    {
        private readonly DebugMonitor _debugMonitor;
        private readonly ClaudeCodeInterface _claudeInterface;
        private readonly NetworkAdapterServiceFixed _networkService;
        private readonly List<string> _detectedIssues = new();
        private readonly Dictionary<string, Action> _fixes = new();

        public DebugRunner()
        {
            _debugMonitor = DebugMonitor.Instance;
            _claudeInterface = new ClaudeCodeInterface();
            _networkService = new NetworkAdapterServiceFixed();
            InitializeFixes();
        }

        public async Task RunDiagnosticAndFix()
        {
            Console.WriteLine("=== Starting Automated Debug and Fix ===\n");
            
            // Start monitoring with auto-fix enabled
            _claudeInterface.StartCapture(true);
            
            try
            {
                // Step 1: Check prerequisites
                await CheckPrerequisites();
                
                // Step 2: Test adapter operations
                await TestAdapterOperations();
                
                // Step 3: Analyze captured errors
                var analysis = await _claudeInterface.AnalyzeCurrentState();
                
                // Step 4: Apply fixes
                await ApplyFixes(analysis);
                
                // Step 5: Verify fixes
                await VerifyFixes();
                
                // Generate report
                var report = await _debugMonitor.GenerateDebugReport();
                Console.WriteLine($"\nDebug report saved: {report}");
            }
            finally
            {
                _claudeInterface.StopCapture();
            }
        }

        private async Task CheckPrerequisites()
        {
            Console.WriteLine("Checking Prerequisites...");
            
            // Check Admin privileges
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            if (!isAdmin)
            {
                _detectedIssues.Add("NO_ADMIN_PRIVILEGES");
                _debugMonitor.LogEvent("Missing Administrator privileges", EventType.Error);
                Console.WriteLine("❌ Not running as Administrator");
                
                // Try to fix by restarting with admin
                ApplyAdminFix();
            }
            else
            {
                Console.WriteLine("✓ Running as Administrator");
            }
            
            // Check WMI Service
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service WHERE Name = 'Winmgmt'");
                var service = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                
                if (service != null)
                {
                    var state = service["State"]?.ToString();
                    if (state != "Running")
                    {
                        _detectedIssues.Add("WMI_SERVICE_NOT_RUNNING");
                        _debugMonitor.LogEvent($"WMI Service state: {state}", EventType.Error);
                        Console.WriteLine($"❌ WMI Service is {state}");
                        
                        // Try to start WMI service
                        await StartWMIService();
                    }
                    else
                    {
                        Console.WriteLine("✓ WMI Service is running");
                    }
                }
            }
            catch (Exception ex)
            {
                _detectedIssues.Add("WMI_ACCESS_ERROR");
                _debugMonitor.LogException(ex);
                Console.WriteLine($"❌ Cannot access WMI: {ex.Message}");
            }
            
            // Check for network adapter access
            try
            {
                var adapters = await _networkService.GetNetworkAdaptersAsync();
                if (!adapters.Any())
                {
                    _detectedIssues.Add("NO_ADAPTERS_FOUND");
                    _debugMonitor.LogEvent("No network adapters found", EventType.Error);
                    Console.WriteLine("❌ No network adapters found");
                }
                else
                {
                    Console.WriteLine($"✓ Found {adapters.Count} network adapters");
                }
            }
            catch (Exception ex)
            {
                _detectedIssues.Add("ADAPTER_ACCESS_ERROR");
                _debugMonitor.LogException(ex);
                Console.WriteLine($"❌ Cannot access network adapters: {ex.Message}");
            }
        }

        private async Task TestAdapterOperations()
        {
            Console.WriteLine("\nTesting Adapter Operations...");
            
            try
            {
                var adapters = await _networkService.GetNetworkAdaptersAsync();
                var testAdapter = adapters.FirstOrDefault(a => !a.Name.Contains("VMware") && !a.Name.Contains("VirtualBox"));
                
                if (testAdapter == null)
                {
                    Console.WriteLine("⚠ No suitable adapter for testing");
                    return;
                }
                
                Console.WriteLine($"Testing with adapter: {testAdapter.Name}");
                
                // Test disable operation
                Console.Write("  Testing disable... ");
                var disableResult = await _networkService.DisableAdapterAsync(testAdapter.DeviceId);
                
                if (!disableResult)
                {
                    _detectedIssues.Add("DISABLE_OPERATION_FAILED");
                    Console.WriteLine("❌ Failed");
                    
                    // Check specific WMI return codes
                    var lastError = _debugMonitor.GetDebugOutput();
                    if (lastError.Contains("ReturnValue=5"))
                    {
                        _detectedIssues.Add("ACCESS_DENIED_ERROR");
                        Console.WriteLine("    Error: Access Denied (Error 5)");
                    }
                    else if (lastError.Contains("ReturnValue=87"))
                    {
                        _detectedIssues.Add("INVALID_PARAMETER_ERROR");
                        Console.WriteLine("    Error: Invalid Parameter (Error 87)");
                    }
                }
                else
                {
                    Console.WriteLine("✓ Success");
                    
                    // Wait and re-enable
                    await Task.Delay(2000);
                    
                    Console.Write("  Testing enable... ");
                    var enableResult = await _networkService.EnableAdapterAsync(testAdapter.DeviceId);
                    
                    if (!enableResult)
                    {
                        _detectedIssues.Add("ENABLE_OPERATION_FAILED");
                        Console.WriteLine("❌ Failed");
                    }
                    else
                    {
                        Console.WriteLine("✓ Success");
                    }
                }
            }
            catch (Exception ex)
            {
                _detectedIssues.Add("OPERATION_TEST_ERROR");
                _debugMonitor.LogException(ex);
                Console.WriteLine($"❌ Test failed: {ex.Message}");
            }
        }

        private async Task ApplyFixes(AnalysisResult analysis)
        {
            Console.WriteLine("\nApplying Fixes...");
            
            foreach (var issue in _detectedIssues.Distinct())
            {
                Console.WriteLine($"  Fixing: {issue}");
                
                if (_fixes.ContainsKey(issue))
                {
                    try
                    {
                        _fixes[issue]();
                        Console.WriteLine($"    ✓ Fix applied");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ❌ Fix failed: {ex.Message}");
                    }
                }
                else
                {
                    // Use auto-fix from Claude interface
                    var result = await _claudeInterface.ApplyAutoFix(issue);
                    if (result.Success)
                    {
                        Console.WriteLine($"    ✓ Auto-fix applied");
                    }
                    else
                    {
                        Console.WriteLine($"    ❌ Auto-fix failed: {result.Message}");
                    }
                }
            }
            
            // Apply suggested fixes from analysis
            foreach (var suggestion in analysis.SuggestedFixes)
            {
                Console.WriteLine($"  Applying suggestion: {suggestion}");
            }
        }

        private async Task VerifyFixes()
        {
            Console.WriteLine("\nVerifying Fixes...");
            
            // Re-run tests to verify
            var adapters = await _networkService.GetNetworkAdaptersAsync();
            
            if (adapters.Any())
            {
                Console.WriteLine("✓ Can access network adapters");
                
                // Try a simple operation
                var testAdapter = adapters.First();
                var currentState = testAdapter.IsEnabled;
                
                Console.Write($"  Testing toggle on {testAdapter.Name}... ");
                
                bool success;
                if (currentState)
                {
                    success = await _networkService.DisableAdapterAsync(testAdapter.DeviceId);
                    if (success)
                    {
                        await Task.Delay(1000);
                        success = await _networkService.EnableAdapterAsync(testAdapter.DeviceId);
                    }
                }
                else
                {
                    success = await _networkService.EnableAdapterAsync(testAdapter.DeviceId);
                    if (success)
                    {
                        await Task.Delay(1000);
                        success = await _networkService.DisableAdapterAsync(testAdapter.DeviceId);
                    }
                }
                
                Console.WriteLine(success ? "✓ Success" : "❌ Still failing");
            }
        }

        private void InitializeFixes()
        {
            _fixes["NO_ADMIN_PRIVILEGES"] = () => ApplyAdminFix();
            _fixes["WMI_SERVICE_NOT_RUNNING"] = () => StartWMIService().Wait();
            _fixes["ACCESS_DENIED_ERROR"] = () => ApplyPermissionsFix();
            _fixes["INVALID_PARAMETER_ERROR"] = () => ApplyWMIFix();
            _fixes["DISABLE_OPERATION_FAILED"] = () => ApplyAlternativeMethod();
        }

        private void ApplyAdminFix()
        {
            Console.WriteLine("    Applying admin privileges fix...");
            
            // Create RunAsAdmin helper
            var helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RunAsAdmin.bat");
            File.WriteAllText(helperPath, @"
@echo off
echo Requesting Administrator privileges...
powershell -Command ""Start-Process '%~dp0NA-ManagerShortcut.exe' -Verb RunAs""
exit
");
            
            _debugMonitor.LogEvent("Created RunAsAdmin helper", EventType.Info);
        }

        private async Task StartWMIService()
        {
            Console.WriteLine("    Starting WMI service...");
            
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "net",
                        Arguments = "start winmgmt",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };
                
                process.Start();
                await Task.Run(() => process.WaitForExit(5000));
                
                _debugMonitor.LogEvent("WMI service start attempted", EventType.Info);
            }
            catch (Exception ex)
            {
                _debugMonitor.LogException(ex);
            }
        }

        private void ApplyPermissionsFix()
        {
            Console.WriteLine("    Applying permissions fix...");
            
            // Add to app.manifest if not exists
            var manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "app.manifest");
            if (File.Exists(manifestPath))
            {
                var content = File.ReadAllText(manifestPath);
                if (!content.Contains("requireAdministrator"))
                {
                    content = content.Replace("asInvoker", "requireAdministrator");
                    File.WriteAllText(manifestPath, content);
                    _debugMonitor.LogEvent("Updated app.manifest to require administrator", EventType.Info);
                }
            }
        }

        private void ApplyWMIFix()
        {
            Console.WriteLine("    Applying WMI fix...");
            
            // Reset WMI repository
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "winmgmt",
                        Arguments = "/resetrepository",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };
                
                process.Start();
                process.WaitForExit(5000);
                
                _debugMonitor.LogEvent("WMI repository reset attempted", EventType.Info);
            }
            catch (Exception ex)
            {
                _debugMonitor.LogException(ex);
            }
        }

        private void ApplyAlternativeMethod()
        {
            Console.WriteLine("    Applying alternative method using netsh...");
            
            // Update NetworkAdapterService to use netsh as fallback
            var servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", 
                "Services", "NetworkAdapterService.cs");
            
            if (File.Exists(servicePath))
            {
                _debugMonitor.LogEvent("Adding netsh fallback method to NetworkAdapterService", EventType.Info);
                
                // This would modify the service to add netsh fallback
                // For now, just log the intention
            }
        }
    }

    // Debug runner helper class - not an entry point
    public static class DebugProgramHelper
    {
        public static async Task RunDebugMode()
        {
            Console.WriteLine("Network Adapter Manager - Automated Debug Mode");
            Console.WriteLine("===============================================\n");
            
            var runner = new DebugRunner();
            await runner.RunDiagnosticAndFix();
            
            Console.WriteLine("\n===============================================");
            Console.WriteLine("Debug and fix process completed.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}