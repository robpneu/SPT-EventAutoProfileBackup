using System.Reflection;
using EventAutoProfileBackup.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils;

namespace EventAutoProfileBackup.Services;

// Service to easily access the mod configuration. Loads the config file on initialization.
[Injectable(InjectionType.Singleton)] // Singleton as the config should only be loaded once.
public class ModConfigService
{
    private readonly AutoProfileBackupConfig _config;

    public ModConfigService(ModHelper modHelper, JsonUtil jsonUtil)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var pathToConfig = Path.Combine(pathToMod, "config.jsonc");

        // Get and deserialize the config.jsonc file from the path
        _config = jsonUtil.DeserializeFromFile<AutoProfileBackupConfig>(pathToConfig) ?? new AutoProfileBackupConfig();
    }

    public AutoProfileBackupConfig GetConfig()
    {
        return _config;
    }
}