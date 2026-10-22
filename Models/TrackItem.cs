namespace NovaPlayer.Models;

public enum TrackType
{
    Audio,
    Subtitle,
    Video
}

public class TrackItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public TrackType Type { get; set; }
    public bool IsExternal { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public override string ToString() => Name;
}
