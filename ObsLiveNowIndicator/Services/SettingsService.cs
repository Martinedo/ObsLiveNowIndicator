using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ObsLiveNowIndicator.Models;

namespace ObsLiveNowIndicator.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("ObsLiveNowIndicator_Entropy_2025");

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "ObsLiveNowIndicator");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonConvert.DeserializeObject<SettingsData>(json);
            
            if (settings == null)
                return new AppSettings();

            return new AppSettings
            {
                ObsUrl = settings.ObsUrl,
                ObsPort = settings.ObsPort,
                ObsPassword = DecryptPassword(settings.EncryptedPassword),
                AppearanceType = settings.AppearanceType,
                RunOnStartup = settings.RunOnStartup,
                IconSize = settings.IconSize,
                IconOpacity = settings.IconOpacity,
                IconPosition = settings.IconPosition,
                EnablePulse = settings.EnablePulse
            };
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        var settingsData = new SettingsData
        {
            ObsUrl = settings.ObsUrl,
            ObsPort = settings.ObsPort,
            EncryptedPassword = EncryptPassword(settings.ObsPassword),
            AppearanceType = settings.AppearanceType,
            RunOnStartup = settings.RunOnStartup,
            IconSize = settings.IconSize,
            IconOpacity = settings.IconOpacity,
            IconPosition = settings.IconPosition,
            EnablePulse = settings.EnablePulse
        };

        var json = JsonConvert.SerializeObject(settingsData, Formatting.Indented);
        File.WriteAllText(_settingsPath, json);
    }

    private string EncryptPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        try
        {
            var data = Encoding.UTF8.GetBytes(password);
            var encrypted = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            return string.Empty;

        try
        {
            var encrypted = Convert.FromBase64String(encryptedPassword);
            var data = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return string.Empty;
        }
    }

    private class SettingsData
    {
        public string ObsUrl { get; set; } = "";
        public int ObsPort { get; set; }
        public string EncryptedPassword { get; set; } = "";
        public AppearanceType AppearanceType { get; set; }
        public bool RunOnStartup { get; set; }
        public int IconSize { get; set; } = 80;
        public double IconOpacity { get; set; } = 0.9;
        public IconPosition IconPosition { get; set; } = IconPosition.TopRight;
        public bool EnablePulse { get; set; } = true;
    }
}
