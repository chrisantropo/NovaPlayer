using System;
using System.IO;

namespace NovaPlayer.Models;

public class PlaylistItem
{
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public string FormattedDuration => Duration > TimeSpan.Zero 
        ? (Duration.Hours > 0 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss")) 
        : "--:--";

    public bool IsPlaying { get; set; }

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

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

    public static PlaylistItem FromFilePath(string path)
    {
        return new PlaylistItem
        {
            Title = Path.GetFileNameWithoutExtension(path),
            FilePath = path
        };
    }
}
