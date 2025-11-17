using SPTarkov.Server.Core.Models.Utils;

namespace EventAutoProfileBackup;

public class EventCallback (ISptLogger<EventCallback> logger, ProfileService profileService)
{
    public ValueTask<string> onEvent(string eventName, string sessionId, string? output)
    {
        logger.Info($"Event triggered: {eventName} for session: {sessionId}");

        // Call the ProfileService to back up the profile with fire and forget to not block the route
        _ = profileService.BackupProfile(eventName, sessionId);

        // Return the output unmodified (or empty string if null)
        return new ValueTask<string>(output ?? string.Empty);
    }
}