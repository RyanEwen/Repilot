using Repilot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Repilot.Pages;

public sealed partial class AboutPage : Page
{
    private UpdateService.UpdateCheckResult? _update;

    public AboutPage()
    {
        InitializeComponent();

        var v = typeof(AboutPage).Assembly.GetName().Version!;
        VersionText.Text = $"Version {v.Major}.{v.Minor}.{v.Build}";
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        // A Store copy is updated by Windows, so a GitHub release link would be wrong advice.
        // It is also the one moment Repilot blocks its own update: an MSIX package cannot
        // install while any of its processes run, and this window is open by definition when
        // someone asks about updates.
        if (UpdateService.IsPackaged)
        {
            UpdateStatusText.Text =
                "The Microsoft Store keeps this up to date, but an update cannot install while "
                + "Repilot is open. Restart to apply anything already downloaded.";
            CheckUpdateButton.Content = "Restart Now";
            CheckUpdateButton.Click -= CheckForUpdates_Click;
            CheckUpdateButton.Click += RestartForUpdate_Click;
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "Checking...";
        UpdateStatusText.Text = "Checking for updates...";

        var result = await UpdateService.CheckForUpdateAsync();
        if (result == null)
        {
            UpdateStatusText.Text = "Unable to check for updates. Check your internet connection.";
        }
        else if (result.UpdateAvailable)
        {
            _update = result;
            UpdateStatusText.Text = $"Version {result.LatestVersion} is available (you have {result.CurrentVersion}).";
            CheckUpdateButton.Content = "View Release";
            CheckUpdateButton.Click -= CheckForUpdates_Click;
            CheckUpdateButton.Click += ViewRelease_Click;
            CheckUpdateButton.IsEnabled = true;
            return;
        }
        else
        {
            UpdateStatusText.Text = $"You're up to date ({result.CurrentVersion}).";
        }

        CheckUpdateButton.Content = "Check for Updates";
        CheckUpdateButton.IsEnabled = true;
    }

    private void RestartForUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "Restarting...";
        CheckUpdateButton.IsEnabled = false;
        UpdateService.RestartToApplyUpdates();
    }

    private void ViewRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_update?.ReleaseUrl is { Length: > 0 } url)
            UpdateService.OpenUrl(url);
    }
}
