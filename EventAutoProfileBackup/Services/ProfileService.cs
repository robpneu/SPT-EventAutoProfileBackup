using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace EventAutoProfileBackup.Services;

[Injectable]
public class ProfileService(
    ISptLogger<ProfileService> logger,
    JsonUtil jsonUtil,
    FileUtil fileUtil,
    TimeUtil timeUtil,
    SaveServer saveServer,
    ConfigServer sptConfigServer,
    ModConfigService modConfigService,
    ModMetadata modMetadata
)
{
    private readonly string _backupPath = Path.Combine(modConfigService.GetConfig().Directory, "Backups");

    private readonly string _profilesToRestorePath = Path.Combine(modConfigService.GetConfig().Directory, "ProfilesToRestore");

    private readonly string _restoredProfilesPath = Path.Combine(modConfigService.GetConfig().Directory, "RestoredProfiles");

    /// <summary>
    ///     Ensure the necessary directories for profile backups and restorations are created.
    /// </summary>
    public void CreateDirectories()
    {
        // Ensure the backup, ToRestore and Restored folders exist
        Directory.CreateDirectory(_backupPath);
        Directory.CreateDirectory(_profilesToRestorePath);
        Directory.CreateDirectory(_restoredProfilesPath);
    }

    /// <summary>
    ///     Backs up the profile for the given session ID.
    /// </summary>
    public async Task BackupProfileAsync(string eventName, MongoId sessionId)
    {
        logger.Debug($"[{modMetadata.Name}] Backing up profile for session: {sessionId}");

        var autoProfileBackupConfig = modConfigService.GetConfig();

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
        var userBackupPath = Path.Combine(_backupPath, sessionId + "-" + profileUsername);
        var backupFileName = GenerateBackupDate() + "_" + eventName + ".json";

        // Get the profile data from the SaveServer and serialize it to JSON. Roughly copied from the SPT SaveServer.saveProfile method
        var profileJson = jsonUtil.Serialize(
            saveServer.GetProfile(sessionId),
            !sptConfigServer.GetConfig<CoreConfig>().Features.CompressProfile);

        if (string.IsNullOrEmpty(profileJson))
        {
            logger.Error($"[{modMetadata.Name}] Could not get and serialize profile for user: {sessionId}({profileUsername}). Backup aborted.");
            return;
        }

        // Save the serialized profile data to the backup file
        await fileUtil.WriteFileAsync(
            Path.Combine(userBackupPath, backupFileName),
            profileJson);

        if (autoProfileBackupConfig.BackupSavedLog)
        {
            logger.Success($"[{modMetadata.Name}] Backed up profile for user: {profileUsername} to {Path.Combine(userBackupPath, backupFileName)}");
        }

        // Cleanup old backups if there are more than MaximumBackupPerProfile
        if (autoProfileBackupConfig.MaximumBackupPerProfile >= 0)
        {
            var deletedFilesCount = CleanUpFolder(userBackupPath, autoProfileBackupConfig.MaximumBackupPerProfile);

            if (autoProfileBackupConfig.MaximumBackupDeleteLog && deletedFilesCount > 0)
            {
                logger.Info($"[{modMetadata.Name}] Maximum backups for user: {profileUsername} reached. Deleted {deletedFilesCount} old backup files");
            }
            else if (deletedFilesCount == 0)
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
    ///     Restores the profiles in the ProfilesToRestore folder.
    /// </summary>
    public async Task RestoreRequestedProfilesAsync()
    {
        // Get all the JSON files in the ProfilesToRestore folder
        var profileFiles = fileUtil
            .GetFiles(_profilesToRestorePath)
            .Where(item => fileUtil.GetFileExtension(item).Equals("json"));

        // Iterate over the profile files to restore
        foreach (var profileFilePath in profileFiles)
        {
            await RestoreProfileAsync(profileFilePath);
        }
    }

    /// <summary>
    ///     Restores a single profile from a JSON file.
    ///     Roughly copied from the SPT SaveServer load and loadProfiles methods
    /// </summary>
    /// <param name="profileFilePath">The profile file path to restore.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task RestoreProfileAsync(string profileFilePath)
    {
        var autoProfileBackupConfig = modConfigService.GetConfig();
        var profileFile = fileUtil.GetFileNameAndExtension(profileFilePath);
        logger.Info($"[{modMetadata.Name}] Restoring {profileFile}");

        // Manually read the profile JSON file and deserialize it to a SptProfile object
        SptProfile? profile = await jsonUtil.DeserializeFromFileAsync<SptProfile>(profileFilePath);

        // Ensure the profile is not null and profile ID in not null and a valid MongoId
        if (profile?.ProfileInfo?.ProfileId is not MongoId profileId)
        {
            logger.Warning($"[{modMetadata.Name}] Profile or Profile ID is null/invalid for file: {profileFile}");
            return; // Stop processing this file if the profile is null and/or profileId is invalid.
        }

        var profileUsername = profile.ProfileInfo.Username;

        // If a profile with the same id exists in the SaveServer, we need to delete it first
        if (saveServer.ProfileExists(profileId))
        {
            logger.Debug($"[{modMetadata.Name}] Profile with ID: {profileId} already exists. Deleting existing profile before restore.");
            saveServer.DeleteProfileById(profileId); // Removes the profile from the SaveServer
            saveServer.RemoveProfile(profileId); // Removes the profile JSON file from user/profiles
        }

        // Add the profile to the SaveServer
        saveServer.AddProfile(profile); // Adds the profile to the SaveServer's in-memory collection.

        // Tell the SaveServer to save the profile to the user/profiles folder
        await saveServer.SaveProfileAsync(profileId);
        logger.Success($"[{modMetadata.Name}] Restored {profileFile} for user: {profileUsername} with ID: {profileId}");

        // Move the restored profile file to the RestoredProfiles folder. Will overwrite if a file with the same name exists.
        fileUtil.CopyFile(profileFilePath, Path.Combine(_restoredProfilesPath, profileFile), overwrite:true);
        fileUtil.DeleteFile(profileFilePath);
        logger.Debug($"[{modMetadata.Name}] Moved restored profile file to RestoredProfiles folder.");

        // Clean up the RestoredProfiles folder if necessary
        if (autoProfileBackupConfig.MaximumRestoredFiles >= 0)
        {
            var deletedFilesCount = CleanUpFolder(_restoredProfilesPath, autoProfileBackupConfig.MaximumRestoredFiles);

            if (autoProfileBackupConfig.MaximumRestoredDeleteLog && deletedFilesCount > 0)
            {
                logger.Info($"[{modMetadata.Name}] Maximum restored profiles reached. Deleted {deletedFilesCount} old restored profile files");
            }
            else if (deletedFilesCount == 0)
            {
                logger.Debug($"[{modMetadata.Name}] No cleanup needed for RestoredProfiles folder. Current restored profiles are within the limit.");
            }
        }
        else
        {
            logger.Warning($"[{modMetadata.Name}] MaximumRestoredProfilesKeep is set to {autoProfileBackupConfig.MaximumRestoredFiles}. This may cause the folder to grow indefinitely and is not recommended");
        }
    }

    /// <summary>
    ///     Cleans up the specified folder by deleting old files, keeping only the most recent ones.
    /// </summary>
    /// 
    /// <param name="folderPath">The path of the folder to clean up</param>
    /// <param name="maxFilesToKeep">The maximum number of files to keep in the folder</param>
    /// 
    /// <returns>The number of deleted files</returns>
    private int CleanUpFolder(string folderPath, int maxFilesToKeep)
    {
        logger.Debug($"[{modMetadata.Name}] Cleaning up folder: {folderPath} to keep only {maxFilesToKeep} files.");

        // Get the JSON files in the folder and sort them by name, which begins with the timestamp
        var files = fileUtil.GetFiles(folderPath).Where(item => fileUtil.GetFileExtension(item).Equals("json"))
            .Select(file => new FileInfo(file))
            .OrderByDescending(file => file.Name)
            .ToList();
        logger.Debug($"[{modMetadata.Name}] Found {files.Count} files in the folder.");

        var deletedFilesCount = 0; // Counter for deleted files

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
    ///     Generates a formatted backup date string in the format `YYYY-MM-DD_hh-mm-ss`, in local time.
    ///     Roughly from SPT's BackupService.GenerateBackupDate
    /// </summary>
    /// <returns> The formatted backup date string. </returns>
    private string GenerateBackupDate()
    {
        return timeUtil.GetDateTimeNow().ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
    }
}