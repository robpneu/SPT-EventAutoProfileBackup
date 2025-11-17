using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Utils;

namespace EventAutoProfileBackup;

[Injectable]
public class EventStaticRouter(EventCallback eventCallbacks, JsonUtil jsonUtil, string eventName, string routeUrl)
: StaticRouter(
    jsonUtil, [
        new RouteAction(
           routeUrl,
            async (
                url, info, sessionId,
                output
            ) => await eventCallbacks.onEvent(eventName, sessionId, output)
        )
]) {}