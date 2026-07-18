using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CursorCycle.Infrastructure;

public sealed class UpdateService
{
    public const string RepositoryUrl = "https://github.com/kedaryu/OshiCursour";
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/kedaryu/OshiCursour/releases/latest";
    private const string PackageAssetName = "OshiCursour-win-x64.zip";
    private const string ChecksumAssetName = "checksums.txt";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            LatestReleaseApiUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
            stream,
            cancellationToken: cancellationToken);

        if (release is null || !TryParseVersion(release.TagName, out var latestVersion))
        {
            return null;
        }

        var currentVersion = typeof(UpdateService).Assembly.GetName().Version
            ?? new Version(0, 0, 0);
        if (latestVersion.CompareTo(NormalizeVersion(currentVersion)) <= 0)
        {
            return null;
        }

        var package = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, PackageAssetName, StringComparison.OrdinalIgnoreCase));
        var checksums = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase));

        if (package is null || checksums is null)
        {
            DiagnosticLogger.Write(
                $"Release {release.TagName} does not contain required update assets.");
            return null;
        }

        return new UpdateInfo(
            latestVersion,
            release.HtmlUrl,
            release.Body ?? string.Empty,
            package.BrowserDownloadUrl,
            checksums.BrowserDownloadUrl);
    }

    public async Task<PreparedUpdate> DownloadAndPrepareAsync(
        UpdateInfo update,
        CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(
            AppPaths.DataDirectory,
            "updates",
            update.Version.ToString(3));
        var downloadPath = Path.Combine(updateRoot, PackageAssetName);
        var stagingDirectory = Path.Combine(updateRoot, "staging");
        Directory.CreateDirectory(updateRoot);

        await DownloadFileAsync(update.PackageUrl, downloadPath, cancellationToken);
        var checksumText = await HttpClient.GetStringAsync(
            update.ChecksumUrl,
            cancellationToken);
        var expectedHash = ParseExpectedHash(checksumText);
        var actualHash = await ComputeSha256Async(downloadPath, cancellationToken);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "更新ファイルのSHA-256が一致しないため、更新を中止しました。");
        }

        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, true);
        }

        Directory.CreateDirectory(stagingDirectory);
        ZipFile.ExtractToDirectory(downloadPath, stagingDirectory, overwriteFiles: true);
        var stagedExecutable = Path.Combine(stagingDirectory, "OshiCursour.exe");
        if (!File.Exists(stagedExecutable))
        {
            throw new InvalidDataException(
                "更新パッケージ内に OshiCursour.exe がありません。");
        }

        return new PreparedUpdate(update.Version, stagedExecutable, updateRoot);
    }

    public void LaunchInstaller(PreparedUpdate update)
    {
        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable) || !File.Exists(currentExecutable))
        {
            throw new InvalidOperationException("現在の実行ファイルを取得できません。");
        }

        var token = Guid.NewGuid().ToString("N");
        var temporaryUpdater = Path.Combine(
            Path.GetTempPath(),
            $"OshiCursour-Updater-{token}.exe");
        var markerPath = Path.Combine(update.UpdateRoot, $"healthy-{token}.marker");
        var backupPath = Path.Combine(update.UpdateRoot, "OshiCursour.previous.exe");
        File.Copy(currentExecutable, temporaryUpdater, overwrite: true);

        var startInfo = new ProcessStartInfo
        {
            FileName = temporaryUpdater,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(currentExecutable)
                ?? Environment.CurrentDirectory
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add("--wait-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(update.StagedExecutable);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(currentExecutable);
        startInfo.ArgumentList.Add("--backup");
        startInfo.ArgumentList.Add(backupPath);
        startInfo.ArgumentList.Add("--marker");
        startInfo.ArgumentList.Add(markerPath);
        startInfo.ArgumentList.Add("--updater");
        startInfo.ArgumentList.Add(temporaryUpdater);

        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("更新プログラムを起動できません。");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("OshiCursour", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return client;
    }

    private static async Task DownloadFileAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string ParseExpectedHash(string checksumText)
    {
        foreach (var line in checksumText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(PackageAssetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
            if (hash.Length == 64 && hash.All(Uri.IsHexDigit))
            {
                return hash;
            }
        }

        throw new InvalidDataException("checksums.txt に更新ファイルのSHA-256がありません。");
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var normalized = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        if (Version.TryParse(normalized, out var parsed))
        {
            version = NormalizeVersion(parsed);
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    private static Version NormalizeVersion(Version version) => new(
        Math.Max(0, version.Major),
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build));

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;
    }
}

public sealed record UpdateInfo(
    Version Version,
    string ReleasePageUrl,
    string ReleaseNotes,
    string PackageUrl,
    string ChecksumUrl);

public sealed record PreparedUpdate(
    Version Version,
    string StagedExecutable,
    string UpdateRoot);
