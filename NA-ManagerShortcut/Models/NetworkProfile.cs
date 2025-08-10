using System;
using System.Collections.Generic;

namespace NA_ManagerShortcut.Models
{
    public class NetworkProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<AdapterConfiguration> AdapterConfigurations { get; set; } = new();
    }

    public class AdapterConfiguration
    {
        public string AdapterName { get; set; } = string.Empty;
        public string AdapterDeviceId { get; set; } = string.Empty;
        public bool UseDhcp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string DefaultGateway { get; set; } = string.Empty;
        public string PreferredDns { get; set; } = string.Empty;
        public string AlternateDns { get; set; } = string.Empty;
        public bool AutoMetric { get; set; } = true;
        public int? MetricValue { get; set; }
    }
}