using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinForms = System.Windows.Forms;
using ObsLiveNowIndicator.Models;

namespace ObsLiveNowIndicator.Windows;

public partial class OverlayWindow : Window
{
    // Windows API constants and imports for click-through
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private Storyboard? _pulseAnimation;
    private double _baseOpacity = 1.0;
    private int _iconSize = 80;
    private IconPosition _iconPosition = IconPosition.TopRight;
    private WinForms.Screen? _currentScreen;
    private bool _pulseEnabled = true;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Make window truly click-through using Windows API
        SetClickThrough();
        
        // Initialize pulse animation reference
        _pulseAnimation = (Storyboard)Resources["PulseAnimation"];
        
        // Start animation only if enabled
        if (_pulseEnabled)
        {
            _pulseAnimation?.Begin();
        }
    }
    
    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }
    
    public void SetPulseEnabled(bool enabled)
    {
        _pulseEnabled = enabled;
        
        if (enabled)
        {
            _pulseAnimation?.Begin();
        }
        else
        {
            _pulseAnimation?.Stop();
            // Reset to base opacity and normal scale when animation stops
            LiveIcon.Opacity = 1.0;
            ScaleTransform.ScaleX = 1.0;
            ScaleTransform.ScaleY = 1.0;
        }
    }

    public void SetAppearance(AppearanceType type)
    {
        Geometry geometry = type switch
        {
            AppearanceType.Circle => (Geometry)Resources["CircleGeometry"],
            AppearanceType.Square => (Geometry)Resources["SquareGeometry"],
            _ => (Geometry)Resources["StarGeometry"]
        };

        LiveIcon.Data = geometry;
    }
    
    public void SetSize(int size)
    {
        _iconSize = size;
        Width = size;
        Height = size;
        
        // Reposition if already placed on a screen
        if (_currentScreen != null)
        {
            PositionOnScreen(_currentScreen, _iconPosition);
        }
    }
    
    public void SetOpacity(double opacity)
    {
        // Store the base opacity (0-1 range)
        _baseOpacity = opacity;
        // Set opacity on the container which gets animated
        // The animation will pulse between 0.7 and 1.0 of this base value
        OpacityContainer.Opacity = _baseOpacity;
    }

    public void PositionOnScreen(WinForms.Screen screen, IconPosition position = IconPosition.TopRight)
    {
        _currentScreen = screen;
        _iconPosition = position;
        
        int margin = 20;
        
        // Use screen working area bounds directly (already in screen coordinates)
        var bounds = screen.Bounds;

        // Calculate position based on IconPosition
        switch (position)
        {
            case IconPosition.TopLeft:
                Left = bounds.Left + margin;
                Top = bounds.Top + margin;
                break;
                
            case IconPosition.TopRight:
                Left = bounds.Right - _iconSize - margin;
                Top = bounds.Top + margin;
                break;
                
            case IconPosition.TopCenter:
                Left = bounds.Left + (bounds.Width - _iconSize) / 2;
                Top = bounds.Top + margin;
                break;
                
            case IconPosition.BottomLeft:
                Left = bounds.Left + margin;
                Top = bounds.Bottom - _iconSize - margin;
                break;
                
            case IconPosition.BottomRight:
                Left = bounds.Right - _iconSize - margin;
                Top = bounds.Bottom - _iconSize - margin;
                break;
                
            case IconPosition.BottomCenter:
                Left = bounds.Left + (bounds.Width - _iconSize) / 2;
                Top = bounds.Bottom - _iconSize - margin;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _pulseAnimation?.Stop();
        base.OnClosed(e);
    }
}
