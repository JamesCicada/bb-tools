using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace BBTools;

public sealed record UpdateResult(Version LatestVersion, Uri ReleaseUrl)
{
    public bool IsNewerThan(Version current) => LatestVersion > current;
}

public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/JamesCicada/bb-tools/releases/latest";

    public static async Task<UpdateResult> CheckAsync(HttpClient client, Version current, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd($"BBTools/{current.ToString(3)}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new IOException("No public release is available yet. You can check the GitHub releases page.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new IOException("GitHub limited the update check. Try again later or open GitHub.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseRelease(json.RootElement);
    }

    public static UpdateResult ParseRelease(JsonElement release)
    {
        if (release.ValueKind != JsonValueKind.Object ||
            !release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
            !release.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False ||
            !release.TryGetProperty("tag_name", out var tagValue) || tagValue.ValueKind != JsonValueKind.String)
            throw new IOException("GitHub returned an unsupported release response.");
        var tag = tagValue.GetString()!;
        var numeric = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        if (numeric.Count(c => c == '.') != 2 || numeric.Any(c => c != '.' && (c < '0' || c > '9')) ||
            !Version.TryParse(numeric, out var version))
            throw new IOException("The latest release tag is not a supported stable version.");
        // Build the link from this repository and the validated tag, rather than
        // accepting a response-supplied download URL or executable.
        return new(version, new Uri($"{AppInfo.RepositoryUrl}/releases/tag/{Uri.EscapeDataString(tag)}"));
    }
}
