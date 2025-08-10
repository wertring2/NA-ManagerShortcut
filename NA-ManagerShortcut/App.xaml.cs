using System;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

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
