using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace EventAutoProfileBackup;

[Injectable]
public class ProfileService(
    ISptLogger<ProfileService> logger,
    JsonUtil jsonUtil,
    FileUtil fileUtil,
    TimeUtil timeUtil,
    EventAutoProfileBackupMetadata modMetadata,
    AutoProfileBackupConfig backupConfig,
    SaveServer saveServer,
    ConfigServer configServer
)
{
    private string backupPath {get; set;} = string.Empty;
    private string profilesToRestorePath {get; set;} = string.Empty;
    private string restoredProfilesPath {get; set;} = string.Empty;

    /// <summary>
    /// Initializes the ProfileRestoreService by setting up necessary paths and configurations.
    /// </summary>
    public void Initialize()
    {
        // Initialize the service, e.g., set up paths, load configurations, etc.
        logger.Debug($"[{modMetadata.Name}] ProfileRestoreService initialized.");

        // Set up paths, related to the directory in the backupConfig
        backupPath = Path.Combine(backupConfig.Directory, "Backups");
        profilesToRestorePath = Path.Combine(backupConfig.Directory, "ProfilesToRestore");
        restoredProfilesPath = Path.Combine(backupConfig.Directory, "RestoredProfiles");

        // Ensure the backup, ToRestore and Restored folders exist
        Directory.CreateDirectory(backupPath);
        Directory.CreateDirectory(profilesToRestorePath);
        Directory.CreateDirectory(restoredProfilesPath);
    }
    
    /// <summary>
    /// Backs up the profile for the given session ID.
    /// </summary>
    public async Task BackupProfile(string eventName, string sessionId)
    {
        logger.Debug($"[{modMetadata.Name}] Backing up profile for session: {sessionId}");

        // Get the profile username from the sessionId
        var profileUsername = saveServer.GetProfile(sessionId).ProfileInfo?.Username;
        if (string.IsNullOrEmpty(profileUsername))
        {
            logger.Warning($"[{modMetadata.Name}] Could not find profile for session: {sessionId}. Backup aborted.");
            return;
        }

        // Check if the profile username is of a headless client. If so, skip the backup.
        if (profileUsername.StartsWith("headless_"))
        {
            logger.Debug($"[{modMetadata.Name}] Skipping backup for headless client profile: {sessionId}({profileUsername})");
            return;
        }

        // Set up the user profile backup path and file name
        var userBackupPath = Path.Combine(backupPath, sessionId + "-" + profileUsername);
        var backupFileName = GenerateBackupDate() + "_" + eventName + ".json";

        // Get the profile data from the SaveServer and serialize it to JSON. Roughly copied from the SPT SaveServer.saveProfile method
        var profileJson = jsonUtil.Serialize(
            saveServer.GetProfile(sessionId),
            !configServer.GetConfig<CoreConfig>().Features.CompressProfile);

        if (string.IsNullOrEmpty(profileJson))
        {
            logger.Error($"[{modMetadata.Name}] Could not get and serialize profile for user: {sessionId}({profileUsername}). Backup aborted.");
            return;
        }

        // Save the serialized profile data to the backup file
        await fileUtil.WriteFileAsync(
            Path.Combine(userBackupPath, backupFileName),
            profileJson);
        
        if (backupConfig.BackupSavedLog)
        {
            logger.Success($"[{modMetadata.Name}] Backed up profile for user: {profileUsername} to {Path.Combine(userBackupPath, backupFileName)}");
        }

        // Cleanup old backups if necessary (if there are more than MaximumBackupPerProfile)
        if (backupConfig.MaximumBackupPerProfile >= 0)
        {
            var deletedFilesCount = CleanUpFolder(
                userBackupPath,
                backupConfig.MaximumBackupPerProfile);
            if (backupConfig.MaximumBackupDeleteLog && deletedFilesCount > 0)
            {
                logger.Info($"[{modMetadata.Name}] Maximum backups for user: {profileUsername} reached. Deleted {deletedFilesCount} old backup files");
            }
            else
            {
                logger.Debug($"[{modMetadata.Name}] No cleanup needed for user: {profileUsername}. Current backups are within the limit.");
            }
        }
        else
        {
            logger.Warning($"[{modMetadata.Name}] MaximumBackupPerProfile is set to 0. This may cause the folder to grow indefinitely and is not recommended");
        }
    }

    /// <summary>
    /// Restores the profiles in the ProfilesToRestore folder.
    /// </summary>
    public async Task RestoreRequestedProfiles()
    {
        // Roughly copied from the SPT SaveServer load and loadProfiles methods

        // Get all the json files in the ProfilesToRestore folder
        var profileFiles = fileUtil.GetFiles(profilesToRestorePath).Where(item => fileUtil.GetFileExtension(item).Equals(".json"));

        // Iterate over the profile files to restore
        foreach (var profileFile in profileFiles)
        {
            await RestoreProfileAsync(profileFile);
        }
    }

    /// <summary>
    ///     Restores a single profile from the given JsonObject.
    /// </summary>
    /// <param name="profileFile">The profile file to restore.</param>
    /// 
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task RestoreProfileAsync(string profileFile)
    {
        var profileFilePath = Path.Combine(profilesToRestorePath, profileFile);
        logger.Info($"[{modMetadata.Name}] Restoring {profileFilePath}");
        
        // Manually read the profile json file and deserialize it to a SptProfile object
        SptProfile? profile = await jsonUtil.DeserializeFromFileAsync<SptProfile>(profileFilePath);

        // Ensure the profile and profile ID are not null
        if (profile is null || profile.ProfileInfo?.ProfileId is not MongoId profileId)
        {
            logger.Warning($"[{modMetadata.Name}] Profile or Profile ID is null/invalid for file: {profileFile}");
            return; // Stop processing this profile if profile or ID is null
        }
        var profileUsername = profile.ProfileInfo.Username;

        // If a profile with the same id exists in the SaveServer, we need to delete it first
        if (saveServer.ProfileExists(profileId))
        {
            logger.Debug($"[{modMetadata.Name}] Profile with ID: {profileId} already exists. Deleting existing profile before restore.");
            saveServer.DeleteProfileById(profileId); // Removes the profile from the SaveServer
            saveServer.RemoveProfile(profileId); // Removes the profile json file from user/profiles
        }
        
        // Add the profile to the SaveServer
        saveServer.AddProfile(profile); // Adds the profile to the SaveServer's in-memory collection.

        // Tell the SaveServer to save the profile to the user/profiles folder
        await saveServer.SaveProfileAsync(profileId);
        logger.Success($"[{modMetadata.Name}] Restored {profileFile} for user: {profileUsername} with ID: {profileId}");

        // Move the restored profile file to the RestoredProfiles folder
        fileUtil.CopyFile(profileFilePath, Path.Combine(restoredProfilesPath, profileFile), false);
        fileUtil.DeleteFile(profileFilePath);
        logger.Debug($"[{modMetadata.Name}] Moved restored profile file to RestoredProfiles folder."); 

        // Clean up the RestoredProfiles folder if necessary
        if (backupConfig.MaximumRestoredFiles >= 0)
        {
            var deletedFilesCount = CleanUpFolder(restoredProfilesPath, backupConfig.MaximumRestoredFiles);
            
            if (backupConfig.MaximumRestoredDeleteLog && deletedFilesCount > 0)
            {
                logger.Info($"[{modMetadata.Name}] Maximum restored profiles reached. Deleted {deletedFilesCount} old restored profile files");
            }
            else
            {
                logger.Debug($"[{modMetadata.Name}] No cleanup needed for RestoredProfiles folder. Current restored profiles are within the limit.");
            }
        }
        else
        {
            logger.Warning($"[{modMetadata.Name}] MaximumRestoredProfilesKeep is set to 0. This may cause the folder to grow indefinitely and is not recommended");
        }
        
        return;
    }

    /// <summary>
    /// Cleans up the specified folder by deleting old files, keeping only the most recent ones.
    /// </summary>
    /// 
    /// <param name="folderPath">The path of the folder to clean up</param>
    /// <param name="maxFilesToKeep">The maximum number of files to keep in the folder</param>
    /// 
    /// <returns>The number of deleted files</returns>
    private int CleanUpFolder(string folderPath, int maxFilesToKeep)
    {
        logger.Debug($"[{modMetadata.Name}] Cleaning up folder: {folderPath} to keep only {maxFilesToKeep} files.");

        // Get the json files in the folder and sort them by name, which begins with the timestamp
        var files = fileUtil.GetFiles(folderPath).Where(item => fileUtil.GetFileExtension(item).Equals(".json"))
            .Select(file => new FileInfo(file))
            .OrderByDescending(file => file.Name)
            .ToList();
        logger.Debug($"[{modMetadata.Name}] Found {files.Count} files in the folder.");

        var deletedFilesCount = 0;
        // If there are more files than the maxFilesToKeep, delete the oldest files until we reach the limit
        while (files.Count > maxFilesToKeep)
        {
            var fileToDelete = files.Last();
            logger.Debug($"[{modMetadata.Name}] Deleting file: {fileToDelete.Name}");
            if (fileUtil.DeleteFile(fileToDelete.FullName))
            {
                deletedFilesCount++;
                files.RemoveAt(files.Count - 1);
                logger.Debug($"[{modMetadata.Name}] Deleted successfully: {fileToDelete.Name}");
            }
            else
            {
                logger.Error($"[{modMetadata.Name}] Error deleting file: {fileToDelete.Name}.");
                // Break the loop to avoid potential infinite loop if deletion keeps failing
                return deletedFilesCount;
            }
        }
        return deletedFilesCount;
    }

    /// <summary>
    /// Generates a formatted backup date string in the format `YYYY-MM-DD_hh-mm-ss`.
    /// Copied from SPT's BackupService.GenerateBackupDate since it's protected and we can't access it here.
    /// </summary>
    /// <returns> The formatted backup date string. </returns>
    protected string GenerateBackupDate()
    {
        return timeUtil.GetDateTimeNow().ToString("yyyy-MM-dd_HH-mm-ss");
    }
}