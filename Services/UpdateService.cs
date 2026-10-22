using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NovaPlayer.Models;

namespace NovaPlayer.Services;

public class UpdateService
{
    private const string GitHubRepo = "chrisantropo/NovaPlayer";
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NovaPlayer-Desktop-App/1.0");
    }

    public string CurrentVersion
    {
        get
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        string url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";

        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
        string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
        string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
        string htmlUrl = root.TryGetProperty("html_url", out var htmlElem) ? htmlElem.GetString() ?? "" : "";

        DateTime? publishedAt = null;
        if (root.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTime(out var dt))
        {
            publishedAt = dt;
        }

        string? downloadUrl = null;
        long fileSize = 0;

        if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsElem.EnumerateArray())
            {
                string assetName = asset.TryGetProperty("name", out var aNameElem) ? aNameElem.GetString() ?? "" : "";
                if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var dUrlElem) ? dUrlElem.GetString() : null;
                    if (asset.TryGetProperty("size", out var sElem) && sElem.TryGetInt64(out var s))
                    {
                        fileSize = s;
                    }
                    if (assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
        }

        string cleanRemoteVer = tagName.TrimStart('v', 'V').Trim();
        bool isNewer = CompareVersions(cleanRemoteVer, CurrentVersion) > 0;

        return new UpdateInfo
        {
            Version = cleanRemoteVer,
            TagName = tagName,
            Name = string.IsNullOrWhiteSpace(name) ? tagName : name,
            ReleaseNotes = body,
            HtmlUrl = htmlUrl,
            DownloadUrl = downloadUrl,
            FileSizeBytes = fileSize,
            PublishedAt = publishedAt,
            IsNewer = isNewer
        };
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress, CancellationToken ct)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "NovaPlayerUpdate");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        string installerPath = Path.Combine(tempDir, "NovaPlayer-Setup-Update.exe");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            totalRead += read;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percentage = (double)totalRead / totalBytes.Value * 100.0;
                progress?.Report(percentage);
            }
        }

        return installerPath;
    }

    public static void InstallAndRestart(string installerPath)
    {
        if (!File.Exists(installerPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };

        Process.Start(psi);
        System.Windows.Application.Current.Shutdown();
    }

    private static int CompareVersions(string v1, string v2)
    {
        if (Version.TryParse(v1, out var parsed1) && Version.TryParse(v2, out var parsed2))
        {
            return parsed1.CompareTo(parsed2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }
}
