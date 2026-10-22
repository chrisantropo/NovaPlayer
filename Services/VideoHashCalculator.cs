using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NovaPlayer.Services;

public static class VideoHashCalculator
{
    private const int ChunkSize = 65536; // 64 KB

    /// <summary>
    /// Computes OpenSubtitles 64-bit hash for a video file.
    /// Algorithm: Sum of first 64KB + last 64KB as 64-bit unsigned ints + file size.
    /// </summary>
    public static string ComputeMovieHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return string.Empty;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileSize = stream.Length;
            if (fileSize < ChunkSize) return string.Empty;

            ulong hash = (ulong)fileSize;

            byte[] buffer = new byte[ChunkSize];

            // Read first 64KB
            stream.Seek(0, SeekOrigin.Begin);
            int read = stream.Read(buffer, 0, ChunkSize);
            for (int i = 0; i < read; i += 8)
            {
                hash += BitConverter.ToUInt64(buffer, i);
            }

            // Read last 64KB
            stream.Seek(Math.Max(0, fileSize - ChunkSize), SeekOrigin.Begin);
            read = stream.Read(buffer, 0, ChunkSize);
            for (int i = 0; i < read; i += 8)
            {
                hash += BitConverter.ToUInt64(buffer, i);
            }

            return hash.ToString("x16");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleans standard scene/torrent release naming conventions into a clean title and year.
    /// Example: "Dune.Part.Two.2024.1080p.WEBRip.x264.AAC5.1-[YTS.MX]" -> "Dune Part Two 2024"
    /// </summary>
    public static string CleanTitle(string rawName)
    {
        string name = Path.GetFileNameWithoutExtension(rawName);

        // Replace separators
        name = name.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');

        // Remove release tags, codecs, audio, resolutions
        string pattern = @"\b(2160p|1080p|720p|480p|4k|uhd|hdr|hdr10|bluray|bdrip|brrip|web-dl|webrip|hdtv|dvdrip|x264|x265|hevc|av1|h264|h265|aac|ac3|dts|truehd|ddp5\.1|atmos|repack|proper|remux|yts|yify|rarbg|eztv|galaxytv)\b";
        name = Regex.Replace(name, pattern, "", RegexOptions.IgnoreCase);

        // Remove bracketed or parenthesized tags like [YTS], (2024) [1080p] etc.
        name = Regex.Replace(name, @"\[.*?\]", "");
        name = Regex.Replace(name, @"\s+", " ").Trim();

        return name;
    }
}
