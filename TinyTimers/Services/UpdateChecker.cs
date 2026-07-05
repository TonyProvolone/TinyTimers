using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TinyTimers.Services;

/// <summary>Looks up the latest GitHub release for the project. Every tag matching v*.*.* is what
/// triggers the release workflow (see .github/workflows/release.yml), so "latest release" and
/// "latest pushed version tag" are the same thing here - checking releases also gets us the
/// installer download URL in the same request.</summary>
internal static class UpdateChecker
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/TonyProvolone/TinyTimers/releases/latest";
    private const string ReleasesHtmlUrl = "https://github.com/TonyProvolone/TinyTimers/releases/latest";

    public sealed record UpdateInfo(Version Version, string TagName, string HtmlUrl, string? InstallerDownloadUrl);

    /// <summary>The version baked into the running executable via -p:Version= at publish time.</summary>
    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>Compares Major.Minor.Build only - a bare tag like "v1.2.3" parses to a Version with
    /// Revision -1, while the published assembly version is always 4-part (e.g. 1.2.3.0), so a naive
    /// Version comparison would treat the identical release as "newer" than itself.</summary>
    public static bool IsNewer(Version candidate)
    {
        var normalizedCandidate = (candidate.Major, candidate.Minor, Math.Max(candidate.Build, 0));
        var normalizedCurrent = (CurrentVersion.Major, CurrentVersion.Minor, Math.Max(CurrentVersion.Build, 0));
        return normalizedCandidate.CompareTo(normalizedCurrent) > 0;
    }

    public static async Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(LatestReleaseApiUrl, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagProp) || tagProp.GetString() is not { } tagName)
            return null;

        if (!Version.TryParse(tagName.TrimStart('v', 'V'), out var version))
            return null;

        var htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? ReleasesHtmlUrl : ReleasesHtmlUrl;
        var downloadUrl = FindInstallerAssetUrl(root);

        return new UpdateInfo(version, tagName, htmlUrl, downloadUrl);
    }

    private static string? FindInstallerAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                continue;

            return asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests with no User-Agent header.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TinyTimers-UpdateChecker");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
