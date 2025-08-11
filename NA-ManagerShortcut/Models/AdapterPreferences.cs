using System;
using System.Collections.Generic;

namespace NA_ManagerShortcut.Models
{
    public class AdapterPreferences
    {
        public Dictionary<string, AdapterPreference> Preferences { get; set; } = new();
    }

    public class AdapterPreference
    {
        public string DeviceId { get; set; } = string.Empty;
        public string CustomName { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}