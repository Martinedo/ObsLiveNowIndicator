using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ObsLiveNowIndicator.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadVersionInfo();
    }

    private void LoadVersionInfo()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch
        {
            VersionText.Text = "Version 1.0.1";
        }
    }

    private void EmailLink_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:martin.gburik@magbit.sk",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore if mail client is not configured
        }
    }

    private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Martinedo/ObsLiveNowIndicator",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore if browser cannot be opened
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
