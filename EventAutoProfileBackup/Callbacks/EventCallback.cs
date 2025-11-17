using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace EventAutoProfileBackup;

[Injectable]
public class EventCallback (ISptLogger<EventAutoProfileBackup> logger, ProfileService profileService)
{
    public ValueTask<string> OnEvent(string eventName, MongoId sessionId, string? output)
    {
        logger.Info($"Event triggered: {eventName} for session: {sessionId}");

        // Call the ProfileService to back up the profile with fire and forget to not block the route
        _ = profileService.BackupProfileAsync(eventName, sessionId);

        // Return the output unmodified (or empty string if null)
        return new ValueTask<string>(output ?? string.Empty);
    }
}