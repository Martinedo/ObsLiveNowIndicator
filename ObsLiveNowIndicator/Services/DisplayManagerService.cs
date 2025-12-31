using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinForms = System.Windows.Forms;
using Microsoft.Win32;
using ObsLiveNowIndicator.Models;
using ObsLiveNowIndicator.Windows;

namespace ObsLiveNowIndicator.Services;

public class DisplayManagerService : IDisposable
{
    private readonly List<OverlayWindow> _overlays = new();
    private bool _isShowing;

    public DisplayManagerService()
    {
        // Monitor display configuration changes
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ShowIndicators(AppearanceType appearanceType, int size = 80, double opacity = 0.9, IconPosition position = IconPosition.TopRight, bool enablePulse = true)
    {
        // Ensure we're on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => ShowIndicators(appearanceType, size, opacity, position, enablePulse));
            return;
        }

        if (_isShowing)
            return;

        _isShowing = true;

        // Create overlay for each screen
        var screens = WinForms.Screen.AllScreens;
        foreach (var screen in screens)
        {
            try
            {
                var overlay = new OverlayWindow();
                overlay.SetAppearance(appearanceType);
                overlay.SetSize(size);  // Set size first
                overlay.SetOpacity(opacity);
                overlay.SetPulseEnabled(enablePulse);
                overlay.Show();
                overlay.PositionOnScreen(screen, position);  // Position after size is set
                _overlays.Add(overlay);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating overlay: {ex.Message}");
            }
        }
    }

    public void HideIndicators()
    {
        // Ensure we're on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => HideIndicators());
            return;
        }

        if (!_isShowing)
            return;

        _isShowing = false;

        foreach (var overlay in _overlays)
        {
            try
            {
                overlay.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing overlay: {ex.Message}");
            }
        }
        _overlays.Clear();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Ensure we're on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => OnDisplaySettingsChanged(sender, e));
            return;
        }

        if (!_isShowing)
            return;

        try
        {
            // Reposition overlays when display configuration changes
            var screens = WinForms.Screen.AllScreens;
            
            // Close extra overlays if screen count decreased
            while (_overlays.Count > screens.Length)
            {
                var overlay = _overlays.Last();
                overlay.Close();
                _overlays.Remove(overlay);
            }

            // Add new overlays if screen count increased
            while (_overlays.Count < screens.Length)
            {
                var overlay = new OverlayWindow();
                overlay.Show();
                _overlays.Add(overlay);
            }

            // Reposition all overlays
            for (int i = 0; i < _overlays.Count && i < screens.Length; i++)
            {
                _overlays[i].PositionOnScreen(screens[i]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling display change: {ex.Message}");
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        HideIndicators();
    }
}
