using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NA_ManagerShortcut.Models;
using Newtonsoft.Json;

namespace NA_ManagerShortcut.Services
{
    public class ProfileManager
    {
        private readonly string _profilesDirectory;
        private readonly string _profilesFile;
        private List<NetworkProfile> _profiles = new();

        public event EventHandler? ProfilesChanged;

        public ProfileManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _profilesDirectory = Path.Combine(appData, "NA_ManagerShortcut");
            _profilesFile = Path.Combine(_profilesDirectory, "profiles.json");
            
            Directory.CreateDirectory(_profilesDirectory);
            LoadProfiles();
        }

        public List<NetworkProfile> GetProfiles() => _profiles.ToList();

        public NetworkProfile? GetProfile(string id)
        {
            return _profiles.FirstOrDefault(p => p.Id == id);
        }

        public async Task<bool> SaveProfileAsync(NetworkProfile profile)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var existing = _profiles.FirstOrDefault(p => p.Id == profile.Id);
                    if (existing != null)
                    {
                        existing.Name = profile.Name;
                        existing.Description = profile.Description;
                        existing.AdapterConfigurations = profile.AdapterConfigurations;
                        existing.LastModified = DateTime.Now;
                    }
                    else
                    {
                        _profiles.Add(profile);
                    }

                    SaveProfiles();
                    ProfilesChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> DeleteProfileAsync(string id)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var profile = _profiles.FirstOrDefault(p => p.Id == id);
                    if (profile != null)
                    {
                        _profiles.Remove(profile);
                        SaveProfiles();
                        ProfilesChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> ApplyProfileAsync(string profileId, NetworkAdapterServiceFixed adapterService)
        {
            var profile = GetProfile(profileId);
            if (profile == null) return false;

            var success = true;
            foreach (var config in profile.AdapterConfigurations)
            {
                if (config.UseDhcp)
                {
                    success &= await adapterService.EnableDhcpAsync(config.AdapterDeviceId);
                }
                else
                {
                    success &= await adapterService.SetStaticIpAsync(
                        config.AdapterDeviceId,
                        config.IpAddress,
                        config.SubnetMask,
                        config.DefaultGateway,
                        config.PreferredDns,
                        config.AlternateDns);
                }
            }

            return success;
        }

        public async Task<bool> ExportProfileAsync(string profileId, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var profile = GetProfile(profileId);
                    if (profile == null) return false;

                    var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<NetworkProfile?> ImportProfileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var profile = JsonConvert.DeserializeObject<NetworkProfile>(json);
                    
                    if (profile != null)
                    {
                        profile.Id = Guid.NewGuid().ToString();
                        profile.CreatedDate = DateTime.Now;
                        profile.LastModified = DateTime.Now;
                        _profiles.Add(profile);
                        SaveProfiles();
                        ProfilesChanged?.Invoke(this, EventArgs.Empty);
                    }
                    
                    return profile;
                }
                catch
                {
                    return null;
                }
            });
        }

        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(_profilesFile))
                {
                    var json = File.ReadAllText(_profilesFile);
                    _profiles = JsonConvert.DeserializeObject<List<NetworkProfile>>(json) ?? new List<NetworkProfile>();
                }
            }
            catch
            {
                _profiles = new List<NetworkProfile>();
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
                File.WriteAllText(_profilesFile, json);
            }
            catch { }
        }
    }
}