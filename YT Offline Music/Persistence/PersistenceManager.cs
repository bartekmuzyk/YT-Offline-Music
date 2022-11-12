using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace YT_Offline_Music.Persistence;

public static class PersistenceManager
{
    public static PersistentData PersistedData { get; private set; } = new();

    private static string? loadedFrom;

    public static async Task LoadPersistedData(string dataFilePath)
    {
        loadedFrom = dataFilePath;
        
        if (!File.Exists(dataFilePath)) return;
        
        await using var openStream = File.OpenRead(dataFilePath);
        PersistedData = (await JsonSerializer.DeserializeAsync<PersistentData>(openStream))!;
        await openStream.DisposeAsync();
    }

    public static async Task SavePersistedData()
    {
        if (loadedFrom == null) return;

        await using var createStream = File.Create(loadedFrom);
        await JsonSerializer.SerializeAsync(createStream, PersistedData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await createStream.DisposeAsync();
    }
}