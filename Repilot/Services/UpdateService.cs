using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Repilot.Services;

/// <summary>
/// Manual update check against this repo's GitHub Releases. Runs only when the user
/// clicks "Check for updates" — there is no automatic/background network activity.
/// (Store-installed copies also update automatically through the Microsoft Store.)
/// </summary>
public static class UpdateService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private const string Owner = "RyanEwen";
    private const string Repo = "Repilot";
    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"Repilot/{CurrentVersion()}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; init; }
        public string CurrentVersion { get; init; } = "";
        public string LatestVersion { get; init; } = "";
        public string? ReleaseUrl { get; init; }
    }

    public static string CurrentVersion()
    {
        var v = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>Returns null only on a network/parse error; an up-to-date result when there are no newer releases.</summary>
    public static async Task<UpdateCheckResult?> CheckForUpdateAsync()
    {
        try
        {
            using var resp = await Http.GetAsync(LatestReleaseUri);
            // No releases published yet → you already have the latest.
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult { CurrentVersion = CurrentVersion(), LatestVersion = CurrentVersion() };

            resp.EnsureSuccessStatusCode();
            var release = await resp.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubRelease);
            if (release == null || string.IsNullOrWhiteSpace(release.TagName)) return null;

            var current = ParseVersion(CurrentVersion());
            var latest = ParseVersion(release.TagName);
            if (current == null || latest == null) return null;

            return new UpdateCheckResult
            {
                UpdateAvailable = latest > current,
                CurrentVersion = current.ToString(3),
                LatestVersion = latest.ToString(3),
                ReleaseUrl = release.HtmlUrl,
            };
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>
    /// True when running from an MSIX package, so the Microsoft Store owns updating.
    /// </summary>
    /// <remarks>
    /// <c>Package.Current</c> throws rather than returning null when the process is unpackaged,
    /// which is the documented way to tell the two apart. Cached because the answer cannot
    /// change while the process lives.
    /// </remarks>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            return global::Windows.ApplicationModel.Package.Current is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Close so Windows can finish installing a Store update, and come back afterwards.
    /// </summary>
    /// <remarks>
    /// <para><b>An MSIX package cannot be installed while any of its processes are running.</b>
    /// Repilot does not sit in the tray, so most of the time the Store updates it quite happily
    /// while it is closed. The exception is the moment that matters: the settings window is by
    /// definition open when someone clicks "Check for updates", so it blocks the very install
    /// they are asking about.</para>
    /// <para>Restarting applies anything staged without needing to detect it, which is just as
    /// well: there is no reliable way to ask. <c>Package.CheckUpdateAvailabilityAsync</c> only
    /// covers .appinstaller installs, not Store-distributed packages.</para>
    /// <para><c>RegisterApplicationRestart</c> has to be called before shutdown begins, not
    /// during it.</para>
    /// </remarks>
    public static void RestartToApplyUpdates()
    {
        RegisterApplicationRestart(null, 0);
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? pwzCommandline, int dwFlags);

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Warn(ex, "Failed to open {Url}", url); }
    }

    private static Version? ParseVersion(string s)
    {
        s = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(s, out var v) ? v : null;
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
internal partial class GitHubJsonContext : JsonSerializerContext { }
