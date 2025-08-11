using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NA_ManagerShortcut.Models;

namespace NA_ManagerShortcut.Services
{
    public class AdapterPreferencesService
    {
        private readonly string _preferencesPath;
        private AdapterPreferences _preferences;

        public AdapterPreferencesService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NA-ManagerShortcut"
            );
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _preferencesPath = Path.Combine(appDataPath, "adapter_preferences.json");
            _preferences = LoadPreferences();
        }

        private AdapterPreferences LoadPreferences()
        {
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = File.ReadAllText(_preferencesPath);
                    return JsonConvert.DeserializeObject<AdapterPreferences>(json) ?? new AdapterPreferences();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading adapter preferences: {ex.Message}");
            }
            
            return new AdapterPreferences();
        }

        private async Task SavePreferencesAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_preferences, Formatting.Indented);
                await File.WriteAllTextAsync(_preferencesPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving adapter preferences: {ex.Message}");
            }
        }

        public async Task SetCustomNameAsync(string deviceId, string customName)
        {
            if (!_preferences.Preferences.ContainsKey(deviceId))
            {
                _preferences.Preferences[deviceId] = new AdapterPreference { DeviceId = deviceId };
            }
            
            _preferences.Preferences[deviceId].CustomName = customName;
            _preferences.Preferences[deviceId].LastModified = DateTime.Now;
            
            await SavePreferencesAsync();
        }

        public async Task SetHiddenAsync(string deviceId, bool isHidden)
        {
            if (!_preferences.Preferences.ContainsKey(deviceId))
            {
                _preferences.Preferences[deviceId] = new AdapterPreference { DeviceId = deviceId };
            }
            
            _preferences.Preferences[deviceId].IsHidden = isHidden;
            _preferences.Preferences[deviceId].LastModified = DateTime.Now;
            
            await SavePreferencesAsync();
        }

        public string GetCustomName(string deviceId)
        {
            if (_preferences.Preferences.TryGetValue(deviceId, out var pref))
            {
                return pref.CustomName;
            }
            return string.Empty;
        }

        public bool IsHidden(string deviceId)
        {
            if (_preferences.Preferences.TryGetValue(deviceId, out var pref))
            {
                return pref.IsHidden;
            }
            return false;
        }

        public AdapterPreference? GetPreference(string deviceId)
        {
            return _preferences.Preferences.TryGetValue(deviceId, out var pref) ? pref : null;
        }

        public async Task ClearPreferenceAsync(string deviceId)
        {
            if (_preferences.Preferences.ContainsKey(deviceId))
            {
                _preferences.Preferences.Remove(deviceId);
                await SavePreferencesAsync();
            }
        }
    }
}