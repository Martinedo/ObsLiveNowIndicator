using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ObsLiveNowIndicator.Models;
using ObsLiveNowIndicator.Services;
using OBSWebsocketDotNet;

namespace ObsLiveNowIndicator.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly StartupManagerService _startupManager;
    private readonly DisplayManagerService _displayManager;
    private readonly bool _isStreaming;
    private bool _isTesting;

    public event Action<AppSettings>? SettingsSaved;

    public SettingsWindow(SettingsService settingsService, StartupManagerService startupManager, DisplayManagerService displayManager, bool isStreaming)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _startupManager = startupManager;
        _displayManager = displayManager;
        _isStreaming = isStreaming;
        LoadCurrentSettings();
        
        // Disable test button if currently streaming
        TestButton.IsEnabled = !_isStreaming;
        if (_isStreaming)
        {
            TestButton.Content = "Test Disabled (Streaming)";
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Stop test when closing settings window
        if (_isTesting)
        {
            StopTest();
        }
        base.OnClosing(e);
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.LoadSettings();
        
        UrlTextBox.Text = settings.ObsUrl;
        PortTextBox.Text = settings.ObsPort.ToString();
        PasswordBox.Password = settings.ObsPassword;
        RunOnStartupCheckBox.IsChecked = settings.RunOnStartup;
        SizeSlider.Value = settings.IconSize;
        OpacitySlider.Value = settings.IconOpacity;
        EnablePulseCheckBox.IsChecked = settings.EnablePulse;

        // Set appearance combo box
        switch (settings.AppearanceType)
        {
            case AppearanceType.Star:
                AppearanceComboBox.SelectedIndex = 0;
                break;
            case AppearanceType.Circle:
                AppearanceComboBox.SelectedIndex = 1;
                break;
            case AppearanceType.Square:
                AppearanceComboBox.SelectedIndex = 2;
                break;
        }
        
        // Set position combo box
        switch (settings.IconPosition)
        {
            case IconPosition.TopLeft:
                PositionComboBox.SelectedIndex = 0;
                break;
            case IconPosition.TopRight:
                PositionComboBox.SelectedIndex = 1;
                break;
            case IconPosition.TopCenter:
                PositionComboBox.SelectedIndex = 2;
                break;
            case IconPosition.BottomLeft:
                PositionComboBox.SelectedIndex = 3;
                break;
            case IconPosition.BottomRight:
                PositionComboBox.SelectedIndex = 4;
                break;
            case IconPosition.BottomCenter:
                PositionComboBox.SelectedIndex = 5;
                break;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate port
        if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Please enter a valid port number (1-65535).", "Invalid Port", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate URL
        if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
        {
            MessageBox.Show("Please enter a WebSocket URL.", "Invalid URL", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Get appearance type
        var appearanceType = AppearanceType.Star;
        if (AppearanceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            appearanceType = item.Tag?.ToString() switch
            {
                "Circle" => AppearanceType.Circle,
                "Square" => AppearanceType.Square,
                _ => AppearanceType.Star
            };
        }
        
        // Get icon position
        var iconPosition = IconPosition.TopRight;
        if (PositionComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem posItem)
        {
            iconPosition = posItem.Tag?.ToString() switch
            {
                "TopLeft" => IconPosition.TopLeft,
                "TopRight" => IconPosition.TopRight,
                "TopCenter" => IconPosition.TopCenter,
                "BottomLeft" => IconPosition.BottomLeft,
                "BottomRight" => IconPosition.BottomRight,
                "BottomCenter" => IconPosition.BottomCenter,
                _ => IconPosition.TopRight
            };
        }

        // Create settings object
        var settings = new AppSettings
        {
            ObsUrl = UrlTextBox.Text.Trim(),
            ObsPort = port,
            ObsPassword = PasswordBox.Password,
            AppearanceType = appearanceType,
            RunOnStartup = RunOnStartupCheckBox.IsChecked ?? true,
            IconSize = (int)SizeSlider.Value,
            IconOpacity = OpacitySlider.Value,
            IconPosition = iconPosition,
            EnablePulse = EnablePulseCheckBox.IsChecked ?? true
        };

        // Save settings
        _settingsService.SaveSettings(settings);

        // Notify listeners
        SettingsSaved?.Invoke(settings);
        
        // If currently streaming, update the indicators with new appearance settings
        if (_isStreaming)
        {
            _displayManager.HideIndicators();
            _displayManager.ShowIndicators(appearanceType, (int)SizeSlider.Value, OpacitySlider.Value, iconPosition, EnablePulseCheckBox.IsChecked ?? true);
        }

        MessageBox.Show("Settings saved successfully!", "Success", 
            MessageBoxButton.OK, MessageBoxImage.Information);
        
        // Don't close the window - let user close it manually
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTesting)
        {
            StopTest();
        }
        else
        {
            StartTest();
        }
    }

    private void StartTest()
    {
        if (_isTesting || _isStreaming)
            return;

        try
        {
            // Get selected appearance type
            var appearanceType = AppearanceType.Star;
            if (AppearanceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                appearanceType = item.Tag?.ToString() switch
                {
                    "Circle" => AppearanceType.Circle,
                    "Square" => AppearanceType.Square,
                    _ => AppearanceType.Star
                };
            }
            
            // Get icon position
            var iconPosition = IconPosition.TopRight;
            if (PositionComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem posItem)
            {
                iconPosition = posItem.Tag?.ToString() switch
                {
                    "TopLeft" => IconPosition.TopLeft,
                    "TopRight" => IconPosition.TopRight,
                    "TopCenter" => IconPosition.TopCenter,
                    "BottomLeft" => IconPosition.BottomLeft,
                    "BottomRight" => IconPosition.BottomRight,
                    "BottomCenter" => IconPosition.BottomCenter,
                    _ => IconPosition.TopRight
                };
            }

            // Show indicators with current settings
            _displayManager.ShowIndicators(appearanceType, (int)SizeSlider.Value, OpacitySlider.Value, iconPosition, EnablePulseCheckBox.IsChecked ?? true);
            _isTesting = true;
            TestButton.Content = "Stop Test";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting test: {ex.Message}", "Test Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopTest()
    {
        if (!_isTesting)
            return;

        try
        {
            _displayManager.HideIndicators();
            _isTesting = false;
            TestButton.Content = "Start Test";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping test: {ex.Message}");
        }
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        TestConnectionButton.IsEnabled = false;
        TestConnectionButton.Content = "Testing...";
        ConnectionStatusText.Text = "Connecting to OBS WebSocket...";
        ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Gray);

        try
        {
            // Validate port
            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                ConnectionStatusText.Text = "❌ Invalid port number";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
                return;
            }

            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                ConnectionStatusText.Text = "❌ Invalid URL";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
                return;
            }

            var wsUrl = $"{url}:{port}";
            var password = PasswordBox.Password;

            // Try to connect
            var testObs = new OBSWebsocket();
            var connected = false;
            var errorMessage = "";

            testObs.Connected += (s, args) => connected = true;
            testObs.Disconnected += (s, args) => 
            {
                if (!connected)
                    errorMessage = args.DisconnectReason ?? "Connection failed";
            };

            await Task.Run(() =>
            {
                try
                {
                    testObs.ConnectAsync(wsUrl, password);
                    // Wait for connection
                    System.Threading.Thread.Sleep(3000);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }
            });

            // Wait a bit more for connection callbacks
            await Task.Delay(500);

            if (connected)
            {
                // Get OBS version info
                try
                {
                    var version = testObs.GetVersion();
                    ConnectionStatusText.Text = $"✓ Connected! OBS v{version.OBSStudioVersion}, WebSocket v{version.PluginVersion}";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }
                catch
                {
                    ConnectionStatusText.Text = "✓ Connected to OBS WebSocket!";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }

                // Disconnect test connection
                try { testObs.Disconnect(); } catch { }
            }
            else
            {
                ConnectionStatusText.Text = $"❌ Failed: {(string.IsNullOrEmpty(errorMessage) ? "Connection timeout" : errorMessage)}";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"❌ Error: {ex.Message}";
            ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            TestConnectionButton.Content = "Test Connection";
        }
    }
}
