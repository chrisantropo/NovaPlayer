using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using NovaPlayer.Models;

namespace NovaPlayer.Services;

public enum RepeatMode
{
    None,
    All,
    One
}

public class PlaylistService
{
    private readonly string _recentHistoryPath;
    private readonly Random _random = new Random();

    public ObservableCollection<PlaylistItem> Items { get; } = new ObservableCollection<PlaylistItem>();
    public ObservableCollection<string> RecentFiles { get; } = new ObservableCollection<string>();

    public int CurrentIndex { get; private set; } = -1;
    public PlaylistItem? CurrentItem => CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null;

    public RepeatMode Repeat { get; set; } = RepeatMode.None;
    public bool Shuffle { get; set; } = false;

    public event Action<PlaylistItem>? CurrentItemChanged;

    public PlaylistService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appData, "NovaPlayer");
        if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
        _recentHistoryPath = Path.Combine(configDir, "recent_history.json");

        LoadRecentHistory();
    }

    public void AddFile(string path, bool playImmediately = false)
    {
        if (!File.Exists(path)) return;

        // Check if already in playlist
        var existing = Items.FirstOrDefault(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (playImmediately)
            {
                SetCurrent(Items.IndexOf(existing));
            }
            return;
        }

        var item = PlaylistItem.FromFilePath(path);
        Items.Add(item);
        AddToRecent(path);

        if (playImmediately || Items.Count == 1)
        {
            SetCurrent(Items.Count - 1);
        }
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        bool wasEmpty = Items.Count == 0;
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var item = PlaylistItem.FromFilePath(path);
                Items.Add(item);
                AddToRecent(path);
            }
        }

        if (wasEmpty && Items.Count > 0)
        {
            SetCurrent(0);
        }
    }

    public void SetCurrent(int index)
    {
        if (index < 0 || index >= Items.Count) return;

        foreach (var item in Items) item.IsPlaying = false;

        CurrentIndex = index;
        var current = Items[CurrentIndex];
        current.IsPlaying = true;
        CurrentItemChanged?.Invoke(current);
    }

    public void Next()
    {
        if (Items.Count == 0) return;

        if (Repeat == RepeatMode.One)
        {
            if (CurrentItem != null) CurrentItemChanged?.Invoke(CurrentItem);
            return;
        }

        if (Shuffle && Items.Count > 1)
        {
            int nextIndex;
            do
            {
                nextIndex = _random.Next(0, Items.Count);
            } while (nextIndex == CurrentIndex);
            SetCurrent(nextIndex);
            return;
        }

        if (CurrentIndex + 1 < Items.Count)
        {
            SetCurrent(CurrentIndex + 1);
        }
        else if (Repeat == RepeatMode.All)
        {
            SetCurrent(0);
        }
    }

    public void Previous()
    {
        if (Items.Count == 0) return;

        if (CurrentIndex - 1 >= 0)
        {
            SetCurrent(CurrentIndex - 1);
        }
        else if (Repeat == RepeatMode.All)
        {
            SetCurrent(Items.Count - 1);
        }
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Items.Count) return;

        bool wasPlaying = (index == CurrentIndex);
        Items.RemoveAt(index);

        if (wasPlaying)
        {
            if (Items.Count > 0)
            {
                SetCurrent(Math.Min(index, Items.Count - 1));
            }
            else
            {
                CurrentIndex = -1;
            }
        }
        else if (index < CurrentIndex)
        {
            CurrentIndex--;
        }
    }

    public void Clear()
    {
        Items.Clear();
        CurrentIndex = -1;
    }

    private void AddToRecent(string path)
    {
        try
        {
            RecentFiles.Remove(path);
            RecentFiles.Insert(0, path);
            while (RecentFiles.Count > 15) RecentFiles.RemoveAt(RecentFiles.Count - 1);

            SaveRecentHistory();
        }
        catch { }
    }

    private void SaveRecentHistory()
    {
        try
        {
            string json = JsonSerializer.Serialize(RecentFiles.ToList());
            File.WriteAllText(_recentHistoryPath, json);
        }
        catch { }
    }

    private void LoadRecentHistory()
    {
        try
        {
            if (File.Exists(_recentHistoryPath))
            {
                string json = File.ReadAllText(_recentHistoryPath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    RecentFiles.Clear();
                    foreach (var item in list)
                    {
                        if (File.Exists(item)) RecentFiles.Add(item);
                    }
                }
            }
        }
        catch { }
    }
}
