using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ObsLiveNowIndicator.Models;
using ObsLiveNowIndicator.Services;
using ObsLiveNowIndicator.Windows;
using Serilog;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using Application = System.Windows.Application;

namespace ObsLiveNowIndicator;

public partial class App : Application
{
    private WinForms.NotifyIcon? _notifyIcon;
    private ObsWebSocketService? _obsService;
    private DisplayManagerService? _displayManager;
    private StartupManagerService? _startupManager;
    private SettingsService? _settingsService;
    private SynchronizationContext? _uiContext;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // Setup Serilog logging - one file per day in logs folder
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logsFolder = Path.Combine(appData, "ObsLiveNowIndicator", "logs");
        Directory.CreateDirectory(logsFolder);
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsFolder, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        Log.Information("Application starting...");
        
        // Capture UI synchronization context
        _uiContext = SynchronizationContext.Current;
        
        // Initialize services
        _settingsService = new SettingsService();
        _startupManager = new StartupManagerService();
        _obsService = new ObsWebSocketService();
        _displayManager = new DisplayManagerService();

        // Setup system tray icon
        SetupTrayIcon();

        // Load settings and apply startup preference
        var settings = _settingsService.LoadSettings();
        _startupManager.SetStartupEnabled(settings.RunOnStartup);
        
        Log.Information("Connecting to OBS at {Url}:{Port}", settings.ObsUrl, settings.ObsPort);

        // Connect display manager to OBS service
        _obsService.StreamingStateChanged += OnStreamingStateChanged;

        // Start OBS monitoring
        await _obsService.ConnectAsync(settings.ObsUrl, settings.ObsPort, settings.ObsPassword);
    }
    
    private void OnStreamingStateChanged(bool isStreaming)
    {
        // Check if application is still running
        var app = System.Windows.Application.Current;
        if (app == null || app.Dispatcher == null)
        {
            return; // Application is shutting down
        }
        
        // Use Dispatcher.BeginInvoke with Background priority to avoid UI freezing
        app.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                try
                {
                    if (isStreaming)
                    {
                        var currentSettings = _settingsService!.LoadSettings();
                        _displayManager!.ShowIndicators(currentSettings.AppearanceType, currentSettings.IconSize, currentSettings.IconOpacity, currentSettings.IconPosition, currentSettings.EnablePulse);
                        Log.Information("Streaming started - showing indicators");
                    }
                    else
                    {
                        _displayManager!.HideIndicators();
                        Log.Information("Streaming stopped - hiding indicators");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling streaming state change");
                }
            }));
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = GetAppIcon(),
            Visible = true,
            Text = "OBS Live Indicator"
        };

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, (s, e) => ShowSettings());
        contextMenu.Items.Add("About", null, (s, e) => ShowAbout());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) => Shutdown());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowSettings();
    }

    private Drawing.Icon GetAppIcon()
    {
        try
        {
            // Try to load from embedded resources first (for single-file apps)
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "ObsLiveNowIndicator.Assets.app.ico";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    return new Drawing.Icon(stream);
                }
            }
            
            // Fallback to file system (for debug/development)
            var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                return new Drawing.Icon(iconPath);
            }
            
            Log.Warning("App icon not found, using default system icon");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading app icon");
        }

        // Return default application icon if custom icon not found
        return Drawing.SystemIcons.Application;
    }

    private void ShowAbout()
    {
        var aboutWindow = new AboutWindow();
        aboutWindow.ShowDialog();
    }

    private void ShowSettings()
    {
        var isStreaming = _obsService?.IsStreaming ?? false;
        var settingsWindow = new SettingsWindow(_settingsService!, _startupManager!, _displayManager!, isStreaming);
        
        // Use local variable to allow unsubscribing after handler fires
        Action<AppSettings>? settingsSavedHandler = null;
        settingsSavedHandler = async (settings) =>
        {
            // Unsubscribe immediately to prevent duplicate invocations
            if (settingsSavedHandler != null)
            {
                settingsWindow.SettingsSaved -= settingsSavedHandler;
            }
            
            // Reconnect to OBS with new settings
            // Keep auto-reconnect enabled so if this fails, it will retry
            try
            {
                Log.Information("Settings saved - reconnecting to OBS");
                await _obsService!.DisconnectAsync(allowAutoReconnect: true);
                
                // Wait a moment for clean disconnect
                await Task.Delay(500);
                
                var connected = await _obsService.ConnectAsync(settings.ObsUrl, settings.ObsPort, settings.ObsPassword);
                if (!connected)
                {
                    Log.Warning("Initial connection failed, auto-reconnect will retry...");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reconnecting to OBS");
            }
            
            // Update startup setting
            _startupManager!.SetStartupEnabled(settings.RunOnStartup);
        };
        
        settingsWindow.SettingsSaved += settingsSavedHandler;
        settingsWindow.ShowDialog();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Log.Information("Application shutting down");
        
        if (_obsService != null)
        {
            // Unsubscribe FIRST to prevent any callbacks during shutdown
            _obsService.StreamingStateChanged -= OnStreamingStateChanged;
            
            // Permanently dispose the OBS service
            _obsService.Dispose();
        }
        
        _displayManager?.Dispose();
        _notifyIcon?.Dispose();
        
        Log.CloseAndFlush();
    }
}
