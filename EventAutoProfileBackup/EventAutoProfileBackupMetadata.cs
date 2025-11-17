using SPTarkov.Server.Core.Models.Spt.Mod;

namespace EventAutoProfileBackup;

public record EventAutoProfileBackupMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.friedengineer.eventautoprofilebackup";
    public override string Name { get; init; } = "EventAutoProfileBackup";
    public override string Author { get; init; } = "FriedEngineer";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.9.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/robpneu/SPT-EventAutoProfileBackup";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "CC-BY-NC-SA-4.0";
}