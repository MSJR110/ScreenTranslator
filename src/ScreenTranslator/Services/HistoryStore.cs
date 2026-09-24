using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ScreenTranslator.Services;

public sealed record HistoryEntry(DateTime When, string Source, string Translation, string SourceLanguage, string Engine, string Mode);

/// <summary>Recent popup translations, persisted as JSON. Live-mode frames are deliberately not recorded.</summary>
public sealed class HistoryStore
{
    private const int Capacity = 500;
    private static readonly string FilePath = Path.Combine(AppSettings.Folder, "history.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public event Action? Changed;

    public static HistoryStore Load()
    {
        var store = new HistoryStore();
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath), Options) ?? new();
                foreach (var e in list.OrderByDescending(e => e.When).Take(Capacity))
                    store.Entries.Add(e);
            }
        }
        catch { /* unreadable history: start empty */ }
        return store;
    }

    public void Add(HistoryEntry entry)
    {
        // Collapse immediate duplicates (same text translated twice in a row).
        if (Entries.Count > 0 && Entries[0].Source == entry.Source && Entries[0].Translation == entry.Translation)
            return;

        Entries.Insert(0, entry);
        while (Entries.Count > Capacity) Entries.RemoveAt(Entries.Count - 1);
        Save();
    }

    public void Remove(HistoryEntry entry)
    {
        if (Entries.Remove(entry)) Save();
    }

    public void Clear()
    {
        Entries.Clear();
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Entries.ToList(), Options));
        }
        catch { /* disk trouble shouldn't break translating */ }
        Changed?.Invoke();
    }
}
