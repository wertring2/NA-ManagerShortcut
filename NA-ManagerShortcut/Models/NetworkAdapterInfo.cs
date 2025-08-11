using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NA_ManagerShortcut.Models
{
    public class NetworkAdapterInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _deviceId = string.Empty;
        private bool _isEnabled;
        private string _status = string.Empty;
        private string _ipAddress = string.Empty;
        private string _subnetMask = string.Empty;
        private string _defaultGateway = string.Empty;
        private string _dnsServers = string.Empty;
        private bool _isDhcpEnabled;
        private string _macAddress = string.Empty;
        private long _bytesReceived;
        private long _bytesSent;
        private int _netConnectionStatus;
        private string _adapterType = string.Empty;
        private double _speed;
        private bool _isTransitioning;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        public string SubnetMask
        {
            get => _subnetMask;
            set { _subnetMask = value; OnPropertyChanged(); }
        }

        public string DefaultGateway
        {
            get => _defaultGateway;
            set { _defaultGateway = value; OnPropertyChanged(); }
        }

        public string DnsServers
        {
            get => _dnsServers;
            set { _dnsServers = value; OnPropertyChanged(); }
        }

        public bool IsDhcpEnabled
        {
            get => _isDhcpEnabled;
            set { _isDhcpEnabled = value; OnPropertyChanged(); }
        }

        public string MacAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(); }
        }

        public long BytesReceived
        {
            get => _bytesReceived;
            set { _bytesReceived = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedBytesReceived)); }
        }

        public long BytesSent
        {
            get => _bytesSent;
            set { _bytesSent = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedBytesSent)); }
        }

        public int NetConnectionStatus
        {
            get => _netConnectionStatus;
            set { _netConnectionStatus = value; OnPropertyChanged(); }
        }

        public string AdapterType
        {
            get => _adapterType;
            set { _adapterType = value; OnPropertyChanged(); }
        }

        public double Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedSpeed)); }
        }

        public bool IsTransitioning
        {
            get => _isTransitioning;
            set { _isTransitioning = value; OnPropertyChanged(); }
        }

        private string _customName = string.Empty;
        public string CustomName
        {
            get => _customName;
            set { _customName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private bool _isHidden;
        public bool IsHidden
        {
            get => _isHidden;
            set { _isHidden = value; OnPropertyChanged(); }
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(CustomName) ? CustomName : Name;

        public string FormattedBytesReceived => FormatBytes(BytesReceived);
        public string FormattedBytesSent => FormatBytes(BytesSent);
        public string FormattedSpeed => FormatSpeed(Speed);

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private string FormatSpeed(double speed)
        {
            if (speed <= 0) return "N/A";
            double mbps = speed / 1_000_000;
            return mbps >= 1000 ? $"{mbps / 1000:0.##} Gbps" : $"{mbps:0.##} Mbps";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}