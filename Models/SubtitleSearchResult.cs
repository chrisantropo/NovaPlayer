namespace NovaPlayer.Models;

public class SubtitleSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = "OpenSubtitles";
    public string Title { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string LanguageName { get; set; } = "Español";
    public string LanguageCode { get; set; } = "es";
    public string FlagEmoji { get; set; } = "🇪🇸";
    public string Format { get; set; } = "SRT";
    public string DownloadUrl { get; set; } = string.Empty;
    public double Rating { get; set; } = 0.0;
    public int DownloadsCount { get; set; } = 0;
    public bool HearingImpaired { get; set; } = false;
    public bool IsDownloading { get; set; } = false;
    public bool IsDownloaded { get; set; } = false;
    public string LocalFilePath { get; set; } = string.Empty;

    public string RatingFormatted => Rating > 0 ? $"★ {Rating:0.1}" : "★ Nuevo";
    public string DownloadsFormatted => DownloadsCount > 1000 
        ? $"{DownloadsCount / 1000.0:0.1}k descargas" 
        : $"{DownloadsCount} descargas";
}
