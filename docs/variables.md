# Variables

Every name RiryTools stores, reads or writes: settings keys, files and folders,
Git names, GUIDs, build symbols, tunable constants, menu paths, and the
publishing workflows' inputs, environment variables and secrets. Source paths
are relative to `com.riry.rirytools/Editor/` unless they start with a repository
name or `com.riry.ftemulator/` (FTEmulator's own package, in the same repository).

Each variable should be listed with the following formatting:
1. Variable name
2. What the variable is used for.
3. What scripts this variable is used in.

Variables are to be ordered A-Z alphabetically by name.

This should be presented as a table object. Each use gets a new row, columns are "Name", "Use" and "Scripts".

Example:

| Name | Use | Scripts |
|---|---|---|
| `RiryTools.AttachTo.AddedBones` | Bones the user added with **Add bone**, separated by commas. | `Utilities/AttachTo.cs` |
| `RiryTools.AutoSync.Enabled` | Turns AutoSync on or off for the project (the **Sync** toggle). | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncGitActions.cs`, `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |
| `RiryTools.AutoSync.Remote` | The Git remote AutoSync pushes to. | `AutoSync/AutoSyncGitRemotes.cs` |
| `RiryTools.BetterFbx.BlenderPath` | Path to the Blender executable used by the Better FBX import preset. Empty means it's detected automatically. | `ImportPresets/ImportPresetBetterFBX.cs` |
| `RiryTools.StopAutoGame.Enabled` | Turns Stop Auto Game on or off. | `Utilities/StopAutoGame.cs`, `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |

## Naming rules

- **Product name:** `RiryTools` in every label, menu, folder and key. Lower
  case (`rirytools`) only where the format needs it: file names, Git refs, UI
  Toolkit element names.
- **Settings keys:** `RiryTools.<Tool>.<Setting>`.
- **Folders:** `<Library|Logs|Documents>/RiryTools/<Tool>`, built by
  `Components/RiryToolsPaths.cs`. The project folder is `Assets/RiryTools`
  (`Components/RiryToolsProjectFolder.cs`).
- **Code:** namespace `Riry.RiryTools.Editor`. `Riry` is the author and
  `RiryTools` the product.
- **Tool names:**
  - Attach To
  - Invert Global Position
  - Find scripts that reference this
  - AutoSync
  - Avatar Migration Assistant
  - Blendshape Sync
  - Clean Animation Keys
  - Constraint Tools
    - Subtools: `ConstraintTrain*`, `ConstraintBezier*`, `InsertableProp*`
  - FTEmulator
  - GUID Tools
  - IconCap
  - Import Presets
  - Menu Mockup
  - Menu Tools
    - Tabs: Parameter budget and Menu manager
  - Refresh Scripts
  - Stop Auto Game

  File, folder and document names follow them per ./instructions~ files.

## Stored settings

### EditorPrefs

Per user, shared by every project.

| Name | Use | Scripts |
|---|---|---|
| `com.vrcfury.parameterCompressor` | VRCFury's own Parameter Compressor setting (int: 0 Compress, 1 Ask, 2 Fail; unset means Compress). Menu Tools reads it (never writes it). | `VRChat/MenuTool/MenuToolCompressor.cs` |
| `RiryTools.AttachTo.AddedBones` | Bones added with **Add bone** (string of HumanBodyBones names, comma separated; default `""`), listed after `AttachTo.CommonBones`. **Remove** takes one off. Read through `AttachTo.AddedBonesKey`. | `Utilities/AttachTo.cs` |
| `RiryTools.AvatarMigrator.Settings` | Avatar Migration Assistant settings as JSON: per-entry options, sub-options, ignore children and migrated folder. Unset means `StructureTolerance` 0.001, `ManualMatching` off, each entry as its `enabled` in types.yml, `MigratedFolder` empty. | `AvatarMigrator/AvatarMigratorSettings.cs` |
| `RiryTools.BetterFbx.BlenderPath` | Path to the Blender executable for Better FBX (string; `""` means detect). Read through `ImportPresetBetterFBX.BlenderPath`. | `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.BetterFbx.Options` | Better FBX export options (string; default `""`). Read through `ImportPresetBetterFBX.Options`. | `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.ImportPresets.UseTags` | Set Folder Import Presets' **Filters**: a tag in a file's name makes a file type of its own (bool; default true). Read through `ImportPresetPreferences.UseTags`. | `ImportPresets/ImportPresetPreferences.cs`, `ImportPresets/ImportPresetFolderRules.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.ImportPresets.UseEndings` | Set Folder Import Presets' **Filters**: a `_word` name ending makes a file type of its own (bool; default true); with both off, file extensions only. Read through `ImportPresetPreferences.UseEndings`. | `ImportPresets/ImportPresetPreferences.cs`, `ImportPresets/ImportPresetFolderRules.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.ScriptLoadGuard.Enabled` | Turns Script Load Guard on or off (bool; default `true`). Read through `ScriptLoadGuard.Enabled`. | `Debug/ScriptLoadGuard.cs`, `UI/RiryToolsMenuBar.cs` |
| `RiryTools.StopAllToolScripts.Enabled` | EditorUserSettings (UserSettings, this user in this project): `1` while **Stop all tool scripts** is on. Every RiryTools hook returns at once, importers and save processors pass files through, windows draw only a notice, and every menu entry but the toggle, Main Menu and Documentation is greyed out. Changing it reloads scripts. Read through `StopAllToolScripts.Active`. | `Components/StopAllToolScripts.cs`, `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs`, `UI/Components/RiryToolGUI.cs` and every tool's hooks and windows |
| `RiryTools.StopAutoGame.Enabled` | Turns Stop Auto Game on or off (bool; default `true`). Read through `StopAutoGame.Enabled`. | `Utilities/StopAutoGame.cs`, `UI/RiryToolsMainMenu.cs`, `UI/RiryToolsMenuBar.cs` |

### EditorUserSettings

Per user and per project, in `UserSettings/`.

| Name | Use | Scripts |
|---|---|---|
| `RiryTools.AutoSync.Enabled` | The **Sync** toggle (`1` / `0`; default `0`). Read through `AutoSync.Enabled`. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncGitActions.cs`, `UI/RiryToolsMainMenu.cs`, `UI/RiryToolsMenuBar.cs` |
| `RiryTools.AutoSync.LargeFileNoticeOff` | Remote URLs, one per line, where the large file notice's **Do Not Show Again** was chosen. Unset by default. | `AutoSync/AutoSync.cs` |
| `RiryTools.AutoSync.Remote` | The **Remote** AutoSync pushes to (a remote name). Unset means `origin`, else the first remote. Read through `AutoSyncGitRemotes.Selected`. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncGitRemotes.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs` |
| `RiryTools.AutoSync.TitleTemplate` | AutoSync's commit title template (string; empty means `{time}`, `AutoSync.DefaultTitleTemplate`): `{time}` is the save's time (`AutoSync.TimeFormat`), `{files}` the saved files' names (comma separated), `{branch}` the checked-out branch (`detached HEAD` when there is none). Line breaks become spaces. Set in Configuration's **Commit title**. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncJob.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs` |
| `RiryTools.AvatarMigrator.SdkNoticeOff` | `1` once **Do not show again** is chosen on the VRC SDK not detected popup. Unset by default. | `UI/AvatarMigrator/AvatarMigratorSdkNoticeWindow.cs`, `UI/AvatarMigrator/AvatarMigratorWindow.cs` |
| `RiryTools.FTEmulator.Backend` | FTEmulator's **Avatar backend**: `auto` (default), `native` or an avatar profile's id. Read through `FTEmulatorSettings.Backend`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.Behavior` | FTEmulator's **Behavior**: `Strict` (default) or `ModuleBehavior` (Documented module behavior). Read through `FTEmulatorSettings.Behavior`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.CustomMappings` | FTEmulator's own mappings (**Override mapping**) as a JSON object: avatar parameter name to a formula of tracking keys. Unset by default. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorDebugWindow.cs` |
| `RiryTools.FTEmulator.Discovery` | FTEmulator's **Discovery** toggle, the OSCQuery service (`1` / `0`; default `1`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.Hardware` | FTEmulator's **Hardware limits**: a hardware profile's id (default `unrestricted`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.Input` | FTEmulator's **Input**: `VrcftOsc` (default) or `Manual`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.ManualOutput` | FTEmulator's **Write manual values to the avatar** (`1` / `0`; default `1`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.FTEmulator.Port` | FTEmulator's **Receive port** on 127.0.0.1 (int; default `9000`, `FTEmulatorSettings.DefaultPort`): the port VRCFaceTracking sends to. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSettings.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `RiryTools.GuidTools.BrokenPromptOff` | Scene and prefab paths, one per line, where the broken reference prompt's **Do Not Show Again** was chosen. Unset by default. | `GuidTools/GuidToolBrokenReferenceMonitor.cs` |
| `RiryTools.GuidTools.Ignored` | Broken references chosen to **Ignore**, one per line: a missing GUID, or `<path> (file ID <n>)` for a missing object. Listed under Ignored; left out of the save check, its warning and the prompt on open. | `GuidTools/GuidToolIgnoredReferences.cs` |
| `RiryTools.MenuTool.LastAvatar` | GlobalObjectId of the last avatar the Menu Tools scanned (saved scenes only); selected when the Menu Tools opens with no target. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |

### SessionState

Until the editor quits; survives script reloads.

| Name | Use | Scripts |
|---|---|---|
| `RiryTools.AttachTo.LastAvatar` | Instance ID of the last avatar selected in the Hierarchy (VRCAvatarDescriptor, else humanoid Animator); fills **Target avatar**. | `Utilities/AttachToAvatarTracker.cs`, `UI/Utilities/AttachToWindow.cs` |
| `RiryTools.AutoSync.LargeFileNoticeShown` | Remote URLs, one per line, whose **Push Held** notice was shown this session. | `AutoSync/AutoSync.cs` |
| `RiryTools.AutoSync.Pending` | Saves not yet committed when scripts reloaded: one save per line, title then paths, tab-separated. | `AutoSync/AutoSync.cs` |
| `RiryTools.MenuTool.EntryState` | `MenuToolEntryState` JSON: `Avatar` (the avatar's GlobalObjectId; saved scenes only), `Vrcfury` and `Path` (a VRCFury avatar is matched by hierarchy path instead, since VRCFury replaces it in Play Mode), `Menu` (submenu path), `Writes` (`Name`, `Value`, in order). Set by Set as entry state; replayed in Play Mode once Gesture Manager has set up that avatar, one write per frame (a parameter VRCFury renamed is matched by fuzzy search in the built FX layer; else `Parameter not found: <name>` is logged), then the submenu opened; erased by Reset entry state. | `VRChat/MenuTool/MenuToolEntryState.cs`, `VRChat/MenuTool/GestureManager/GestureManagerEntryState.cs` |
| `RiryTools.MenuTool.UnsavedAvatars` | `<instance ID>=<key>` lines: keys of avatars in scenes not saved yet; moved into `avatars.json` when the scene is saved. | `VRChat/MenuTool/MenuToolAvatarKeys.cs` |
| `RiryTools.RefreshScripts.Reopen` | JSON of the RiryTools windows to reopen after **Refresh Scripts** reloads scripts (assembly-qualified type names, positions); erased once read. | `Utilities/RefreshScripts.cs` |
| `RiryTools.ScriptLoadGuard.Guarded` | Newline-separated asset paths Script Load Guard is guarding. | `Debug/ScriptLoadGuard.cs`, `UI/Debug/ScriptLoadGuardFailureWindow.cs` |
| `RiryTools.StopAutoGame.FocusedWindows` | Comma-separated instance IDs of the windows focused when Play Mode started. | `Utilities/StopAutoGame.cs` |
| `RiryTools.StopAutoGame.GameViewWasOpen` | Bool: a Game view existed before Play Mode. | `Utilities/StopAutoGame.cs` |
| `RiryTools.StopAutoGame.Missing.<what>` | Bool: the warning about a reflection lookup this Unity version lacks was already logged this session. | `Utilities/StopAutoGame.cs` |

### Other keys

Not stored, but named like settings keys.

| Name | Use | Scripts |
|---|---|---|
| `control:<name>` / `parameter:<name>` | FTEmulator's debug row keys (`FTEmulatorParameterStore.ControlPrefix` / `ParameterPrefix`): a hardware control, or an avatar parameter that arrives already mapped. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorParameterStore.cs`, `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSession.cs` |
| `local:RiryFTEmulator:<fingerprint>` | The avatar id FTEmulator's OSCQuery service gives in `/avatar/change`: an FNV-1a fingerprint of the avatar's name, Expression Parameters and native settings. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorAvatarScanner.cs` |
| `RiryTools.MenuMockup.Type` | Drag and drop data key: a component type dragged into the Menu Mockup Edit window. | `UI/Debug/MenuMockupEditWindow.cs` |
| `RiryTools.MenuTool.AddMenuName` | GUI control name of the name field in Menu Tools' Add menu dialog, so it gets keyboard focus. | `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs` |
| `RiryTools.MenuTool.Entry` | Drag and drop data key: the menu entry path dragged in the Menu manager tree. | `VRChat/UI/MenuTool/MenuToolManagerTree.cs` |
| Window `[SerializeField]` fields | Survive script reloads while the window is open. Each window file lists them in its glossary. | Every window under `UI/` and `VRChat/UI/` |

## Files and folders

### In the project

| Name | Use | Scripts |
|---|---|---|
| `.gitignore`, `.gitattributes` | Copied from a template by **Sync** (new repository) and **Configuration**. Built by `AutoSyncGitActions.CopyTemplate`. | `AutoSync/AutoSyncGitActions.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.cs` |
| `<main prefab folder>/<prefab> Variant.prefab`, `<controller> Variant.controller` | Find and Replace **Apply to child prefab**: the child prefab (a Prefab Variant of the main prefab) and the copied controllers it uses, next to the main prefab; unique names (Unity numbers them). | `VRChat/AnimatorTools/BehaviourFindReplaceChildPrefab.cs` |
| `<export folder>/.iconcap.json` | IconCap jobs of the folder (entries with camera, objects as GlobalObjectIds, settings, effect stack); hidden on Windows. Named by `IconCapFiles.JobFileName`. | `IconCap/IconCapFiles.cs` |
| `<export folder>/OLD_<n>/` | Icons a job's **Run** replaced, moved here without their `.meta` (one folder per Run). Named by `IconCapFiles.OldFolderPrefix`. | `IconCap/IconCapFiles.cs`, `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `<folder>/ImportPreset.md` | Import preset rules for a folder. Named by `ImportPresetMarker.FileName`. | `ImportPresets/ImportPresetFolderRules.cs`, `ImportPresets/ImportPresetMarker.cs`, `ImportPresets/ImportPresetPostprocessor.cs` |
| `<Icon Generator folder>/<prefix><source name><suffix>.png` | Icon Generator outputs, beside the asset. Named by `IconGenerator.OutputName`. | `IconCap/IconGenerator.cs` |
| `<migrated folder>/<original path without Assets/>` | Avatar Migration Assistant's duplicated controllers and clips (files from `Packages/` go under `<migrated folder>/Packages/`); never overwritten. Built by `AvatarMigratorMigratedFolder.Destination`. | `AvatarMigrator/AvatarMigratorMigratedFolder.cs`, `AvatarMigrator/AvatarMigratorRetargetAnim.cs` |
| `<model folder>/.<Model>_ani-extract.json~` | Extract Animation settings; no `.meta`, hidden on Windows, staged in Git. Built by `ImportPresetAnimationExtraction.PathFor`. | `ImportPresets/ImportPresetAnimationExtraction.cs`, `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs` |
| `<model folder>/Animations/<Model>_<Clip>.anim` | Extracted clips (or the model's Destination). Folder named by `ImportPresetAnimations.FolderName`. | `ImportPresets/ImportPresetAnimations.cs` |
| `<model>_ROOT.prefab` | Constraint Tools Train prefab variant. Suffix is `ConstraintTrainPrefabs.RootSuffix`. | `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs` |
| `<name>.fbx` beside `<name>.blend` | Better FBX export. Built by `ImportPresetBetterFBX.FbxPathFor`. | `ImportPresets/ImportPresetBetterFBX.cs`, `ImportPresets/ImportPresetPostprocessor.cs` |
| `Assets/RiryTools/` | Files unpacked from the package's `ProjectFolder~`, Constraint Tools Train clips, `_ROOT` prefab variants. Path is `RiryToolsProjectFolder.AssetPath`. | `Components/RiryToolsProjectFolder.cs`, `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `Assets/RiryTools/InsertableProps/IP_<id>_<first 8 of the controller GUID>/` | Insertable prop clips: one folder per pair and controller, replaced in place on Generate. Built by `InsertablePropAnimator.ClipFolderPath`. | `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.cs` |
| `Assets/RiryTools/MenuTool/<avatar>/<new menu>.asset` | Menu Tools' new, empty menus planned with **Add menu** as a menu asset, made by **Apply** (unique path) and linked by a SubMenu control in the parent's menu. Ctrl+Z does not delete them. Built by `MenuToolApply.NewMenuAsset`. | `VRChat/MenuTool/MenuToolApply.cs` |
| `Library/RiryTools/AutoSync/rewrite-index` | Temporary index while **Large Files** rewrites commits. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| `Library/RiryTools/GuidReferenceCache/` | `<name>_<hash>.rguiddb` reference caches. Path is `GuidToolReferenceCache.CacheDirectoryPath`. | `GuidTools/GuidToolReferenceCache.cs`, `UI/GuidTools/GuidToolClearReferenceCache.cs`, `UI/GuidTools/GuidToolsWindow.cs` |
| `Library/RiryTools/GuidTools/` | `restore` and `restore-meta` while a Git backup is written. | `GuidTools/GuidToolReferenceRepair.cs` |
| `Library/RiryTools/ImportPresets/` | `owned.txt` (path, size, write time and, for a clip, its model GUID), `Pending/<sha1>.txt`, `better_fbx_export.py`, `previous.fbx`. | `ImportPresets/ImportPresetOwnedFiles.cs`, `ImportPresets/ImportPresetAnimations.cs`, `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs` |
| `Logs/RiryTools/GuidTools/` | `guid-tools.log` and `archive/guid-tools_<UTC time>.log`. Path is `GuidToolRegenerate.LogDirectoryPath`. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolLog.cs`, `GuidTools/GuidToolReferenceRepair.cs`, `UI/GuidTools/GuidToolsWindow.cs` |
| `ProjectSettings/RiryToolsGuidRemap.tsv` | GUID remap ledger; keep it in version control. Path is `GuidToolRemapLedger.RelativePath`. | `GuidTools/GuidToolReferenceRepair.cs`, `GuidTools/GuidToolRemapLedger.cs`, `UI/GuidTools/GuidToolRegenerate.Prompts.cs` |
| `UserSettings/RiryTools/BlendshapeSync/<scene GUID>.json` | Blendshape Sync jobs of a scene (renderers as GlobalObjectIds; `Name`, the job's name, blank for the default "<master> > N children"). | `BlendshapeSync/BlendshapeSyncJobs.cs`, `BlendshapeSync/BlendshapeSyncJob.cs`, `UI/BlendshapeSync/BlendshapeSyncJobWindow.cs` |
| `UserSettings/RiryTools/MenuTool/<avatar key>.json` | Menu Tools' pathing JSON: the planned moves of one avatar (`Avatar`, `Key`, `Moves`: `From`, `To`, `Index`, `Choice`, `NewMenu` for a menu planned with **Add menu**; `NewControl`, `ControlType`, `Parameter`, `Value`, `Icon` (texture GUID) for **Add New Control**; `AssignIcon` with `From` and `Icon` for **Assign Menu Icon**; `Remove` with `From` for **Delete Menu Entry**; `ToNull` with `From` for **Move to… > <NULL>**; `Rename` (the new name) with `From` for **Rename Menu Entry**; `Destructive` for **Destructive Move**, which edits the item's own source inside its menu root). Deleted when empty. | `VRChat/MenuTool/MenuToolPlan.cs`, `VRChat/MenuTool/MenuToolApply.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `UserSettings/RiryTools/MenuTool/avatars.json` | The GUID stamped on each avatar Menu Tools planned for (`Object`: its GlobalObjectId, `Key`); nothing is written on the avatar. | `VRChat/MenuTool/MenuToolAvatarKeys.cs` |

### In the user's Documents folder

`%USERPROFILE%\Documents` on Windows, including a OneDrive redirect;
`~/Documents` elsewhere.

| Name | Use | Scripts |
|---|---|---|
| `Documents/RiryTools/FTEmulator/Recordings/` | `<yyyyMMdd-HHmmss>.ftrec` OSC recordings: `RTFR` header, version `1`, packet count, then each packet's time (seconds), length and bytes. Read back as untrusted (counts, lengths and times checked). Path is `FTEmulatorRecording.Folder`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorRecording.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs` |
| `Documents/RiryTools/GitTemplates/` | `<name>.gitignore` and `<name>.gitattributes` templates. Path is `AutoSyncGitTemplates.RootPath`. | `AutoSync/AutoSyncGitTemplates.cs`, `AutoSync/AutoSyncGitActions.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs` |
| `Documents/RiryTools/iconcap presets/` | `<name>.json` IconCap presets (size, camera settings, background, capture options, effect stack). Path is `IconCapFiles.PresetFolder`. | `IconCap/IconCapFiles.cs`, `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `Documents/RiryTools/ImportPresets/<configuration>/` | `*.preset` and optional `config.json`. Path is `ImportPresetLibrary.RootPath`. | `ImportPresets/ImportPresetLibrary.cs`, `ImportPresets/ImportPresetPreferences.cs`, `UI/ImportPresets/ImportPresetFolderWindow.cs`, `UI/ImportPresets/ImportPresetSaveWindow.cs` |
| `Documents/RiryTools/ImportPresets/menu_icon.preset` | Menu Tools' icon preset: a TextureImporter preset with every max size at `256`, made from the first oversized icon's importer when missing; only its max size settings are applied. Path is `MenuToolIconPreset.PresetPath`. | `VRChat/MenuTool/MenuToolIconPreset.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `Documents/RiryTools/ImportPresets/preferences.yml` | Import Presets tags (`tags:` list). Path is `ImportPresetPreferences.FilePath`. | `ImportPresets/ImportPresetPreferences.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `Documents/RiryTools/migrator/types.yml` | Avatar Migration Assistant's type list (categories of entries: id, label, common, enabled, sdk, rule, types); copied from the package's `AvatarMigrator/types.yml` when missing, then read from here. Path is `AvatarMigratorCopyTypes.FilePath`. | `AvatarMigrator/AvatarMigratorCopyTypes.cs`, `UI/AvatarMigrator/AvatarMigratorWindow.cs` |
| `Documents/RiryTools/Mockups/` | `<name>.yml` saved mockups (annotated; Unity never compiles them). Path is `MenuMockup.Folder`. | `Debug/MenuMockup.cs`, `UI/Debug/MenuMockupComponentsWindow.cs`, `UI/Debug/MenuMockupNameWindow.cs` |

## Git names (AutoSync)

| Name | Use | Scripts |
|---|---|---|
| `.gitignore` lines | `/<path>`, pattern characters escaped; added by **Large Files**. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| Branch | The checked-out branch AutoSync commits to. | `AutoSync/AutoSyncJob.cs` |
| Commit contents | The saved `.unity` and `.prefab` files and their `.meta` files. | `AutoSync/AutoSyncJob.cs` |
| Commit title | `RiryTools.AutoSync.TitleTemplate` filled in; by default the time of the save, `yyyy-MM-dd'T'HH:mm:sszzz` (`2026-10-30T14:05:12+01:00`). Format is `AutoSync.TimeFormat`. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncJob.cs` |
| Conflict branch | `autosync-conflict-<yyyyMMdd-HHmmss>`: made at HEAD and checked out when rebasing onto the remote's new commits conflicts (the rebase is aborted first); pushed instead of the original branch. | `AutoSync/AutoSyncJob.cs`, `AutoSync/AutoSync.cs`, `UI/AutoSync/AutoSyncConflictNotice.cs` |
| Default templates | `Unity.gitignore` and `Unity.gitattributes`. Name is `AutoSyncGitTemplates.DefaultName`. | `AutoSync/AutoSyncGitTemplates.cs`, `AutoSync/AutoSyncGitActions.cs` |
| Fallback identity | `RiryTools AutoSync` / `autosync@localhost`, only when `user.name` or `user.email` is unset. | `AutoSync/AutoSyncJob.cs` |
| LFS query | HTTP(S): `POST <remote>.git/info/lfs/objects/batch` (or `remote.<name>.lfsurl`, `lfs.url`), one all-zero object. SSH: `git-lfs-authenticate <path> download` with `core.sshCommand` or `ssh`. | `AutoSync/AutoSyncGitLfsQuery.cs` |
| Reflog message | `RiryTools AutoSync: drop large files`. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| Remote | The one chosen in Configuration; **Add Remote** adds `origin` (`AutoSyncGitRemotes.DefaultName`). | `AutoSync/AutoSyncGitRemotes.cs`, `AutoSync/AutoSyncJob.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.cs` |

### Git environment variables and arguments

| Name | Use | Scripts |
|---|---|---|
| `-c core.fsync=committed` | Argument for AutoSync commits. | `AutoSync/AutoSyncJob.cs` |
| `-c core.quotepath=off` | Argument on every Git command. | `Components/GitRunner.cs` |
| `-c rebase.autoStash=true` | Argument for the rebase onto the remote's new commits before a push: other changed files are stashed and put back. | `AutoSync/AutoSyncJob.cs` |
| `GIT_AUTHOR_*`, `GIT_COMMITTER_*` | The fallback identity. | `AutoSync/AutoSyncJob.cs` |
| `GIT_LITERAL_PATHSPECS=1` | Saved paths are not patterns. | `AutoSync/AutoSyncJob.cs`, `AutoSync/AutoSyncGitLargeFiles.cs`, `ImportPresets/ImportPresetAnimationExtraction.cs` |
| `GIT_TERMINAL_PROMPT=0` | Set on every command: Git never waits on a terminal prompt; sign-in goes through Git's credential manager. | `Components/GitRunner.cs` |

## GUIDs and build symbols

Every file and folder in `com.riry.rirytools/` and `com.riry.ftemulator/` has a `.meta` with a fixed GUID.
Moving or renaming a file must move its `.meta` with it.

| Name | Use | Scripts |
|---|---|---|
| `0756bf5a933b437db4405f7a01a55498` | GUID of `AvatarMigrator/types.yml.meta`: Avatar Migration Assistant's type list template, found by GUID (`AvatarMigratorCopyTypes.TemplateGuid`). | `AvatarMigrator/AvatarMigratorCopyTypes.cs` |
| `311b68250f50442e9fd0792f0665a214` | GUID of the folder `com.riry.ftemulator/Editor/FTEmulator/MappingSchemas` (its `.meta`): FTEmulator's tracking key schema, found by GUID (`FTEmulatorCatalog.SchemaFolderGuid`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorCatalog.cs` |
| `762a0ef7a65b45ab944c36b36148869d` | GUID of the folder `com.riry.ftemulator/Editor/FTEmulator/HardwareProfiles`: FTEmulator's hardware profiles (`FTEmulatorCatalog.HardwareFolderGuid`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorCatalog.cs` |
| Assembly `Riry.RiryTools.Editor` | Editor only, auto-referenced. All tools except the VRChat window. | `Riry.RiryTools.Editor.asmdef` |
| Assembly `Riry.RiryTools.Editor.FTEmulator` | FTEmulator, in its own package `com.riry.ftemulator`: logic, windows and its menu entry. Editor only; references `Riry.RiryTools.Editor`, `VRCSDK3A.dll` and `VRCSDKBase.dll`; needs `RIRYTOOLS_VRC_CONSTRAINTS`. | `com.riry.ftemulator/Editor/Riry.RiryTools.Editor.FTEmulator.asmdef` |
| Assembly `Riry.RiryTools.Editor.FTEmulator.GestureManager` | FTEmulator's output `FTEmulatorGestureManager`, built against Gesture Manager 3.9.9. Editor only; references `Riry.RiryTools.Editor`, `Riry.RiryTools.Editor.FTEmulator`, Gesture Manager's assemblies, `VRCSDK3A.dll` and `VRCSDKBase.dll`; needs `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER`. | `com.riry.ftemulator/Editor/FTEmulator/GestureManager/Riry.RiryTools.Editor.FTEmulator.GestureManager.asmdef` |
| Assembly `Riry.RiryTools.Editor.GestureManager` | `GestureManagerRadial`, built against Gesture Manager 3.9.9. Editor only; references Gesture Manager's assemblies, `VRCSDK3A.dll` and `VRCSDKBase.dll`; needs `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER`. | `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef`, `com.riry.ftemulator/Editor/FTEmulator/GestureManager/Riry.RiryTools.Editor.GestureManager.asmref` |
| Assembly `Riry.RiryTools.Editor.VRChat` | Constraint Tools window (Train, Bezier and Insertable prop tabs), Menu Tools, and their menus. Editor only; needs `VRC.Dynamics.dll`, `VRC.SDK3.Dynamics.Constraint.dll`, `VRC.SDK3.Dynamics.Contact.dll`, `VRC.SDK3.Dynamics.PhysBone.dll`, `VRCSDK3A.dll` and `VRCSDKBase.dll`. | `VRChat/Riry.RiryTools.Editor.VRChat.asmdef` |
| `e173195361fc4488ba7d04cd43776d4a` | GUID of the folder `com.riry.ftemulator/Editor/FTEmulator/AvatarProfiles`: FTEmulator's avatar profiles (`FTEmulatorCatalog.AvatarFolderGuid`). | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorCatalog.cs` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.FTEmulator")` | Lets FTEmulator's package use RiryTools' logging, paths, Main Menu host, menu paths and Stop all tool scripts. | `AssemblyInfo.cs` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.FTEmulator.GestureManager")` | Lets FTEmulator's Gesture Manager output implement `IFTEmulatorOutput`. | `com.riry.ftemulator/Editor/AssemblyInfo.cs` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.GestureManager")` | Lets the Gesture Manager bridge implement `IMenuToolRadial`. | `VRChat/AssemblyInfo.cs` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.VRChat")` | Lets the VRChat assembly use internal types and menu paths. | `AssemblyInfo.cs` |
| `RIRYTOOLS_FTEMULATOR` | Defined in `Riry.RiryTools.Editor` when the package `com.riry.ftemulator` is in the project (any version). With it and `RIRYTOOLS_VRC_CONSTRAINTS`, the FTEmulator menu entry is the package's; without either, RiryTools' entry and the Main Menu's FTEmulator tab say why FTEmulator is not available (`RiryToolsMenuBar.FTEmulatorUnavailable`). | `Riry.RiryTools.Editor.asmdef`, `UI/RiryToolsMenuBar.cs` |
| `RIRYTOOLS_GESTURE_MANAGER` | Defined when `vrchat.blackstartx.gesture-manager` is 3.9.0 or newer. Compiles `Riry.RiryTools.Editor.GestureManager` (Menu Tools' radial preview) and `Riry.RiryTools.Editor.FTEmulator.GestureManager` (FTEmulator's output); without it the Menu manager says to install Gesture Manager, and FTEmulator shows the Gesture Manager not found message. | `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef`, `VRChat/MenuTool/GestureManager/GestureManagerRadial.cs`, `com.riry.ftemulator/Editor/FTEmulator/GestureManager/FTEmulatorGestureManager.cs`, `com.riry.ftemulator/Editor/FTEmulator/GestureManager/Riry.RiryTools.Editor.FTEmulator.GestureManager.asmdef` |
| `RIRYTOOLS_VRC_BIT_WIDTH` | Defined when `com.vrchat.avatars` is 3.10.6 or newer. Menu Tools reads each parameter's synced width and flipped flag (`GetBitWidthByType`, `flippedRepresentation`; `MenuToolScanner.WidthOf` / `FlippedOf`); older SDKs count 8 bits. FTEmulator reads the descriptor's native face tracking settings and follows `VRC_AnimatorFaceTrackingBlendshapesSet.OnApplySettings` only with it (native face tracking arrived in 3.10.6). | `VRChat/Riry.RiryTools.Editor.VRChat.asmdef`, `VRChat/MenuTool/MenuToolScanner.cs`, `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorNativeDescriptor.cs`, `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorNativeSetWeights.cs` |
| `RIRYTOOLS_MENUTOOL_MA` | Never defined: comments out Menu Tools' Modular Avatar code (scanning MA menu installers, items, parameters and Merge Animators, and the MA edits) until MA support returns. | `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolScanner.ModularAvatar.cs`, `VRChat/MenuTool/MenuToolModel.cs`, `VRChat/MenuTool/MenuToolPlan.cs`, `VRChat/MenuTool/MenuToolPlanner.cs`, `VRChat/MenuTool/MenuToolApply.cs`, `VRChat/UI/MenuTool/MenuToolIcons.cs` |
| `RIRYTOOLS_VRC_CONSTRAINTS` | Defined when `com.vrchat.avatars` is 3.7.0 or newer. Compiles `Riry.RiryTools.Editor.VRChat`; without it the menus show the **VRCSDK not found** popup. | `Riry.RiryTools.Editor.asmdef`, `VRChat/Riry.RiryTools.Editor.VRChat.asmdef`, `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef`, `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsRightClickMenu.cs` |
| Shader `Hidden/RiryTools/IconCap/Draw` | IconCap alpha pass and Flat lighting; keyword `ICONCAP_CLIP`. | `IconCap/Shaders/IconCapDraw.shader`, `IconCap/IconCapRenderer.cs` |
| Shader `Hidden/RiryTools/IconCap/Effects` | IconCap effect stack, composite and HDRI unwrap passes. | `IconCap/Shaders/IconCapEffects.shader`, `IconCap/IconCapEffects.cs` |
| Type and package names read by FTEmulator | From `AvatarProfiles/*.json` signatures: components `Pawlygon.FaceTracking.PawlygonFaceTracking` and `Pawlygon.FaceTracking.PawlygonFTParameters`; packages `net.pawlygon.vrc-facetracking` and `adjerry91.vrcft.templates`. For the diagnostics export also `com.riry.rirytools`, `com.vrchat.avatars`, `com.vrchat.base`, `vrchat.blackstartx.gesture-manager`, `com.vrcfury.vrcfury`, `nadena.dev.modular-avatar`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorAvatarScanner.cs`, `com.riry.ftemulator/Editor/FTEmulator/FTEmulator.cs` |
| Type names read by Menu Tools | `VF.Model.VRCFury` (features `Toggle`, `Puppet`, `FullController`, `MoveMenuItem`, `ReorderMenuItem`, `SetIcon`), `nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller`, `...MenuItem`, `...MenuGroup`, `...MenuInstallTarget`, `...Parameters`, `...MergeAnimator`, `VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks`. Looked up by name, so neither VRCFury nor Modular Avatar is needed to compile. | `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolBudget.cs`, `VRChat/MenuTool/MenuToolApply.cs` |

## Tunable constants

### AutoSync, Git and Debug

| Name | Use | Scripts |
|---|---|---|
| `AutoSyncGitLargeFiles.ProxyLimitBytes` | 100 MiB: Cloudflare's proxied upload limit, used for the ! button and held pushes. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| `AutoSyncGitLfsQuery.TimeoutMs` | `30000`: LFS query timeout. | `AutoSync/AutoSyncGitLfsQuery.cs` |
| `AutoSyncGitTemplates.DefaultAttributes` | Built-in LFS template text, saved as `UnityLFS`. | `AutoSync/AutoSyncGitTemplates.cs` |
| `AutoSyncGitTemplates.UnityAttributesUrl` | The gitattributes project's `Unity.gitattributes`: default `.gitattributes` template, saved as `Unity`. | `AutoSync/AutoSyncGitTemplates.cs` |
| `AutoSyncGitTemplates.UnityIgnoreUrl` | GitHub's `Unity.gitignore`: default `.gitignore` template, saved as `Unity`. | `AutoSync/AutoSyncGitTemplates.cs` |
| `AutoSyncJob.PushTimeoutMs` | `600000`: push timeout, long enough to sign in. | `AutoSync/AutoSyncJob.cs` |
| `AutoSyncJob.TimeoutMs` | `120000`: timeout per Git command. | `AutoSync/AutoSyncJob.cs` |
| `GitNotInstalledException.DownloadUrl` | `https://git-scm.com/install/`: the **Download Git** link. | `Components/GitNotInstalledException.cs`, `AutoSync/AutoSyncGitActions.cs` |
| `GitRunner.TimedOut` | `-1`: exit code reported for a timed-out or cancelled command. | `Components/GitRunner.cs` |
| `GitState.CacheSeconds` | `5.0`: how long Git and repository state is cached. | `Components/GitState.cs` |
| `GuidToolBrokenReferenceMonitor.Threshold` / `WindowSeconds` | `3` in `30.0` s: missing-reference log lines that trigger the prompt. | `GuidTools/GuidToolBrokenReferenceMonitor.cs` |
| `GuidToolBrokenReferenceMonitor.AnimationLocal` | `Broken text PPtr in file(<…>.controller / .overrideController / .anim). Local file identifier`: log lines left out of the prompt's count, since they name an object missing inside the same controller or clip (such as a state's Motion that lost its clip's GUID), not a missing asset. | `GuidTools/GuidToolBrokenReferenceMonitor.cs` |
| `ScriptLoadGuard.CaptureSeconds` | `5.0`: errors this long before a prefab opens count as suspects. | `Debug/ScriptLoadGuard.cs` |
| `ScriptLoadGuard.RecentLimit` | `50`: logged errors kept. | `Debug/ScriptLoadGuard.cs` |

### GUID Tools

| Name | Use | Scripts |
|---|---|---|
| `GuidToolLog.ActiveLogLimit` / `ArchiveLimit` | 10 MiB / 100 MiB: log rotation and archive trim. | `GuidTools/GuidToolLog.cs` |
| `GuidToolReferenceCache.CacheExtension` / `CacheMagic` / `CacheVersion` | `.rguiddb` / `RGIX` / `1`: cache file format. | `GuidTools/GuidToolReferenceCache.cs` |
| `GuidToolReferenceCache.CacheNameLength` | `40`: readable part of a cache file name. | `GuidTools/GuidToolReferenceCache.cs` |
| `GuidToolReferenceRepair.MaxNameMatches` | `5`: name matches offered per old name. | `GuidTools/GuidToolReferenceRepair.cs` |
| `GuidToolReferenceRepair.Title` | `Broken References`: window and dialog title. | `GuidTools/GuidToolReferenceRepair.cs`, `GuidTools/GuidToolBrokenReferenceMonitor.cs`, `GuidTools/GuidToolBrokenReferenceSaveGuard.cs`, `UI/GuidTools/GuidToolBrokenReferenceWindow.cs`, `UI/GuidTools/GuidToolBrokenSaveWindow.cs` |
| `GuidToolRegenerate.GuidLength` | `32`: hex digits in a GUID. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolRegenerate.Scan.cs`, `GuidTools/GuidToolRegenerate.Patch.cs`, `GuidTools/GuidToolReferenceCache.cs`, `GuidTools/GuidToolReferenceRepair.cs`, `GuidTools/GuidToolRemapLedger.cs` |
| `GuidToolRegenerate.MaxDirectoryDepth` | `64`: stops symbolic link loops. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolRegenerate.Scan.cs` |
| `GuidToolRegenerate.ScanBlockSize` | 256 KiB: read block for scanning. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolRegenerate.Scan.cs`, `GuidTools/GuidToolReferenceRepair.cs`, `UI/GuidTools/GuidToolRegenerate.Prompts.cs` |
| `GuidToolRegenerate.TextExtensions` | List of files scanned for references. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolRegenerate.Scan.cs` |
| `GuidToolRegenerate.UnitySerializedExtensions` | List of files that must be text to be patched. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolRegenerate.Scan.cs`, `GuidTools/GuidToolReferenceRepair.cs`, `UI/GuidTools/GuidToolBrokenReferenceWindow.cs`, `UI/GuidTools/GuidToolRegenerate.Prompts.cs` |
| `GuidToolRemapLedger.MaxChain` | `64`: longest old-to-new GUID chain followed. | `GuidTools/GuidToolRemapLedger.cs` |

### Import Presets and Better FBX

| Name | Use | Scripts |
|---|---|---|
| `ImportPresetAnimations.UndoName` | `Extract Animation`: Undo step of the Extract button's extraction (replaced clips are recorded; new files are not removed by Undo). Automatic extractions (first import, re-export, Folder Rules Apply) leave no Undo step. | `ImportPresets/ImportPresetAnimations.cs` |
| `ImportPresetBetterFBX.SourceExtension` | `blend`: the only file type Blender exports. | `ImportPresets/ImportPresetBetterFBX.cs`, `ImportPresets/ImportPresetFolderRules.cs`, `ImportPresets/ImportPresetPostprocessor.cs`, `UI/ImportPresets/ImportPresetFolderWindow.cs` |
| `ImportPresetBetterFBX.SuccessMarker` | `RIRYTOOLS_BETTER_FBX_OK`: printed by the export script on success. | `ImportPresets/ImportPresetBetterFBX.cs` |
| `ImportPresetBetterFBX.TimeoutSeconds` | `600`: Blender run limit. | `ImportPresets/ImportPresetBetterFBX.cs` |
| `ImportPresetDropChoices.PickLifetime` | `60` s: how long a preset picked with the scroll wheel during a drag is used for the dropped file's import. | `ImportPresets/ImportPresetDropChoices.cs` |
| `ImportPresetFolderRules.Labels` | `Image files`, `Model files`, `Audio files`, `Video files`, `Font files`: file type names; other importers show as `<Type> files`. | `ImportPresets/ImportPresetFolderRules.cs` |
| `ImportPresetFolderWindow.MaxSuggestions` | `8`: folder suggestions in Set Folder Import Presets. | `UI/ImportPresets/ImportPresetFolderWindow.cs` |
| `ImportPresetLibrary.ConfigFileName` / `PresetExtension` | `config.json` / `.preset`: configuration files. | `ImportPresets/ImportPresetLibrary.cs` |
| `ImportPresetLibrary.ReservedNames` | `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`: names Windows refuses. | `ImportPresets/ImportPresetLibrary.cs` |
| `ImportPresetMarker.BetterFbxKey` | `betterfbx`: marker key for Better FBX. | `ImportPresets/ImportPresetMarker.cs` |
| `ImportPresetMarker.FileName` | `ImportPreset.md`: folder marker. | `ImportPresets/ImportPresetMarker.cs`, `ImportPresets/ImportPresetFolderRules.cs`, `ImportPresets/ImportPresetPostprocessor.cs` |

### Main Menu, logging and shared UI

| Name | Use | Scripts |
|---|---|---|
| `RiryLog.Format` | `[RiryTools/<tool>] <message>` on every Console line. Tools: Attach To, AutoSync, Avatar Migration, Blendshape Sync, Clean Animation Keys, Constraint Tools, Find and Replace, Find Null references, FTEmulator, GUID Tools, IconCap, Import Presets, Material Cleaner, Menu Mockup, Menu Tools, Script Load Guard, Stop Auto Game. A line about an object or asset passes it, so clicking the line pings it. | `Components/RiryLog.cs` |
| `RiryToolsMainMenu.TabBorder` / `SubTabGap` / `TopMargin` | `2` / `2` / `2` px: tab outline thickness; space between a tab strip and the sub-tab strip under it; space above the main tabs. | `UI/RiryToolsMainMenu.cs`, `UI/Debug/MenuMockupPreview.cs`, `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `RiryToolsMainMenu.MainTabs` / `SubTabs` / `ToolTabs` | `0` / `1` / `2`: tab strip levels (`TabStrip`'s `level`). MainTabs: the Main Menu's tabs. SubTabs: General Tools' and Animator Tools' sub-tabs, Menu Tools' tabs, FTEmulator debug window's tabs, Menu Mockup's Tabs block. ToolTabs: Import Presets' sub-tabs and Constraint Tools' tabs (hosted or in its own window). | `UI/RiryToolsMainMenu.cs`, `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs`, `UI/Debug/MenuMockupPreview.cs`, `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorDebugWindow.cs` |
| `RiryToolsMainMenu.TabMinHeights` | `48` / `36` / `24` px: minimum tab height per level; a strip whose names need two lines at `TabMinWidth` grows to fit. | `UI/RiryToolsMainMenu.cs`, `UI/Debug/MenuMockupPreview.cs` |
| `RiryToolsMainMenu.ActiveTabs` / `ActiveTabText` | Active tab colour per level, both skins: `#96D2FF` light blue (MainTabs), `#5FA0E1` darker blue (SubTabs), `#50CDA5` seafoam green (ToolTabs); active tab text `#141414`. | `UI/RiryToolsMainMenu.cs`, `UI/Debug/MenuMockupPreview.cs` |
| `RiryToolsMenuBar.DocumentationLink` | `https://github.com/Rionquosue/riryunityworkflow`: opened by **Documentation**. Change it when the wiki is up. | `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |
| Settings Undo names | `Constraint Train settings`, `Bezier settings`, `Insertable prop settings`, `Axis detector settings`, `Blendshape Sync settings`, `Clean Animation Keys settings`, `Extract Animation settings`, `IconCap settings`: Undo steps of window settings edits, through `RiryToolGUI.Set`. | `UI/Components/RiryToolGUI.cs`, `UI/BlendshapeSync/BlendshapeSyncWindow.UI.cs`, `UI/BlendshapeSync/BlendshapeSyncJobList.cs`, `UI/AnimatorTools/CleanAnimationKeysWindow.cs`, `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs`, `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.UI.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `StopAutoGame.HandleDelayMs` | `100`: delay after Play Mode starts before focus is restored. | `Utilities/StopAutoGame.cs` |

### Attach To

| Name | Use | Scripts |
|---|---|---|
| `AttachTo.CommonBones` | Head, Chest, Hips, Left/Right Hand, Left/Right Foot, Left/Right Upper Leg: bones listed before any added with **Add bone**. | `Utilities/AttachTo.cs` |
| `AttachTo.DialogNames` | `10`: names a dialog lists before "and n more". | `Utilities/AttachTo.cs` |
| `AttachTo.UndoName` | `Attach To`: popup title; Undo group `Attach To <bone>`. | `Utilities/AttachTo.cs`, `UI/Utilities/AttachToWindow.cs` |
| `AttachToBones.CentreNames` / `SidedNames` | e.g. `chest`, `head`, `hips`/`pelvis`; `hand`/`wrist`, `upperleg`/`thigh`, `foot`: name matching from a name without its side (`L`, `R`, `Left`, `Right`) to its bone. | `Utilities/AttachToBones.cs` |
| `AttachToBones.IgnoredParts` | `j`, `bip`, `c`, `def`, `mixamorig`, `bone`: name parts dropped before name matching. | `Utilities/AttachToBones.cs` |

### Invert Global Position

| Name | Use | Scripts |
|---|---|---|
| `InvertGlobalPosition.UndoName` | `Invert Global Position`: the undo step, the dialogs' title and the menu entry's name. | `Utilities/InvertGlobalPosition.cs`, `UI/Utilities/InvertGlobalPositionPrompts.cs`, `UI/RiryToolsRightClickMenu.cs` |
| `InvertGlobalPosition.DescriptorType` | `VRC.SDK3.Avatars.Components.VRCAvatarDescriptor`: found by name to pick the avatar root (else the nearest Animator). | `Utilities/InvertGlobalPosition.cs` |
| `RiryLog.InvertGlobalPosition` | `Invert Global Position`: its Console lines' tool name. | `Components/RiryLog.cs`, `UI/Utilities/InvertGlobalPositionPrompts.cs` |

### Find scripts that reference this

| Name | Use | Scripts |
|---|---|---|
| `FindReferencingScripts.Title` | `Find scripts that reference this`: the menu entry's name and the popup's title. | `Utilities/FindReferencingScripts.cs`, `UI/Utilities/FindReferencingScriptsWindow.cs`, `UI/RiryToolsRightClickMenu.cs` |
| `RiryLog.FindReferencingScripts` | `Find scripts that reference this`: its Console lines' tool name. | `Components/RiryLog.cs` |

### Avatar Migration Assistant

| Name | Use | Scripts |
|---|---|---|
| `AvatarMigratorCopyRun.ArmatureNames` | `armature`, `skeleton`, `root`, `rig`: names treated as the armature, besides the parent of the hips. | `AvatarMigrator/AvatarMigratorCopyRun.TypeRules.cs` |
| `AvatarMigratorCopyRun.ConverterClass` / `ConverterMethod` | `AvatarDynamicsSetup` / `DoConvertUnityConstraints`: the VRChat SDK's constraint converter, found by name. | `AvatarMigrator/AvatarMigratorCopyRun.UpgradeConstraints.cs` |
| `AvatarMigratorCopyRun.ScaleDecimals` | `8`: decimal places of every scale-corrected value. | `AvatarMigrator/AvatarMigratorCopyRun.CorrectScale.cs` |
| `AvatarMigratorMatchObjects.TieEpsilon` | `1e-7`: candidates this close in distance are a tie (left unmatched). | `AvatarMigrator/AvatarMigratorMatchObjects.cs` |
| `AvatarMigratorSettings.DefaultTolerance` | `0.001`: structure match distance in avatar-root space. | `AvatarMigrator/AvatarMigratorSettings.cs`, `AvatarMigrator/AvatarMigratorMatchObjects.cs` |

### Blendshape Sync and Clean Animation Keys

| Name | Use | Scripts |
|---|---|---|
| `BehaviourFindReplace.RegexTimeout` | `200` ms: the longest Find and Replace's Regex match may run on one string. | `VRChat/AnimatorTools/BehaviourFindReplace.cs` |
| `BlendshapeSyncJobList.WideRow` | `570`: window width below which a Main Menu Blendshape Sync job row puts its buttons on a second line. | `UI/BlendshapeSync/BlendshapeSyncJobList.cs` |
| `BlendshapeSyncWindow.Interval` | `0.5` s: automatic sync period. | `UI/BlendshapeSync/BlendshapeSyncWindow.cs`, `UI/BlendshapeSync/BlendshapeSyncWindow.UI.cs`, `BlendshapeSync/BlendshapeSyncJob.cs` |
| `BlendshapeSyncWindow.SyncUndoName` / `RestoreUndoName` | `Sync Blendshape Weights` / `Restore Original Blendshape Weights`: Undo names; also mark the tool's own Undo steps. | `UI/BlendshapeSync/BlendshapeSyncWindow.cs`, `BlendshapeSync/BlendshapeSyncWindow.Sync.cs` |
| `BlendshapeNames.CurvePrefix` | `blendShape.`: what a blendshape curve's property name starts with. | `Components/BlendshapeNames.cs`, `AnimatorTools/CleanAnimationKeys.cs`, `AvatarMigrator/AvatarMigratorRetargetAnim.cs` |
| `CleanAnimationKeys.BoneValueDecimalPlaces` | `3`: rounding for bone endpoint comparison. | `AnimatorTools/CleanAnimationKeys.cs` |
| `ControllerCleanerWindow.Title` | `Controller Cleaner`: window title, dialogs and Undo name of Clean (one undo group per click, also for the clean offered after a GUID repair; `ControllerCleanerClean` records the controller, its state machines and states, and destroys unused sub-assets with Undo). | `UI/AnimatorTools/ControllerCleanerWindow.cs`, `Components/ControllerCleanerClean.cs`, `UI/GuidTools/GuidToolBrokenReferenceWindow.cs` |
| `MaterialCleaner.Title` | `Material Cleaner`: window title and Undo name of Clean (one undo step per click; removes a material's texture, float and color entries its shader doesn't have). | `Utilities/MaterialCleaner.cs`, `UI/Utilities/MaterialCleanerWindow.cs` |
| `CleanAnimationKeys.DisplayName` | `Clean Animation Keys`: dialog, window and Undo name. | `AnimatorTools/CleanAnimationKeys.cs`, `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysButton.ButtonElementName` | `rirytools-clean-animation-keys`: UI Toolkit name of the **Clean Keys** button. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysButton.ButtonLeft` / `ButtonBottom` | `170` / `1`: **Clean Keys** button in the Animation window's hierarchy-pane bottom bar, right of Dopesheet and Curves. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysButton.ScanIntervalSeconds` | `0.5`: how often the focused Animation window is checked. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysWindow.AnimationListHeight` | `150`: height of the scrolling Animations list. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysWindow.MaxAnimations` | `64`: animations in the list. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysWindow.MaxSuggestions` | `8`: whitelist suggestions shown. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |

### Constraint Tools

| Name | Use | Scripts |
|---|---|---|
| Axis detector collision tag | `RiryTilt_<8 hex>`: one per Build, shared by its sender and receivers (receivers take only the same avatar's contacts). | `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs` |
| Axis detector objects | `TiltDetector_<source>` under Relative to (the receivers: `Right`, `Up`, or `Top`, `BottomLeft`, `BottomRight`) and `TiltSender_<source>` under Source (the sender). Names are `TiltDetectorBuilder.RootPrefix` / `SenderPrefix`. | `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs` |
| Axis detector parameters | `Tilt/Right`, `Tilt/Up` (Dual axis) or `Tilt/Top`, `Tilt/BottomLeft`, `Tilt/BottomRight` (Three axis): the Proximity receivers' parameters, `TiltDetectorBuilder.ParameterPrefix` + the receiver name; the axis detector templates read them. | `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs`, `VRChat/UI/ConstraintTools/TiltDetectorWindow.cs` |
| Bezier rig objects | `Anchor Start`, `Anchor End`, `Handle Start`, `Handle End`, `Samples/Sample NN`, `Twist/Twist NN`: children of `BezierRig_<start>` (handles under their anchors with **Handles follow anchors**). | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierCurve.ArcSteps` / `FitRounds` / `Regularization` | `256` / `4` / `1e-6`: length table steps, fit rounds, and the pull of P1/P2 to straight-line handles. | `VRChat/ConstraintTools/ConstraintBezier/ConstraintBezierCurve.cs` |
| `ConstraintBezierWindow.MinAimDistance` | `1e-5`: shorter directions are not aimed along. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierWindow.RigPrefix` / `MechanismPrefix` | `BezierRig_` / `Bezier_`: the rig object and the mechanism chain, each a sibling of the chain start named prefix + its name. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierWindow.UndoName` | `Bezier rig`: Undo group of Generate rig; Remove rig is `Remove Bezier rig`. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintToolWindow.Title` / `Tabs` | `Constraint Tools` / `Train`, `Bezier`, `Insertable prop`, `Dual axis detector`, `Three axis detector`: the Constraint Tools window and its tabs (the Main Menu hosts this window, so these are the only tab list); minSize 420 x 500. | `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs` |
| `ConstraintTrainChains.RestPrefix` / `ChainPrefix(n)`, `ConstraintToolChains.EndpointSuffix` | `Rest_` / `Phys<n>_` / `_End`: generated chain names (`Rest_` chains are no longer generated, only still recognised as generated). | `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainChains.cs`, `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`, `VRChat/Components/ConstraintToolChains.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs` |
| `ConstraintTrainPlanner.MaxChains` | `5`: physics chains. | `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPlanner.cs`, `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs` |
| `ConstraintTrainWindow.UndoName` | `Constraint Train`: Undo group name. | `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs` |
| Insertable prop layers | `IP_<id> Decide`, `Pose`, `Hand`, `Blend`: appended to the chosen controller, else the avatar's FX. | `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| Insertable prop objects | `IP_<id>/Prop`, `Rig/Anchor/End`, `Rig/GripSense/Marker`, `Rig/Frame Remote`, `Frame Wearer L`, `Frame Wearer R`, `Frame Sheath`, `Rig/HandFrame`, `Rig/Hand`, `World/Body`, `World/Follower` (PC), `Drop/RayOrigin`, `Landed`, `DropYaw/Lying` (Android); on the prop `IP Grip`, `IP Tip`; on the socket `IP_<id> Seat`, `Opening`, `Entry`. Generated by `InsertablePropRig.Build`. | `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.cs` |
| Insertable prop parameters | `IP_<id>_IsGrabbed`, `IP_<id>_Grip_SenderType`, `_SenderLocalPositionX/Y/Z`, `_PointSeparation`, `_CollisionTag_FingerLittleL/R`, `IP_<id>_Sheath`, `IP_<id>_State` (synced Int: Seated 0, Held 1, Inserting 2, Dropped 3), `IP_<id>_One`: Animator and Expression parameters. | `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `InsertablePropAnimator.WeakSeparation` / `InsideSheath` | `0.01` m / `0.001`: grip reading too weak below this; tip inside the capture radius above this proximity. | `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `InsertablePropMath.MaxStretch` / `AnchorLength` / `EntryMargin` / `SettledFraction` | `5` / `1.7` m / `0.03` m / `0.99`: PhysBone stretch cap (SDK), root-to-end rest distance (reach about 10 m), entry point margin, slide step end. | `VRChat/ConstraintTools/InsertableProp/InsertablePropMath.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `InsertablePropRig.RootPrefix` / pair ID | `IP_` / `<avatar>_<prop>` (non-alphanumerics as `_`): names of the pair's objects, contact tag (`IP_<id>_Tip`), parameters, layers and clip folder. | `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.cs` |
| `InsertablePropRig.TipRadius` / `GripReceiverRadius` / `GrabRadius` / `PickupLayer` | `0.01` / `0.15` / `0.06` m / `13`: tip sender, grip receiver, PhysBone grab radius, body layer. | `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `InsertablePropWindow.Settings` defaults | Capture radius `0.05` m, grip smoothing `0.08` s, insert smoothing `0.15` s, stiffness `1500`: Insertable prop tab options. | `VRChat/UI/ConstraintTools/InsertablePropWindow.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs` |
| `TiltDetectorBuilder.DefaultOffset` / `RadiusScale` / `SenderRadius` | `0.05` m / `2` / `0.001` m: receiver distance from Source's rest position, receiver radius as a multiple of it (so each reads 0.5 at rest), and the sender's radius. | `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs` |
| `TiltDetectorBuilder.UndoName` | `Axis detector`: Build's undo step. | `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs` |

### IconCap

| Name | Use | Scripts |
|---|---|---|
| `IconCapRenderer.MaxSize` | `4096`: largest capture size. | `IconCap/IconCapRenderer.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapRenderer.SensorHeight` | `24` mm: full-frame sensor for the 35, 50 and 85mm lenses. | `IconCap/IconCapRenderer.cs`, `IconCap/IconCapData.cs` |
| `IconCapWindow.PrefixSuffixRow` / `JobRow` / `RestoreRow` | `460` / `440` / `420`: window widths below which Prefix/Suffix, the Job row's Add to job (Jobs > Current job) and Restore's buttons go on a second line. | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapWindow.PreviewInterval` | `0.1` s: shortest time between live preview renders. | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapWindow.PreviewMaxSide` / `PreviewMaxPixels` | `512` / `1024`: largest preview on screen / in pixels. | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapWindow.RowMargin` | `40`: scroll bar and margins left out of a job row's width; a longer row puts its folder, count and Run on a second line. | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapWindow.SmallThumbnail` / `LargeThumbnail` | `64` / `128`: Icon Generator source and result size (**Large previews**). | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `IconCapWindow.WideWidth` | `640`: window width from which the effect stack sits beside the preview. | `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |

### Menu Tools

| Name | Use | Scripts |
|---|---|---|
| `MenuToolAddMenuDialog.Size` / `ControlSize` | `380` x `112` / `380` x `200`: Menu Tools' Add menu and Add New Control dialogs (fixed size, centred on the main window). | `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs` |
| `MenuToolBudgetCount.MaxBits` / `MaxParameters` | `256` / `8192`: VRChat's synced bits and custom expression parameters. | `VRChat/MenuTool/MenuToolBudget.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolCompressor.BatchTime` | `0.1` s: VRCFury's time per compressed batch, used to pick between control type sets (a later set only if it halves the sync time). | `VRChat/MenuTool/MenuToolCompressor.cs` |
| `MenuToolCompressor.MaxCost()` | `256` (`MAX_PARAMETER_COST`), less `19` with DexProtect: VRCFury's limit for compression. | `VRChat/MenuTool/MenuToolCompressor.cs`, `VRChat/MenuTool/MenuToolBudget.cs` |
| `MenuToolIconPreset.MaxSize` / `PresetName` | `256` / `menu_icon`: the largest icon the VRChat SDK uploads, and the import preset that sets oversized icons' max size. | `VRChat/MenuTool/MenuToolIconPreset.cs`, `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolPlanner.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolScanner.MaxControls` | `8`: controls per menu (VRChat's limit); more are paged by VRCFury, otherwise flagged. | `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolPlanner.cs`, `VRChat/MenuTool/MenuToolPreviewMenus.cs` |
| `MenuToolWindow.AvatarFilter` / `PickerButton` | `t:VRCAvatarDescriptor` / `19`: the target field's object picker search (Hierarchy objects with a VRC Avatar Descriptor) and the width of the picker button it replaces. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolWindow.IconPicker` | `0x52694963`: control ID of the Assign Menu Icon object picker. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolWindow.MinWidth` / `MinHeight` | `640` / `760`: Menu Tools' own window; the Menu manager asks for this width in the Main Menu too. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolWindow.duplicatesOpen` / `largeIconsOpen` | [SerializeField] Whether the Menu manager's "Controls with the same name in one submenu" and "Icons over 256 px" boxes, under the radial, are open (both collapsed by default). | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolRichText.Tag` | `</?[A-Za-z#][^<>]*>`: a rich text tag in a menu control name, left out of the names Menu Tools shows (`MenuToolRichText.Plain`); renaming edits the name with its tags. | `VRChat/MenuTool/MenuToolRichText.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs`, `VRChat/UI/MenuTool/MenuToolManagerTree.cs`, `VRChat/UI/MenuTool/MenuToolBudgetTree.cs`, `VRChat/MenuTool/MenuToolScanner.cs` |
| `MenuToolScan.Duplicates` / `MenuToolDuplicate` | Controls sharing a name in one submenu: a label (`path: "name" (count)`) and the assets they come from (the menu asset, or the prefab asset holding the VRCFury or Modular Avatar component; none for a scene object outside a prefab). | `VRChat/MenuTool/MenuToolModel.cs`, `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| Writes box height (`MenuToolWindow.DrawBits`) | `120` px: the Writes foldout's scroll box, with every parameter write since the window opened or Reset menu state, one row each. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |

### FTEmulator

| Name | Use | Scripts |
|---|---|---|
| `FTEmulatorBlendshapeWriter.SameWeight` | `0.01`: a weight further than this from the one written has another owner and is not restored. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorBlendshapeWriter.cs` |
| `FTEmulatorCatalog.SchemaVersion` / `UnrestrictedId` | `1`: the `schema` every data file must have. `unrestricted`: the hardware profile with one control per tracking key. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorCatalog.cs` |
| `FTEmulatorDebugWindow.RowHeight` / `ChangedSeconds` | `20` px table rows; a row counts as changed for `2` s (**Changed only**). Minimum window size `960` x `420`. | `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorDebugWindow.cs` |
| `FTEmulatorDiagnostics.MaxCodes` | `128`: problem categories kept; each is counted, and logged once. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorDiagnostics.cs` |
| `FTEmulatorDnsPacket.MaxRecords` / `MaxJumps` | `64` records of a kind and `16` name compression jumps read from one multicast DNS packet. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorDnsPacket.cs` |
| `FTEmulatorExpression` limits | `MaxLength` 1024 characters, `MaxTokens` 256, `MaxInstructions` 256, `MaxNesting` 32, `MaxStack` 32, `MaxLiteralLength` 20, `MaxLiteral` 10000: the limits the SDK states for Parameter Behavior custom expressions. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorExpression.cs` |
| `FTEmulatorJson.MaxDepth` / `MaxLength` | `64` levels / `4194304` characters of JSON read. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorJson.cs` |
| `FTEmulatorNativeConfig` limits | `MaxMeshes` 16, `MaxSets` 128, `MaxBehaviors` 128, `MaxInputsPerBehavior` 32, `MaxTotalInstructions` 2048: native face tracking's limits. Input order: the 102 tracking keys, `Voice`, then `Viseme_sil` ... `Viseme_ou`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorNativeEvaluator.cs` |
| `FTEmulatorOscCodec` addresses | `/avatar/parameters/`, `/tracking/face/v1` (one blob of 102 bytes, byte / 255), `/avatar/change`, `/avatar/avatarfullfacetracking`. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscCodec.cs` |
| `FTEmulatorOscCodec` limits | `MaxBundleDepth` 4, `MaxMessagesPerPacket` 1024, `MaxArguments` 64, `MaxPacketLength` 65507. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscCodec.cs` |
| `FTEmulatorOscQuery.JsonService` / `NamePrefix` | `_oscjson._tcp.local` and `VRChat-Client-RiryFTEmulator-` (plus six characters): the service VRCFaceTracking looks for. Multicast DNS on `224.0.0.251:5353`; HTTP on `127.0.0.1`, a free port. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscQuery.cs` |
| `FTEmulatorOscQuery` timing | Announce every `15` s (three times a second apart at first), ask for tracking apps every `3` s, `30` s once one answered, HOST_INFO at most every `5` s per app, at most `8` apps. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscQuery.cs` |
| `FTEmulatorOscReceiver.MaxAddresses` | `2048`: parameter addresses kept pending between reads; the newest value per address wins. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscReceiver.cs` |
| `FTEmulatorParameterStore.GraceSeconds` / `FadeSeconds` | `0.25` s a Live value stays after input stops, then `0.2` s to fade to neutral; after that it is not driven. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorParameterStore.cs` |
| `FTEmulatorRecording.Extension` / `MaxPackets` | `.ftrec` / `2000000`: recording file extension, and the most packets a recording keeps or loads. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorRecording.cs` |
| `FTEmulatorSession.UserFaceTracking` / `UserEyeTracking` | The native status parameters, set by the session. `VerifyEvery` 30 frames and `VerifyTolerance` 0.02: written parameters read back. | `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSession.cs` |

## Menu paths

The number in brackets is the menu priority. Constraint Tools', Menu Tools',
Find and Replace's and FTEmulator's entries are defined in `VRChat/UI/RiryToolsVRChatMenus.cs`
when the VRChat SDK is present, and in `UI/RiryToolsMenuBar.cs` (showing the
**VRCSDK not found** popup) when it is not. Keep the priorities of both files in
step.

### Tools menu

Laid out like the Main Menu's tabs.

| Name | Use | Scripts |
|---|---|---|
| `Tools/RiryTools/Main Menu` | (1990) The dockable RiryTools window. Tabs (`RiryToolsMainMenu.Tabs`, all on one row): General Tools, Menu Tools, Animator Tools, Debug Tools, FTEmulator. General Tools' sub-tabs (`ToolsTabs`): Blendshape Sync, Import Presets, Constraint Tools, AutoSync, PopUp Tools. Documentation link at the bottom right; no Menu Mockup tab. | `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |
| `Tools/RiryTools/General Tools/Blendshape Sync` | (2000) Add Job: a new job in its popup. Path prefix is `RiryToolsMenuBar.GeneralTools`. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/Import Presets/Set Folder Import Presets` | (2010) The Set Folder Import Presets window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/Import Presets/Save Import Preset` | (2011) The Save Import Preset window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/Import Presets/Open Import Preset Library` | (2012) Opens the Import Preset library folder. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/Constraint Tools/Train` | (2020) The Constraint Tools window on its Train tab. The folder is written `Constraint\u00A0&Tools` (a no-break space) so its menu key is T. Paths `RiryToolsMenuBar.ConstraintTrainTools` and so on. Without the VRChat SDK: the VRCSDK not found popup. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/General Tools/Constraint Tools/Bezier` | (2021) The Bezier tab. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/General Tools/Constraint Tools/Insertable prop` | (2022) The Insertable prop tab. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/General Tools/Constraint Tools/Dual axis detector` | (2023) The Dual axis detector tab. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/General Tools/Constraint Tools/Three axis detector` | (2024) The Three axis detector tab. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/General Tools/AutoSync/Sync` | (2030) Turns AutoSync on or off; check mark. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/AutoSync/Configuration` | (2031) The AutoSync Configuration window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/Avatar Migration Assistant` | (2040) Written `Avatar Migratio&n Assistant` so its menu key is N: the Avatar Migration Assistant window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/IconCap` | (2041) Written `Ic&onCap` so its menu key is O: the IconCap window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/Material Cleaner` | (2042) The Material Cleaner window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/Prefab Cleaner` | (2043) The Prefab Cleaner popup (`GuidToolPrefabCleanerWindow`). | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/GUID Regenerator` | (2044) The GUID Regenerator popup, filled from the Project selection. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/GUID Repair` | (2045) The GUID Repair popup, filled from the Project selection. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/General Tools/PopUp Tools/GUID Broken Reference Checker` | (2046) Broken References for the open scenes. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Menu Tools` | (2060) Written `M&enu Tools` so its menu key is E: the Menu Tools window. Without the VRChat SDK: the VRCSDK not found popup. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/Animator Tools/Find and Replace` | (2070) The Find and Replace popup (`BehaviourFindReplaceWindow`), no source. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/Animator Tools/Clean Animation Keys` | (2071) The Clean Animation Keys window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Animator Tools/Controller Cleaner` | (2072) The Controller Cleaner window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Animator Tools/Extract Animations` | (2073) The Extract Animation window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Refresh Scripts` | (2080) `RefreshScripts.Run`: `AssetDatabase.Refresh`, closes open RiryTools windows, `EditorUtility.RequestScriptReload`, reopens them at their old positions. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Find Null references` | (2081) Reads the Console's NullReferenceException entries once and lists the references involved. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Menu Mockup` | (2082) Opens the Edit and Components windows. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Delete GUID Reference Cache` | (2083) `GuidToolClearReferenceCache.Run`. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Stop Auto Game` | (2084) Turns Stop Auto Game on or off; check mark. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Script Load Guard` | (2085) Turns Script Load Guard on or off; check mark. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Debug Tools/Stop all tool scripts` | (2086) [check mark] `StopAllToolScripts.Set`: the emergency stop; works while stopped. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/FTEmulator` | (2090) The FTEmulator window (`FTEmulatorWindow.Open`). No right-click entry. Path is `RiryToolsMenuBar.FTEmulatorTools`. Without the VRChat SDK or the `com.riry.ftemulator` package: a popup saying which is missing. | `com.riry.ftemulator/Editor/UI/FTEmulatorMenus.cs`, `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Documentation` | (2100) Opens `RiryToolsMenuBar.DocumentationLink`. | `UI/RiryToolsMenuBar.cs` |

### Project window right-click

| Name | Use | Scripts |
|---|---|---|
| `Assets/RiryTools/General Tools/Import Presets/Set Folder Import Presets` | (2000) The small popup: one preset per folder, or for all. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/General Tools/Import Presets/Save Import Preset` | (2001) The Save Import Preset window for the selected asset. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/General Tools/PopUp Tools/Material Cleaner` | (2002) The Material Cleaner window, with the selected material in its Material field. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/General Tools/PopUp Tools/Prefab Cleaner` | (2003) Removes unused prefab overrides from the selected scenes and prefabs (and those in selected folders), after asking. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/General Tools/PopUp Tools/GUID Regenerator` | (2004) Regenerates the selected assets' GUIDs. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/General Tools/PopUp Tools/GUID Repair` | (2005) Repairs missing GUID references in the selection. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Animator Tools/Find and Replace` | (2010) The Find and Replace popup with the selected animator controller. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Assets/RiryTools/Animator Tools/Clean Animation Keys` | (2011) Clean Animation Keys on the selected clips. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Animator Tools/Extract Animations` | (2012) The Extract Animation window with the selected model; only for a model with embedded animations. | `UI/RiryToolsRightClickMenu.cs` |

### Hierarchy right-click

Also in the GameObject menu.

| Name | Use | Scripts |
|---|---|---|
| `GameObject/RiryTools/General Tools/Blendshape Sync` | (10) A new Blendshape Sync job with the selected Skinned Mesh Renderer as its master; enabled when the active object has one. | `UI/RiryToolsRightClickMenu.cs` |
| `GameObject/RiryTools/General Tools/Constraint Tools/Train` | (11) The Train tab, with the selection as the chain. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/General Tools/Constraint Tools/Bezier` | (12) The Bezier tab, with the selection as the chain. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/General Tools/Constraint Tools/Insertable prop` | (13) The Insertable prop tab; the selected object becomes the Socket and its avatar the Avatar root. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/General Tools/Constraint Tools/Dual axis detector` | (14) The Dual axis detector tab; the selected object becomes Source, another selected object Relative to. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/General Tools/Constraint Tools/Three axis detector` | (15) The Three axis detector tab, filled the same way. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/General Tools/PopUp Tools/Avatar Migration Assistant` | (16) The Avatar Migration Assistant popup with the selected object's avatar as Source. | `UI/RiryToolsRightClickMenu.cs` |
| `GameObject/RiryTools/Menu Tools` | (17) The Menu Tools window with the selected object's avatar as its target. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/Animator Tools/Find and Replace` | (18) The Find and Replace popup with the selected scene object. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/Animator Tools/Controller Cleaner` | (19) The Controller Cleaner window with the controller of the Animator on the selected object or its nearest parent with one; enabled when there is one. | `UI/RiryToolsRightClickMenu.cs` |
| `GameObject/RiryTools/Attach To` | (30) The Attach To popup. | `UI/RiryToolsRightClickMenu.cs` |
| `GameObject/RiryTools/Invert Global Position` | (31) Mirrors the selected objects to the other side of their avatar, then asks whether to move them under the mirrored bone. | `UI/RiryToolsRightClickMenu.cs`, `UI/Utilities/InvertGlobalPositionPrompts.cs` |
| `GameObject/RiryTools/Find scripts that reference this` | (32) A popup of links to the components in the selected objects' avatar that reference them. | `UI/RiryToolsRightClickMenu.cs`, `UI/Utilities/FindReferencingScriptsWindow.cs` |

### Git menu

| Name | Use | Scripts |
|---|---|---|
| `Git/Configuration` | (2) The AutoSync Configuration window. | `UI/RiryToolsMenuBar.cs` |
| `Git/Sync` | (1) Turns AutoSync on or off; check mark. | `UI/RiryToolsMenuBar.cs` |

## Publishing

### Source workflow

| Name | Use | Scripts |
|---|---|---|
| `CHANNEL` | Step env: `stable` for `main`, `beta` for `beta`. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `GH_TOKEN` | Step env: `secrets.VPM_DISPATCH_TOKEN`. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `notify-vpm-<branch>` | Concurrency group: one run at a time per branch, in push order. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |
| `notify-vpm-ftemulator-<branch>` | Concurrency group of the FTEmulator notify: one run at a time per branch, in push order. | `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `paths` (notify) | `notify-vpm.yml` runs only for pushes that change `com.riry.rirytools/**`, `notify-vpm-ftemulator.yml` only for `com.riry.ftemulator/**`, so an unchanged package is not built. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `SHA` | Step env: `github.sha`, the pushed commit. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `package` (notify) | `notify-vpm.yml` starts Build Package with the default package (`com.riry.rirytools`); `notify-vpm-ftemulator.yml` with `com.riry.ftemulator`. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |
| `VPM_DISPATCH_TOKEN` | Secret: fine-grained token for `Rionquosue/rirytools_vpm` only, **Actions: Read and write**. | `riryunityworkflow/.github/workflows/notify-vpm.yml`, `riryunityworkflow/.github/workflows/notify-vpm-ftemulator.yml` |

### Destination workflows

| Name | Use | Scripts |
|---|---|---|
| `build-package` | Concurrency group; never cancelled. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `channel` | Input, required: `stable` or `beta`. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `package` | Input, required: `com.riry.rirytools` (source `Rionquosue/riryunityworkflow`), `com.riry.animatortemplates` (`Rionquosue/riryanimatortemplates`) or `com.riry.ftemulator` (`Rionquosue/riryunityworkflow`). | `rirytools_vpm/.github/workflows/build-package.yml` |
| `CHANNEL`, `SHA`, `VERSION` | Step env: the inputs, passed only through the environment. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `GH_TOKEN` | Step env: `github.token`. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `github-pages` | Environment: Pages deployment target; main only, no required reviewers. | `rirytools_vpm/.github/workflows/build-listing.yml` |
| `GITHUB_TOKEN` | Step env: `github.token`. | `rirytools_vpm/.github/workflows/build-listing.yml`, `rirytools_vpm/.github/workflows/issue-check.yml` |
| `issue-check-<issue number>` | Concurrency group: one run per issue. | `rirytools_vpm/.github/workflows/issue-check.yml` |
| `listPublishDirectory` | Workflow env: `Website`. | `rirytools_vpm/.github/workflows/build-listing.yml` |
| `pages` | Concurrency group: deploy job only. | `rirytools_vpm/.github/workflows/build-listing.yml` |
| `pathToCi` | Workflow env: `ci`. | `rirytools_vpm/.github/workflows/build-listing.yml` |
| `PREVIOUS`, `TAG`, `ZIP` | Step env: the step outputs below. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `sha` | Input: 40 lowercase hex; empty for the head of the channel's branch. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `SOURCE_REPOSITORY` | Workflow env: `Rionquosue/riryunityworkflow`. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `steps.build.outputs.tag` / `zip` / `version` / `digest` | Step output from `build_vpm.py build`. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `steps.inputs.outputs.ref` | Step output: `sha`, or the channel's branch. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `steps.version.outputs.previous` / `version` | Step output from `build_vpm.py plan`. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `TRIPWIRE_HASHES` | Repository variable: comma-separated canary hashes. | `rirytools_vpm/.github/workflows/issue-check.yml` |
| `version` | Input: `X.Y.Z` (stable) or `X.Y.Z-beta.N` (beta), no leading zeros, `Z` at most 99; empty for automatic. | `rirytools_vpm/.github/workflows/build-package.yml` |

### Destination scripts

| Name | Use | Scripts |
|---|---|---|
| `BLOCK_SCORE` | `0.8`: issue score that blocks. | `rirytools_vpm/scripts/issue_check.py` |
| `FIELDS` | `package.json`: the package fields; `version` and `url` are added. | `rirytools_vpm/scripts/build_vpm.py` |
| `FIRST` | `0.1.0`: the first stable version. | `rirytools_vpm/scripts/build_vpm.py` |
| `FIXED_TIME` | 2020-01-01: zip timestamps, so builds are reproducible. | `rirytools_vpm/scripts/build_vpm.py` |
| Label names | The issue labels the check applies. | `rirytools_vpm/scripts/issue_check.py` |
| `LFS_POINTER` | Refuses LFS pointer files. | `rirytools_vpm/scripts/build_vpm.py` |
| `MAX_PATCH` | `99`: the highest patch; the next version is the next minor at patch 0. | `rirytools_vpm/scripts/build_vpm.py` |
| `NUMBER`, `STABLE`, `BETA` | Version patterns; `NUMBER` has no leading zeros. | `rirytools_vpm/scripts/build_vpm.py` |
| `PACKAGE_NAME` | `com.riry.rirytools`: the source folder that is zipped, the package name, and the zip name `com.riry.rirytools-<version>.zip`. | `rirytools_vpm/scripts/build_vpm.py` |
| `PACKAGES` | `com.riry.rirytools` (tags `v`), `com.riry.animatortemplates` (`animatortemplates-v`) and `com.riry.ftemulator` (`ftemulator-v`): packages the listing must contain, each with its tag prefix. | `rirytools_vpm/scripts/verify_listing.py`, `rirytools_vpm/scripts/build_vpm.py` |
| `ftemulator.package.json` | The fields of `com.riry.ftemulator`'s release `package.json`, without `vpmDependencies` (set at build time from `DEPENDENCIES`). | `rirytools_vpm/scripts/build_vpm.py` |
| `DEPENDENCIES` | `com.riry.ftemulator` needs `com.riry.rirytools`: its release `package.json` gets `vpmDependencies` `com.riry.rirytools` `>=` RiryTools' current release on the same channel (stable, beta or beta-unity6); the build stops when there is none. | `rirytools_vpm/scripts/build_vpm.py` |
| `--tags` | `build_vpm.py build`: file of the listing's release tags, one per line (`$RUNNER_TEMP/tags`); needed for a package in `DEPENDENCIES`. | `rirytools_vpm/scripts/build_vpm.py`, `rirytools_vpm/.github/workflows/build-package.yml` |
| `REPOSITORY` | `Rionquosue/rirytools_vpm`. | `rirytools_vpm/scripts/verify_listing.py`, `rirytools_vpm/scripts/triage_read.py` |
| `REVIEW_SCORE` | `0.45`: issue score that asks for review. | `rirytools_vpm/scripts/issue_check.py` |
| `WEBSITE`, `SOURCE_JSON`, `TEMPLATED` | What the listing is checked against. | `rirytools_vpm/scripts/verify_listing.py` |
