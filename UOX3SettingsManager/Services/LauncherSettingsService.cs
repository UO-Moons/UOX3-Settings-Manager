using System;
using System.IO;
using System.Text.Json;
using UOX3SettingsManager.Models;

namespace UOX3SettingsManager.Services
{
    public class LauncherSettingsService
    {
        public string GetSettingsFolderPath()
        {
            string applicationDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UOX3SettingsManager");

            if (!Directory.Exists(applicationDataFolder))
            {
                Directory.CreateDirectory(applicationDataFolder);
            }

            return applicationDataFolder;
        }

        public string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsFolderPath(), "settings.json");
        }

        public LauncherSettings LoadSettings()
        {
            try
            {
                string settingsFilePath = GetSettingsFilePath();

                if (!File.Exists(settingsFilePath))
                {
                    LauncherSettings defaultSettings = new LauncherSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                string jsonText = File.ReadAllText(settingsFilePath);
                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    LauncherSettings defaultSettings = new LauncherSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                LauncherSettings launcherSettings = JsonSerializer.Deserialize<LauncherSettings>(jsonText);

                if (launcherSettings == null)
                {
                    LauncherSettings defaultSettings = new LauncherSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                return launcherSettings;
            }
            catch
            {
                LauncherSettings defaultSettings = new LauncherSettings();

                try
                {
                    SaveSettings(defaultSettings);
                }
                catch
                {
                }

                return defaultSettings;
            }
        }

        public void SaveSettings(LauncherSettings launcherSettings)
        {
            if (launcherSettings == null)
            {
                launcherSettings = new LauncherSettings();
            }

            string settingsFilePath = GetSettingsFilePath();

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.WriteIndented = true;

            string jsonText = JsonSerializer.Serialize(launcherSettings, jsonSerializerOptions);
            File.WriteAllText(settingsFilePath, jsonText);
        }
    }
}
