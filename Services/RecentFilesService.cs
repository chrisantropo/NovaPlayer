using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using NovaPlayer.Models;

namespace NovaPlayer.Services;

public class RecentFilesService
{
    private const int MaxItems = 10;
    private readonly string _storagePath;
    public ObservableCollection<RecentFileItem> RecentFiles { get; } = new();

    public RecentFilesService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "NovaPlayer");
        Directory.CreateDirectory(folder);
        _storagePath = Path.Combine(folder, "recent_files.json");
        Load();
    }

    public void AddFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            var existing = RecentFiles.FirstOrDefault(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.LastPlayed = DateTime.Now;
                RecentFiles.Remove(existing);
                RecentFiles.Insert(0, existing);
            }
            else
            {
                var item = new RecentFileItem
                {
                    Title = Path.GetFileName(filePath),
                    FilePath = filePath,
                    LastPlayed = DateTime.Now
                };
                RecentFiles.Insert(0, item);
                while (RecentFiles.Count > MaxItems)
                {
                    RecentFiles.RemoveAt(RecentFiles.Count - 1);
                }
            }
            Save();
        }
        catch { }
    }

    public void Clear()
    {
        RecentFiles.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                string json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<RecentFileItem>>(json);
                if (items != null)
                {
                    RecentFiles.Clear();
                    foreach (var item in items.Where(i => File.Exists(i.FilePath)))
                    {
                        RecentFiles.Add(item);
                    }
                }
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(RecentFiles.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch { }
    }
}
