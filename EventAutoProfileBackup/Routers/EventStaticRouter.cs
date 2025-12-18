using EventAutoProfileBackup.Callbacks;
using EventAutoProfileBackup.Models;
using EventAutoProfileBackup.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace EventAutoProfileBackup.Routers;

[Injectable]
public class EventStaticRouter(
    ISptLogger<EventAutoProfileBackup> logger,
    JsonUtil jsonUtil,
    EventCallback eventCallback,
    ModMetadata modMetadata,
    ModConfigService modConfigService)
    : StaticRouter(
        jsonUtil,
        GetEventRoutes(logger, eventCallback, modMetadata, modConfigService))
{
    /// <summary>
    ///     Creates a list of routes on which to trigger events based on the mod configuration.
    /// </summary>
    /// <returns></returns>
    private static List<RouteAction> GetEventRoutes(ISptLogger<EventAutoProfileBackup> logger,
        EventCallback eventCallbacks, ModMetadata modMetadata, ModConfigService modConfigService)
    {
        AutoProfileBackupConfig config = modConfigService.GetConfig();
        List<RouteAction> routes = [];

        if (!config.Enabled)
        {
            // if the mod is not enabled, no routes should be registered so just return the empty list of routes. 
            // A warning is logged by the EventAutoProfileBackup class so we don't need to here.
            return routes;
        }

        foreach (var autoBackupEvent in config.AutoBackupEvents)
        {
            routes.Add(
                new RouteAction(
                    autoBackupEvent.Route,
                    async (_, _, sessionId, output)
                        => await eventCallbacks.OnEvent(autoBackupEvent.Name, sessionId, output)
                )
            );
            logger.Success($"[{modMetadata.Name}] Registered AutoBackupEvent: {autoBackupEvent.Name} on route: {autoBackupEvent.Route}");
        }

        return routes;
    }
}