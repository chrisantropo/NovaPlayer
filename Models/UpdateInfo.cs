using System;

namespace NovaPlayer.Models;

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsNewer { get; set; }
}
