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

        if (!config.Enabled)
        {
            // If the mod is not enabled,just return an empty list of routes. A warning is logged by the EventAutoProfileBackup class.
            return new List<RouteAction>();
        }

        List<RouteAction> routes = new List<RouteAction>();
        foreach (var autoBackupEvent in config.AutoBackupEvents)
        {
            routes.Add(
                new RouteAction(
                    autoBackupEvent.Route,
                    async (_, _, sessionId, output)
                        => await eventCallbacks.OnEvent(autoBackupEvent.Name, sessionId, output)
                )
            );
            logger.Info($"[{modMetadata.Name}] Registered AutoBackupEvent: {autoBackupEvent.Name} on route: {autoBackupEvent.Route}");
        }

        return routes;
    }
}