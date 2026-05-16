using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Manages persistence of NetworkProfile configurations.
    /// Direct analog of SillyRabbitMQ's ProfileManager.
    /// No passwords to encrypt for UDP, but keeps the DPAPI infrastructure
    /// for any future auth-enabled scenarios.
    /// </summary>
    public class ProfileManager
    {
        private readonly string _profileDirectory;
        private readonly string _profileFilePath;

        public ProfileManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _profileDirectory = Path.Combine(appData, "SillyDis");
            _profileFilePath  = Path.Combine(_profileDirectory, "profiles.json");
        }

        public List<NetworkProfile> LoadProfiles()
        {
            if (!File.Exists(_profileFilePath))
                return new List<NetworkProfile>();

            try
            {
                var json = File.ReadAllText(_profileFilePath);
                return JsonConvert.DeserializeObject<List<NetworkProfile>>(json)
                       ?? new List<NetworkProfile>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profiles: {ex.Message}");
                return new List<NetworkProfile>();
            }
        }

        public void SaveProfiles(List<NetworkProfile> profiles)
        {
            if (!Directory.Exists(_profileDirectory))
                Directory.CreateDirectory(_profileDirectory);

            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            File.WriteAllText(_profileFilePath, json);
        }
    }
}
