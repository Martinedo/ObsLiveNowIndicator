using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using Serilog;

namespace ObsLiveNowIndicator.Services;

public class StartupManagerService
{
    private const string AppName = "ObsLiveNowIndicator";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null)
                return;

            if (enabled)
            {
                // Use AppContext.BaseDirectory for single-file apps instead of Assembly.Location
                var exePath = Path.Combine(AppContext.BaseDirectory, "ObsLiveNowIndicator.exe");
                
                // Fallback to Assembly.Location if not a single-file app
                if (!File.Exists(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                    if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        exePath = Path.ChangeExtension(exePath, ".exe");
                    }
                }

                key.SetValue(AppName, $"\"{exePath}\"");
                Log.Information("Startup enabled: {Path}", exePath);
            }
            else
            {
                key.DeleteValue(AppName, false);
                Log.Information("Startup disabled");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set startup");
        }
    }

    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
