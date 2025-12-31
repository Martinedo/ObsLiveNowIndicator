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
    
    // Windows API for per-monitor DPI
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);
    
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

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
        
        // Get DPI scale factor for the specific monitor
        double scaleX = 1.0;
        double scaleY = 1.0;
        
        try
        {
            // Get monitor handle for this screen
            var point = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            IntPtr monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            
            // Get DPI for this specific monitor
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
            {
                scaleX = dpiX / 96.0;
                scaleY = dpiY / 96.0;
                System.Diagnostics.Debug.WriteLine($"Screen at {screen.Bounds.Left},{screen.Bounds.Top}: DPI={dpiX}x{dpiY}, Scale={scaleX}x{scaleY}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Screen at {screen.Bounds.Left},{screen.Bounds.Top}: GetDpiForMonitor failed, using scale 1.0");
            }
        }
        catch (Exception ex)
        {
            // Fallback to window's DPI if API call fails
            var dpi = VisualTreeHelper.GetDpi(this);
            scaleX = dpi.DpiScaleX;
            scaleY = dpi.DpiScaleY;
            System.Diagnostics.Debug.WriteLine($"Screen at {screen.Bounds.Left},{screen.Bounds.Top}: DPI API failed ({ex.Message}), using window DPI scale {scaleX}x{scaleY}");
        }
        
        // Screen.Bounds returns physical pixels, but WPF uses DIPs (device-independent pixels)
        // Convert physical pixels to DIPs by dividing by scale factor
        var bounds = screen.Bounds;
        double left = bounds.Left / scaleX;
        double top = bounds.Top / scaleY;
        double width = bounds.Width / scaleX;
        double height = bounds.Height / scaleY;
        double right = left + width;
        double bottom = top + height;
        
        System.Diagnostics.Debug.WriteLine($"Screen DIPs: Left={left}, Top={top}, Width={width}, Height={height}");

        // Calculate position based on IconPosition (all in DIPs now)
        switch (position)
        {
            case IconPosition.TopLeft:
                Left = left + margin;
                Top = top + margin;
                break;
                
            case IconPosition.TopRight:
                Left = right - _iconSize - margin;
                Top = top + margin;
                break;
                
            case IconPosition.TopCenter:
                Left = left + (width - _iconSize) / 2;
                Top = top + margin;
                break;
                
            case IconPosition.BottomLeft:
                Left = left + margin;
                Top = bottom - _iconSize - margin;
                break;
                
            case IconPosition.BottomRight:
                Left = right - _iconSize - margin;
                Top = bottom - _iconSize - margin;
                break;
                
            case IconPosition.BottomCenter:
                Left = left + (width - _iconSize) / 2;
                Top = bottom - _iconSize - margin;
                break;
        }
        
        System.Diagnostics.Debug.WriteLine($"Overlay positioned at Left={Left}, Top={Top} for position {position}");
    }

    protected override void OnClosed(EventArgs e)
    {
        _pulseAnimation?.Stop();
        base.OnClosed(e);
    }
}
