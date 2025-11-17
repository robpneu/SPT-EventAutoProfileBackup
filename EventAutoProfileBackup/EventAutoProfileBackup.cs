using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace EventAutoProfileBackup;

// Load just before the PostSptModLoader as that would be just after all the callbacks are loaded
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader - 1)]
public class EventAutoProfileBackup(
    ISptLogger<EventAutoProfileBackup> logger,
    EventAutoProfileBackupMetadata modMetadata,
    ProfileService profileService,
    EventCallback eventCallbacks,
    ModHelper modHelper,
    JsonUtil jsonUtil
    ) : IOnLoad
{
    private AutoProfileBackupConfig Config { get; set; } = new();
    private List<EventStaticRouter> eventStaticRouters { get; set; } = new();

    public async Task OnLoad()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        logger.Debug($"[{modMetadata.Name}] pathToMod: {pathToMod}");

        var pathToConfig = Path.Combine(pathToMod, "config.jsonc");
        logger.Debug($"[{modMetadata.Name}] pathToConfig: {pathToConfig}");

        // Get and deserialize the config.jsonc file from the assets folder
        Config = await jsonUtil.DeserializeFromFileAsync<AutoProfileBackupConfig>(pathToConfig) ?? new();

        if (!Config.Enabled)
        {
            // If the mod is not enabled, we log a message and return
            logger.Warning($"[{modMetadata.Name}] EventAutoProfileBackup mod is disabled. Backups will not be made.");
            return;
        }

        // If the mod is enabled, we log a message
        logger.Success($"[{modMetadata.Name}] Mod is enabled. Loading...");

        RegisterRoutes();

        // Initialize the ProfileRestoreService and restore any requested profiles
        profileService.Initialize();
        await profileService.RestoreRequestedProfiles();

        logger.Success($"[{modMetadata.Name}] mod loaded successfully.");
    }

    /// <summary>
    /// This method registers a new route for each AutoBackupEvent in the configuration.
    /// </summary>
    private void RegisterRoutes()
    {
        // Iterate over the AutoBackupEvents from the config and register a new RouteAction for each event
        if (Config.AutoBackupEvents is null || Config.AutoBackupEvents.Count() == 0)
        {
            // If there are no AutoBackupEvents, we log a message and return
            logger.Warning($"[{modMetadata.Name}] No AutoBackupEvents found in the configuration.");
            return;
        }
        else
        {
            // If there are AutoBackupEvents, we log a message
            logger.Debug($"[{modMetadata.Name}] Found {Config.AutoBackupEvents.Count()} AutoBackupEvents in the configuration.");

            foreach (var autoBackupEvent in Config.AutoBackupEvents)
            {
                eventStaticRouters.Add(new EventStaticRouter(
                    eventCallbacks,
                    jsonUtil,
                    autoBackupEvent.Name,
                    autoBackupEvent.Route
                ));
                logger.Info($"[{modMetadata.Name}] Registered AutoBackupEvent: {autoBackupEvent.Name} on route: {autoBackupEvent.Route}");
            }

            logger.Success($"[{modMetadata.Name}] Registered {Config.AutoBackupEvents.Count()} AutoBackupEvent(s).");
        }
    }
}