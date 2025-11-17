# EventAutoProfileBackup SPT Mod
Automatic event-based profile backup system with easy restore system.

## Usage
After installation this will automatically begin to backup each profile individually when any of the following happens 
- `GameLaunch`: When the game client is launched
- `RaidStart`: When a raid starts
- `RaidEnd`: When a raid ends
- `GameClose`: When the game client is closed

### Backup files
The backup files are stored as follows
- Path: `user/profiles/EventAutoBackup/backups/profileUsername-profileID/`
- File format: `year-month-day_hour-minute-second_​event.json`
  - Example: `2025-01-14_21-14-49_GameLaunch.json`

### Restore
Automatically and safely restores profile backups by just placing the desired backup file in the `ProfilesToRestore` folder.

#### Process
- Copy or move the backup file you wish to restore to `user/profiles/EventAutoBackup/ProfilesToRestore/`.
- Start or restart the server
- When the restore is complete the file will be moved to `user/profiles/EventAutoBackup/RestoredProfiles/`.
  - The number of restored backup files retained is set by the `MaximumRestoredBackupFiles` parameter in the config.jsonc.

## Configuration
The configuration is found at `user/mods/eventautoprofilebackup/config/config.jsonc`

Each event can be configurable in the following ways
- Enable or disable
- The name of the event (which is used in when creating the backup files)
- The route to which triggers the 

For a bit of background, the "events" are triggered when the server receives a call to the specified route. You can enable, disable and/or rename any of the events. Do not change the Route unless you know what you're doing. (You should only have to rename them if SPT changes them in a future version).

#### Custom routes

If you are so inclined, you can add a route to the list of `AutoBackupEvents` in the config like this:

```JSONC
{ "Name": "AnotherEvent", "Route": "/this/is/another/route" }
```

A few words of caution:
1. I recommend that you do not add a route that occurs frequently.
	- I don't really know the limit of how often often a backup can be made before causing server performance issues from it constantly creating and probably deleting backup files.
	- I imagine that a significant number of profile backups of the same event would make it difficult to find the exact one you want.
2. ​I do not know if all routes will actually work. I have not tested any beyond the ones included by default so any you add are at your own ris.
	- Having said that, if you have an idea for an event to that you think should be included by default but you don't want to venture into this on your own, please leave a comment on the hub page or open a Github issue [here](https://github.com/robpneu/SPT-EventAutoProfileBackup/issues) and I'll see what I can do.​

## Contributing

You are free to fork, improve and send PRs to improve the project. Please try
to make your code coherent for the other developers.

## Development Requirements

- [Visual Studio Code](https://code.visualstudio.com/)
- [.NET SDK 9.0.x](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

## History

The mod was originally known as Lua-AutoProfileBackup. I rewrote most of it for SPT 3.10 and then entirely in migrating it to C# for SPT 4.0.

Mod history:

SPT Version     | Author
----------      | ----------
SPT 3.1.x-3.2.5 | Lua
SPT 3.4-3.5     | Reis
3.6-3.9         | Props

I also received some great help in the SPT discord from DrakiaXYZ, acidphantasm, and Jehree. Thank you!

## Licenses

[<img src="https://mirrors.creativecommons.org/presskit/buttons/88x31/svg/by-nc-sa.svg" alt="cc by-nc-sa" width="180" height="63" align="right">](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en)

This project is licensed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en).

### Credits

Project    | License
---------- | ----------
SPT.Server | [NCSA](https://github.com/sp-tarkov/server-csharp/blob/main/LICENSE)