using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using NA_ManagerShortcut.Services;

namespace NA_ManagerShortcut
{
    public partial class App : Application
    {
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "NA_ManagerShortcut";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Network Adapter Manager is already running.", 
                    "Application Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }
            
            // Initialize debug monitor
            var debugMonitor = DebugMonitor.Instance;
            debugMonitor.StartMonitoring(false);
            
            // Check for administrator privileges
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            debugMonitor.LogEvent($"Application started - Administrator: {isAdmin}", 
                isAdmin ? EventType.Info : EventType.Warning,
                new Dictionary<string, object>
                {
                    ["IsAdministrator"] = isAdmin,
                    ["UserName"] = identity.Name,
                    ["ProcessId"] = System.Diagnostics.Process.GetCurrentProcess().Id
                });
            
            if (!isAdmin)
            {
                debugMonitor.LogEvent("Running without Administrator privileges - Some features will be limited", 
                    EventType.Warning);
                
                var result = MessageBox.Show(
                    "Network Adapter Manager requires Administrator privileges to function properly.\n\n" +
                    "Features that will NOT work without Administrator:\n" +
                    "• Enable/Disable network adapters\n" +
                    "• Change IP configuration\n" +
                    "• Reset network adapters\n\n" +
                    "Do you want to continue anyway?",
                    "Administrator Privileges Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    debugMonitor.LogEvent("Application shutdown - User declined to run without admin", EventType.Info);
                    Current.Shutdown();
                    return;
                }
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
