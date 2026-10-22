using System;
using System.IO;

namespace NovaPlayer.Models;

public class RecentFileItem
{
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastPlayed { get; set; } = DateTime.Now;

    public string FormattedLastPlayed
    {
        get
        {
            var diff = DateTime.Now - LastPlayed;
            if (diff.TotalMinutes < 1) return "Justo ahora";
            if (diff.TotalMinutes < 60) return $"Hace {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24) return $"Hace {(int)diff.TotalHours} h";
            if (diff.TotalDays < 7) return $"Hace {(int)diff.TotalDays} d";
            return LastPlayed.ToString("dd/MM/yyyy");
        }
    }

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    public string DirectoryName
    {
        get
        {
            try { return Path.GetDirectoryName(FilePath) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }

    public string FileSizeFormatted
    {
        get
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    long bytes = new FileInfo(FilePath).Length;
                    if (bytes >= 1024 * 1024 * 1024)
                        return $"{bytes / (1024.0 * 1024 * 1024):0.1} GB";
                    if (bytes >= 1024 * 1024)
                        return $"{bytes / (1024.0 * 1024):0.1} MB";
                    return $"{bytes / 1024.0:0.1} KB";
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
