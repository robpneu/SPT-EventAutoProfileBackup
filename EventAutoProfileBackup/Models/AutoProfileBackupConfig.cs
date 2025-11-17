using System.Text.Json.Serialization;

namespace EventAutoProfileBackup;

// Model of the mod configuration
public record AutoProfileBackupConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("BackupSavedLog")]
    public bool BackupSavedLog { get; set; } = true;

    [JsonPropertyName("MaximumBackupDeleteLog")]
    public bool MaximumBackupDeleteLog { get; set; } = false;

    [JsonPropertyName("MaximumBackupPerProfile")]
    public int MaximumBackupPerProfile { get; set; } = 20;

    [JsonPropertyName("MaximumRestoredDeleteLog")]
    public bool MaximumRestoredDeleteLog { get; set; } = false;

    [JsonPropertyName("MaximumRestoredFiles")]
    public int MaximumRestoredFiles { get; set; } = 10;

    [JsonPropertyName("Directory")]
    public string Directory { get; set; } = "./user/profiles/AutoProfileBackups";

    [JsonPropertyName("AutoBackupEvents")]
    public AutoBackupEvent[] AutoBackupEvents { get; set; } = Array.Empty<AutoBackupEvent>();
}

// Model of a single AutoBackupEvent
public class AutoBackupEvent
{
    [JsonPropertyName("Name")]
    public required string Name { get; set; }
    [JsonPropertyName("Route")]
    public required string Route { get; set; }
}