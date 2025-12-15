using EventAutoProfileBackup.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace EventAutoProfileBackup;

[Injectable] // Injectable to be used as-needed by other classes
public record ModMetadata : AbstractModMetadata
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
    public override string License { get; init; } = "CC-BY-NC-SA-4.0";
}

// Load just before the PostSptModLoader as that would be just after all callbacks are loaded
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader - 1)]
public class EventAutoProfileBackup(
    ISptLogger<EventAutoProfileBackup> logger,
    ModMetadata modMetadata,
    ProfileService profileService,
    ModConfigService modConfigService
) : IOnLoad
{
    public async Task OnLoad()
    {
        if (!modConfigService.GetConfig().Enabled)
        {
            // If the mod is not enabled, log a warning message and return
            logger.Warning($"[{modMetadata.Name}] EventAutoProfileBackup mod is disabled. Backups will not be made.");
            return;
        }

        // Create necessary directories for backups
        profileService.CreateDirectories();
        logger.Success($"[{modMetadata.Name}] mod loaded successfully.");
        
        // Restore any requested profiles
        await profileService.RestoreRequestedProfilesAsync();
    }
}