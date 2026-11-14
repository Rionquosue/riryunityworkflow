# Variables

Every name RiryTools stores, reads or writes: settings keys, files and folders,
Git names, GUIDs, build symbols, tunable constants, menu paths, and the
publishing workflows' inputs, environment variables and secrets. Source paths
are relative to `com.riry.rirytools/Editor/` unless they start with a repository
name.

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
  - AutoSync
  - Avatar Migration Assistant
  - Blendshape Sync
  - Clean Animation Keys
  - Constraint Tool
    - Subtools: `ConstraintTrain*`, `ConstraintBezier*`, `InsertableProp*`
  - GUID Tools
  - IconCap
  - Import Presets
  - Menu Mockup
  - Menu Tool
    - Tabs: Parameter budget and Menu manager
  - Refresh Scripts
  - Stop Auto Game

  File, folder and document names follow them per ./instructions~ files.

## Stored settings

### EditorPrefs

Per user, shared by every project.

| Name | Use | Scripts |
|---|---|---|
| `com.vrcfury.parameterCompressor` | VRCFury's own Parameter Compressor setting (int: 0 Compress, 1 Ask, 2 Fail; unset means Compress). Menu Tool reads it; **Compare exactly** sets it to 2 and then 0 for its two test builds and puts it back (deleted again if it was unset). | `VRChat/MenuTool/MenuToolCompressor.cs` |
| `RiryTools.AttachTo.AddedBones` | Bones added with **Add bone** (string of HumanBodyBones names, comma separated; default `""`), listed after `AttachTo.CommonBones`. **Remove** takes one off. Read through `AttachTo.AddedBonesKey`. | `Utilities/AttachTo.cs` |
| `RiryTools.AvatarMigrator.Settings` | Avatar Migration Assistant settings as JSON: per-entry options, sub-options, ignore children and migrated folder. Unset means `StructureTolerance` 0.001, `ManualMatching` off, each entry as its `enabled` in types.yml, `MigratedFolder` empty. | `AvatarMigrator/AvatarMigratorSettings.cs` |
| `RiryTools.BetterFbx.BlenderPath` | Path to the Blender executable for Better FBX (string; `""` means detect). Read through `ImportPresetBetterFBX.BlenderPath`. | `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.BetterFbx.Options` | Better FBX export options (string; default `""`). Read through `ImportPresetBetterFBX.Options`. | `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `RiryTools.ScriptLoadGuard.Enabled` | Turns Script Load Guard on or off (bool; default `true`). Read through `ScriptLoadGuard.Enabled`. | `Debug/ScriptLoadGuard.cs`, `UI/RiryToolsMenuBar.cs` |
| `RiryTools.StopAutoGame.Enabled` | Turns Stop Auto Game on or off (bool; default `true`). Read through `StopAutoGame.Enabled`. | `Utilities/StopAutoGame.cs`, `UI/RiryToolsMainMenu.cs`, `UI/RiryToolsMenuBar.cs` |

### EditorUserSettings

Per user and per project, in `UserSettings/`.

| Name | Use | Scripts |
|---|---|---|
| `RiryTools.AutoSync.Enabled` | The **Sync** toggle (`1` / `0`; default `0`). Read through `AutoSync.Enabled`. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncGitActions.cs`, `UI/RiryToolsMainMenu.cs`, `UI/RiryToolsMenuBar.cs` |
| `RiryTools.AutoSync.LargeFileNoticeOff` | Remote URLs, one per line, where the large file notice's **Do Not Show Again** was chosen. Unset by default. | `AutoSync/AutoSync.cs` |
| `RiryTools.AutoSync.Remote` | The **Remote** AutoSync pushes to (a remote name). Unset means `origin`, else the first remote. Read through `AutoSyncGitRemotes.Selected`. | `AutoSync/AutoSync.cs`, `AutoSync/AutoSyncGitRemotes.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs` |
| `RiryTools.AvatarMigrator.SdkNoticeOff` | `1` once **Do not show again** is chosen on the VRC SDK not detected popup. Unset by default. | `UI/AvatarMigrator/AvatarMigratorSdkNoticeWindow.cs`, `UI/AvatarMigrator/AvatarMigratorWindow.cs` |
| `RiryTools.GuidTools.BrokenPromptOff` | Scene and prefab paths, one per line, where the broken reference prompt's **Do Not Show Again** was chosen. Unset by default. | `GuidTools/GuidToolBrokenReferenceMonitor.cs` |
| `RiryTools.MenuTool.LastAvatar` | GlobalObjectId of the last avatar the Menu Tool scanned (saved scenes only); selected when the Menu Tool opens with no target. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |

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
| `RiryTools.MenuMockup.Type` | Drag and drop data key: a component type dragged into the Menu Mockup Edit window. | `UI/Debug/MenuMockupEditWindow.cs` |
| `RiryTools.MenuTool.AddMenuName` | GUI control name of the name field in Menu Tool's Add menu dialog, so it gets keyboard focus. | `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs` |
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
| `<model>_ROOT.prefab` | Constraint Tool Train prefab variant. Suffix is `ConstraintTrainPrefabs.RootSuffix`. | `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs` |
| `<name>.fbx` beside `<name>.blend` | Better FBX export. Built by `ImportPresetBetterFBX.FbxPathFor`. | `ImportPresets/ImportPresetBetterFBX.cs`, `ImportPresets/ImportPresetPostprocessor.cs` |
| `Assets/RiryTools/` | Files unpacked from the package's `ProjectFolder~`, Constraint Tool Train clips, `_ROOT` prefab variants. Path is `RiryToolsProjectFolder.AssetPath`. | `Components/RiryToolsProjectFolder.cs`, `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`, `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs` |
| `Assets/RiryTools/InsertableProps/IP_<id>_<first 8 of the controller GUID>/` | Insertable prop clips: one folder per pair and controller, replaced in place on Generate. Built by `InsertablePropAnimator.ClipFolderPath`. | `VRChat/ConstraintTools/InsertableProp/InsertablePropAnimator.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.cs` |
| `Assets/RiryTools/MenuTool/<avatar>/<menu>.asset` | Menu Tool's copies of menu assets anything else uses (another slot of the avatar, Modular Avatar or VRCFury, a package, or any other file in the project), made by **Apply** before a native move and linked into that one slot. Ctrl+Z does not delete them. Tracked by `MenuToolApply.Owned`. | `VRChat/MenuTool/MenuToolApply.cs` |
| `Assets/RiryTools/MenuTool/<avatar>/<new menu>.asset` | Menu Tool's new, empty menus planned with **Add menu** as a menu asset, made by **Apply** (unique path) and linked by a SubMenu control in the parent's menu. Ctrl+Z does not delete them. Built by `MenuToolApply.NewMenuAsset`. | `VRChat/MenuTool/MenuToolApply.cs` |
| `Library/RiryTools/AutoSync/rewrite-index` | Temporary index while **Large Files** rewrites commits. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| `Library/RiryTools/GuidReferenceCache/` | `<name>_<hash>.rguiddb` reference caches. Path is `GuidToolReferenceCache.CacheDirectoryPath`. | `GuidTools/GuidToolReferenceCache.cs`, `UI/GuidTools/GuidToolClearReferenceCache.cs`, `UI/GuidTools/GuidToolsWindow.cs` |
| `Library/RiryTools/GuidTools/` | `restore` and `restore-meta` while a Git backup is written. | `GuidTools/GuidToolReferenceRepair.cs` |
| `Library/RiryTools/ImportPresets/` | `owned.txt` (path, size, write time and, for a clip, its model GUID), `Pending/<sha1>.txt`, `better_fbx_export.py`, `previous.fbx`. | `ImportPresets/ImportPresetOwnedFiles.cs`, `ImportPresets/ImportPresetAnimations.cs`, `ImportPresets/ImportPresetBetterFBX.cs`, `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs` |
| `Logs/RiryTools/GuidTools/` | `guid-tools.log` and `archive/guid-tools_<UTC time>.log`. Path is `GuidToolRegenerate.LogDirectoryPath`. | `GuidTools/GuidToolRegenerate.cs`, `GuidTools/GuidToolLog.cs`, `GuidTools/GuidToolReferenceRepair.cs`, `UI/GuidTools/GuidToolsWindow.cs` |
| `ProjectSettings/RiryToolsGuidRemap.tsv` | GUID remap ledger; keep it in version control. Path is `GuidToolRemapLedger.RelativePath`. | `GuidTools/GuidToolReferenceRepair.cs`, `GuidTools/GuidToolRemapLedger.cs`, `UI/GuidTools/GuidToolRegenerate.Prompts.cs` |
| `UserSettings/RiryTools/BlendshapeSync/<scene GUID>.json` | Blendshape Sync jobs of a scene (renderers as GlobalObjectIds). | `BlendshapeSync/BlendshapeSyncJobs.cs`, `BlendshapeSync/BlendshapeSyncJob.cs`, `UI/BlendshapeSync/BlendshapeSyncJobWindow.cs` |
| `UserSettings/RiryTools/MenuTool/<avatar key>.json` | Menu Tool's pathing JSON: the planned moves of one avatar (`Avatar`, `Key`, `Moves`: `From`, `To`, `Index`, `Choice`, `NewMenu` for a menu planned with **Add menu**; `NewControl`, `ControlType`, `Parameter`, `Value`, `Icon` (texture GUID) for **Add New Control**; `AssignIcon` with `From` and `Icon` for **Assign Menu Icon**). Deleted when empty. | `VRChat/MenuTool/MenuToolPlan.cs`, `VRChat/MenuTool/MenuToolApply.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `UserSettings/RiryTools/MenuTool/avatars.json` | The GUID stamped on each avatar Menu Tool planned for (`Object`: its GlobalObjectId, `Key`); nothing is written on the avatar. | `VRChat/MenuTool/MenuToolAvatarKeys.cs` |

### In the user's Documents folder

`%USERPROFILE%\Documents` on Windows, including a OneDrive redirect;
`~/Documents` elsewhere.

| Name | Use | Scripts |
|---|---|---|
| `Documents/RiryTools/GitTemplates/` | `<name>.gitignore` and `<name>.gitattributes` templates. Path is `AutoSyncGitTemplates.RootPath`. | `AutoSync/AutoSyncGitTemplates.cs`, `AutoSync/AutoSyncGitActions.cs`, `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs` |
| `Documents/RiryTools/iconcap presets/` | `<name>.json` IconCap presets (size, camera settings, background, capture options, effect stack). Path is `IconCapFiles.PresetFolder`. | `IconCap/IconCapFiles.cs`, `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs` |
| `Documents/RiryTools/ImportPresets/<configuration>/` | `*.preset` and optional `config.json`. Path is `ImportPresetLibrary.RootPath`. | `ImportPresets/ImportPresetLibrary.cs`, `ImportPresets/ImportPresetPreferences.cs`, `UI/ImportPresets/ImportPresetFolderWindow.cs`, `UI/ImportPresets/ImportPresetSaveWindow.cs` |
| `Documents/RiryTools/ImportPresets/preferences.yml` | Import Presets tags (`tags:` list). Path is `ImportPresetPreferences.FilePath`. | `ImportPresets/ImportPresetPreferences.cs`, `UI/ImportPresets/ImportPresetFolderWindow.UI.cs` |
| `Documents/RiryTools/migrator/types.yml` | Avatar Migration Assistant's type list (categories of entries: id, label, common, enabled, sdk, rule, types); copied from the package's `AvatarMigrator/types.yml` when missing, then read from here. Path is `AvatarMigratorCopyTypes.FilePath`. | `AvatarMigrator/AvatarMigratorCopyTypes.cs`, `UI/AvatarMigrator/AvatarMigratorWindow.cs` |
| `Documents/RiryTools/Mockups/` | `<name>.yml` saved mockups (annotated; Unity never compiles them). Path is `MenuMockup.Folder`. | `Debug/MenuMockup.cs`, `UI/Debug/MenuMockupComponentsWindow.cs`, `UI/Debug/MenuMockupNameWindow.cs` |

## Git names (AutoSync)

| Name | Use | Scripts |
|---|---|---|
| `.gitignore` lines | `/<path>`, pattern characters escaped; added by **Large Files**. | `AutoSync/AutoSyncGitLargeFiles.cs` |
| Branch | The checked-out branch AutoSync commits to. | `AutoSync/AutoSyncJob.cs` |
| Commit contents | The saved `.unity` and `.prefab` files and their `.meta` files. | `AutoSync/AutoSyncJob.cs` |
| Commit title | Time of the save, `yyyy-MM-dd'T'HH:mm:sszzz` (`2026-10-30T14:05:12+01:00`). Format is `AutoSync.TimeFormat`. | `AutoSync/AutoSync.cs` |
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
| `GIT_AUTHOR_*`, `GIT_COMMITTER_*` | The fallback identity. | `AutoSync/AutoSyncJob.cs` |
| `GIT_LITERAL_PATHSPECS=1` | Saved paths are not patterns. | `AutoSync/AutoSyncJob.cs`, `AutoSync/AutoSyncGitLargeFiles.cs`, `ImportPresets/ImportPresetAnimationExtraction.cs` |
| `GIT_TERMINAL_PROMPT=0` | Set on every command: Git never waits on a terminal prompt; sign-in goes through Git's credential manager. | `Components/GitRunner.cs` |

## GUIDs and build symbols

Every file and folder in `com.riry.rirytools/` has a `.meta` with a fixed GUID.
Moving or renaming a file must move its `.meta` with it.

| Name | Use | Scripts |
|---|---|---|
| `0756bf5a933b437db4405f7a01a55498` | GUID of `AvatarMigrator/types.yml.meta`: Avatar Migration Assistant's type list template, found by GUID (`AvatarMigratorCopyTypes.TemplateGuid`). | `AvatarMigrator/AvatarMigratorCopyTypes.cs` |
| Assembly `Riry.RiryTools.Editor` | Editor only, auto-referenced. All tools except the VRChat window. | `Riry.RiryTools.Editor.asmdef` |
| Assembly `Riry.RiryTools.Editor.GestureManager` | `GestureManagerRadial`, built against Gesture Manager 3.9.9. Editor only; references Gesture Manager's assemblies, `VRCSDK3A.dll` and `VRCSDKBase.dll`; needs `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER`. | `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef` |
| Assembly `Riry.RiryTools.Editor.VRChat` | Constraint Tool window (Train, Bezier and Insertable prop tabs), Menu Tool, and their menus. Editor only; needs `VRC.Dynamics.dll`, `VRC.SDK3.Dynamics.Constraint.dll`, `VRC.SDK3.Dynamics.Contact.dll`, `VRC.SDK3.Dynamics.PhysBone.dll`, `VRCSDK3A.dll` and `VRCSDKBase.dll`. | `VRChat/Riry.RiryTools.Editor.VRChat.asmdef` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.GestureManager")` | Lets the Gesture Manager bridge implement `IMenuToolRadial`. | `VRChat/AssemblyInfo.cs` |
| `InternalsVisibleTo("Riry.RiryTools.Editor.VRChat")` | Lets the VRChat assembly use internal types and menu paths. | `AssemblyInfo.cs` |
| `RIRYTOOLS_GESTURE_MANAGER` | Defined when `vrchat.blackstartx.gesture-manager` is 3.9.0 or newer. Compiles `Riry.RiryTools.Editor.GestureManager` (Menu Tool's radial preview); without it the Menu manager says to install Gesture Manager. | `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef`, `VRChat/MenuTool/GestureManager/GestureManagerRadial.cs` |
| `RIRYTOOLS_VRC_BIT_WIDTH` | Defined when `com.vrchat.avatars` is 3.10.6 or newer. Menu Tool reads each parameter's synced width and flipped flag (`GetBitWidthByType`, `flippedRepresentation`; `MenuToolScanner.WidthOf` / `FlippedOf`); older SDKs count 8 bits. | `VRChat/Riry.RiryTools.Editor.VRChat.asmdef`, `VRChat/MenuTool/MenuToolScanner.cs` |
| `RIRYTOOLS_VRC_CONSTRAINTS` | Defined when `com.vrchat.avatars` is 3.7.0 or newer. Compiles `Riry.RiryTools.Editor.VRChat`; without it the menus show the **VRCSDK not found** popup. | `Riry.RiryTools.Editor.asmdef`, `VRChat/Riry.RiryTools.Editor.VRChat.asmdef`, `VRChat/MenuTool/GestureManager/Riry.RiryTools.Editor.GestureManager.asmdef`, `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsRightClickMenu.cs` |
| Shader `Hidden/RiryTools/IconCap/Draw` | IconCap alpha pass and Flat lighting; keyword `ICONCAP_CLIP`. | `IconCap/Shaders/IconCapDraw.shader`, `IconCap/IconCapRenderer.cs` |
| Shader `Hidden/RiryTools/IconCap/Effects` | IconCap effect stack, composite and HDRI unwrap passes. | `IconCap/Shaders/IconCapEffects.shader`, `IconCap/IconCapEffects.cs` |
| Type names read by Menu Tool | `VF.Model.VRCFury` (features `Toggle`, `Puppet`, `FullController`, `MoveMenuItem`, `ReorderMenuItem`, `SetIcon`), `nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller`, `...MenuItem`, `...MenuGroup`, `...MenuInstallTarget`, `...Parameters`, `...MergeAnimator`, `VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks`. Looked up by name, so neither VRCFury nor Modular Avatar is needed to compile. | `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolBudget.cs`, `VRChat/MenuTool/MenuToolApply.cs` |

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
| `ImportPresetFolderRules.Labels` | `Image files`, `Model files`, `Audio files`, `Video files`, `Font files`: file type names; other importers show as `<Type> files`. | `ImportPresets/ImportPresetFolderRules.cs` |
| `ImportPresetFolderWindow.MaxSuggestions` | `8`: folder suggestions in Set Folder Import Presets. | `UI/ImportPresets/ImportPresetFolderWindow.cs` |
| `ImportPresetLibrary.ConfigFileName` / `PresetExtension` | `config.json` / `.preset`: configuration files. | `ImportPresets/ImportPresetLibrary.cs` |
| `ImportPresetLibrary.ReservedNames` | `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`: names Windows refuses. | `ImportPresets/ImportPresetLibrary.cs` |
| `ImportPresetMarker.BetterFbxKey` | `betterfbx`: marker key for Better FBX. | `ImportPresets/ImportPresetMarker.cs` |
| `ImportPresetMarker.FileName` | `ImportPreset.md`: folder marker. | `ImportPresets/ImportPresetMarker.cs`, `ImportPresets/ImportPresetFolderRules.cs`, `ImportPresets/ImportPresetPostprocessor.cs` |

### Main Menu, logging and shared UI

| Name | Use | Scripts |
|---|---|---|
| `RiryLog.Format` | `[RiryTools/<tool>] <message>` on every Console line. Tools: Attach To, AutoSync, Avatar Migration, Blendshape Sync, Clean Animation Keys, Constraint Tool, Find and Replace, Find Null references, GUID Tools, IconCap, Import Presets, Menu Mockup, Menu Tool, Script Load Guard, Stop Auto Game. A line about an object or asset passes it, so clicking the line pings it. | `Components/RiryLog.cs` |
| `RiryToolsMainMenu.TabBorder` / `SubTabGap` / `TopMargin` | `2` / `8` / `2` px: tab outline thickness; space between the main tabs and the sub-tabs; space above the main tabs. | `UI/RiryToolsMainMenu.cs`, `UI/Debug/MenuMockupPreview.cs`, `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `RiryToolsMenuBar.DocumentationLink` | `https://github.com/Rionquosue/riryunityworkflow`: opened by **Documentation**. Change it when the wiki is up. | `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |
| Settings Undo names | `Constraint Train settings`, `Bezier settings`, `Insertable prop settings`, `Blendshape Sync settings`, `Clean Animation Keys settings`, `Extract Animation settings`, `IconCap settings`: Undo steps of window settings edits, through `RiryToolGUI.Set`. | `UI/Components/RiryToolGUI.cs`, `UI/BlendshapeSync/BlendshapeSyncWindow.UI.cs`, `UI/BlendshapeSync/BlendshapeSyncJobList.cs`, `UI/AnimatorTools/CleanAnimationKeysWindow.cs`, `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs`, `UI/IconCap/IconCapWindow.cs`, `UI/IconCap/IconCapWindow.UI.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs`, `VRChat/UI/ConstraintTools/InsertablePropWindow.UI.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `StopAutoGame.HandleDelayMs` | `100`: delay after Play Mode starts before focus is restored. | `Utilities/StopAutoGame.cs` |

### Attach To

| Name | Use | Scripts |
|---|---|---|
| `AttachTo.CommonBones` | Head, Chest, Hips, Left/Right Hand, Left/Right Foot, Left/Right Upper Leg: bones listed before any added with **Add bone**. | `Utilities/AttachTo.cs` |
| `AttachTo.DialogNames` | `10`: names a dialog lists before "and n more". | `Utilities/AttachTo.cs` |
| `AttachTo.UndoName` | `Attach To`: popup title; Undo group `Attach To <bone>`. | `Utilities/AttachTo.cs`, `UI/Utilities/AttachToWindow.cs` |
| `AttachToBones.CentreNames` / `SidedNames` | e.g. `chest`, `head`, `hips`/`pelvis`; `hand`/`wrist`, `upperleg`/`thigh`, `foot`: name matching from a name without its side (`L`, `R`, `Left`, `Right`) to its bone. | `Utilities/AttachToBones.cs` |
| `AttachToBones.IgnoredParts` | `j`, `bip`, `c`, `def`, `mixamorig`, `bone`: name parts dropped before name matching. | `Utilities/AttachToBones.cs` |

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
| `BlendshapeSyncJobList.WideRow` | `570`: window width below which a Main Menu Blendshape Sync job row puts its buttons on a second line. | `UI/BlendshapeSync/BlendshapeSyncJobList.cs` |
| `BlendshapeSyncWindow.Interval` | `0.5` s: automatic sync period. | `UI/BlendshapeSync/BlendshapeSyncWindow.cs`, `UI/BlendshapeSync/BlendshapeSyncWindow.UI.cs`, `BlendshapeSync/BlendshapeSyncJob.cs` |
| `BlendshapeSyncWindow.SyncUndoName` / `RestoreUndoName` | `Sync Blendshape Weights` / `Restore Original Blendshape Weights`: Undo names; also mark the tool's own Undo steps. | `UI/BlendshapeSync/BlendshapeSyncWindow.cs`, `BlendshapeSync/BlendshapeSyncWindow.Sync.cs` |
| `CleanAnimationKeys.BoneValueDecimalPlaces` | `3`: rounding for bone endpoint comparison. | `AnimatorTools/CleanAnimationKeys.cs` |
| `CleanAnimationKeys.DisplayName` | `Clean Animation Keys`: dialog, window and Undo name. | `AnimatorTools/CleanAnimationKeys.cs`, `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysButton.ButtonElementName` | `rirytools-clean-animation-keys`: UI Toolkit name of the **Clean Keys** button. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysButton.ButtonLeft` / `ButtonBottom` | `170` / `1`: **Clean Keys** button in the Animation window's hierarchy-pane bottom bar, right of Dopesheet and Curves. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysButton.ScanIntervalSeconds` | `0.5`: how often the focused Animation window is checked. | `UI/AnimatorTools/CleanAnimationKeysButton.cs` |
| `CleanAnimationKeysWindow.AnimationListHeight` | `150`: height of the scrolling Animations list. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysWindow.MaxAnimations` | `64`: animations in the list. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |
| `CleanAnimationKeysWindow.MaxSuggestions` | `8`: whitelist suggestions shown. | `UI/AnimatorTools/CleanAnimationKeysWindow.cs` |

### Constraint Tool

| Name | Use | Scripts |
|---|---|---|
| Bezier rig objects | `Anchor Start`, `Anchor End`, `Handle Start`, `Handle End`, `Samples/Sample NN`, `Twist/Twist NN`: children of `BezierRig_<start>` (handles under their anchors with **Handles follow anchors**). | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierCurve.ArcSteps` / `FitRounds` / `Regularization` | `256` / `4` / `1e-6`: length table steps, fit rounds, and the pull of P1/P2 to straight-line handles. | `VRChat/ConstraintTools/ConstraintBezier/ConstraintBezierCurve.cs` |
| `ConstraintBezierWindow.MinAimDistance` | `1e-5`: shorter directions are not aimed along. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierWindow.RigPrefix` / `MechanismPrefix` | `BezierRig_` / `Bezier_`: the rig object and the mechanism chain, each a sibling of the chain start named prefix + its name. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintBezierWindow.UndoName` | `Bezier rig`: Undo group of Generate rig; Remove rig is `Remove Bezier rig`. | `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs` |
| `ConstraintToolWindow.Title` / `Tabs` | `Constraint Tool` / `Train`, `Bezier`, `Insertable prop`: the Constraint Tool window and its tabs (the Main Menu hosts this window, so these are the only tab list); minSize 420 x 500. | `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs` |
| `ConstraintTrainChains.RestPrefix` / `ChainPrefix(n)`, `ConstraintToolChains.EndpointSuffix` | `Rest_` / `Phys<n>_` / `_End`: generated chain names. | `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainChains.cs`, `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`, `VRChat/Components/ConstraintToolChains.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`, `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs` |
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

### Menu Tool

| Name | Use | Scripts |
|---|---|---|
| `MenuToolAddMenuDialog.Size` / `ControlSize` | `380` x `112` / `380` x `200`: Menu Tool's Add menu and Add New Control dialogs (fixed size, centred on the main window). | `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs` |
| `MenuToolBudgetCount.MaxBits` / `MaxParameters` | `256` / `8192`: VRChat's synced bits and custom expression parameters. | `VRChat/MenuTool/MenuToolBudget.cs`, `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolCompressor.BatchTime` | `0.1` s: VRCFury's time per compressed batch, used to pick between control type sets (a later set only if it halves the sync time). | `VRChat/MenuTool/MenuToolCompressor.cs` |
| `MenuToolCompressor.MaxCost()` | `256` (`MAX_PARAMETER_COST`), less `19` with DexProtect: VRCFury's limit for compression. | `VRChat/MenuTool/MenuToolCompressor.cs`, `VRChat/MenuTool/MenuToolBudget.cs` |
| `MenuToolScanner.MaxControls` | `8`: controls per menu (VRChat's limit); more are paged by VRCFury, otherwise flagged. | `VRChat/MenuTool/MenuToolScanner.cs`, `VRChat/MenuTool/MenuToolPlanner.cs`, `VRChat/MenuTool/MenuToolPreviewMenus.cs` |
| `MenuToolWindow.AvatarFilter` / `PickerButton` | `t:VRCAvatarDescriptor` / `19`: the target field's object picker search (Hierarchy objects with a VRC Avatar Descriptor) and the width of the picker button it replaces. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolWindow.IconPicker` | `0x52694963`: control ID of the Assign Menu Icon object picker. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| `MenuToolWindow.MinWidth` / `MinHeight` | `640` / `760`: Menu Tool's own window; the Menu manager asks for this width in the Main Menu too. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |
| Writes box height (`MenuToolWindow.DrawBits`) | `120` px: the Writes foldout's scroll box, with every parameter write since the window opened or Reset menu state, one row each. | `VRChat/UI/MenuTool/MenuToolWindow.cs` |

## Menu paths

The number in brackets is the menu priority. Constraint Tool's, Menu Tool's and
Find and Replace's entries are defined in `VRChat/UI/RiryToolsVRChatMenus.cs`
when the VRChat SDK is present, and in `UI/RiryToolsMenuBar.cs` (showing the
**VRCSDK not found** popup) when it is not. Keep the priorities of both files in
step.

### Tools menu

One entry or submenu per tool.

| Name | Use | Scripts |
|---|---|---|
| `Tools/RiryTools/Animator Tools/Clean Animation Keys` | (1994) The Clean Animation Keys window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Animator Tools/Find and Replace` | (1995) The Find and Replace popup (`BehaviourFindReplaceWindow`), no source; also the Main Menu's Animator Tools tab, sub-tab Find and Replace (`AnimatorTabs`). | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/AutoSync/Configuration` | (2001) The AutoSync Configuration window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/AutoSync/Sync` | (2000) Turns AutoSync on or off; check mark. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Avatar Migration Assistant` | (2005) Written `Avatar Migratio&n Assistant` so its menu key is N. Without the VRChat SDK it still runs, and opening it shows the VRC SDK not detected popup. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Blendshape Sync` | (2010) Add Job: a new job in its popup. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Constraint Tool` | (2030) Written `Constraint\u00A0&Tool` (a no-break space) so its menu key is T: the Constraint Tool window on the tab used last. Path is `RiryToolsMenuBar.ConstraintTool`. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/Documentation` | (2070) Opens `RiryToolsMenuBar.DocumentationLink`. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Find Null references` | (2056) Reads the Console's NullReferenceException entries (internal `UnityEditor.LogEntries`) once and lists empty or missing references in a popup, the whole open scenes when the log gives nothing. Also a button on the Main Menu's Tools tab. | `UI/RiryToolsMenuBar.cs`, `Debug/FindNullReferences.cs` |
| `Tools/RiryTools/GUID Tools` | (2040) The window, with Regenerate GUID, Repair Missing GUID References, Check for Broken References and Delete GUID Reference Cache. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/IconCap` | (2045) Written `Ic&onCap` so its menu key is O: the IconCap window (also from the Main Menu's Tools tab, in its own window). | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Import Presets/Extract Animation` | (2052) The Extract Animation window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Import Presets/Open Import Preset Library` | (2053) Opens the Import Preset library folder. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Import Presets/Save Import Preset` | (2051) The Save Import Preset window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Import Presets/Set Folder Import Presets` | (2050) The Set Folder Import Presets window. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Main Menu` | (1990) The dockable RiryTools window: a tab per tool and a Tools tab opening IconCap and Avatar Migration Assistant in their own windows, with Refresh Scripts and the Stop Auto Game toggle (Documentation link at its bottom right; a Menu Tool tab, no Menu Mockup tab). | `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs` |
| `Tools/RiryTools/Menu Mockup` | (2065) Opens the Edit and Components windows. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Menu Tool` | (2066) Written `M&enu Tool` so its menu key is E: the Menu Tool window (the Main Menu's Menu Tool tab shows its Parameter budget; its Menu manager opens this window). Without the VRChat SDK, the **VRCSDK not found** popup. Path is `RiryToolsMenuBar.MenuTool`. | `UI/RiryToolsMenuBar.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Tools/RiryTools/Refresh Scripts` | (2058) `RefreshScripts.Run`: `AssetDatabase.Refresh`, closes open RiryTools windows, `EditorUtility.RequestScriptReload`, reopens them at their old positions. Also a button on the Main Menu's Tools tab. | `UI/RiryToolsMenuBar.cs`, `UI/RiryToolsMainMenu.cs`, `Utilities/RefreshScripts.cs` |
| `Tools/RiryTools/Script Load Guard` | (2061) Turns Script Load Guard on or off; check mark. | `UI/RiryToolsMenuBar.cs` |
| `Tools/RiryTools/Stop Auto Game` | (2060) Turns Stop Auto Game on or off; check mark. | `UI/RiryToolsMenuBar.cs` |

### Project window right-click

| Name | Use | Scripts |
|---|---|---|
| `Assets/RiryTools/Animator Tools/Find and Replace` | (2006) The Find and Replace popup with the selected animator controller; enabled when one is selected. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `Assets/RiryTools/Clean Animation Keys` | (2005) Clean Animation Keys on the selected clips. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Extract Animation` | (2003) The Extract Animation window with the selected model; enabled only for a model with embedded animations. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Regenerate GUID` | (2000) Regenerates the selected assets' GUIDs. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Repair Missing GUID References` | (2004) Repairs missing GUID references in the selection. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Save Import Preset` | (2002) The Save Import Preset window for the selected asset. | `UI/RiryToolsRightClickMenu.cs` |
| `Assets/RiryTools/Set Folder Import Presets` | (2001) The small popup: one preset per folder, or for all. | `UI/RiryToolsRightClickMenu.cs` |

### Hierarchy right-click

Also in the GameObject menu.

| Name | Use | Scripts |
|---|---|---|
| `GameObject/RiryTools/Animator Tools/Find and Replace` | (22) The Find and Replace popup with the selected scene object; enabled when one is selected. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/Attach To` | (19) The Attach To popup; enabled when scene objects are selected; in no other menu. | `UI/RiryToolsRightClickMenu.cs` |
| `GameObject/RiryTools/Constraint Tool/Bezier` | (21) The Bezier tab, with the selection as the chain. Path is `RiryToolsRightClickMenu.ConstraintBezierHierarchy`. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |
| `GameObject/RiryTools/Constraint Tool/Train` | (20) The Train tab, with the selection as the chain. Path is `RiryToolsRightClickMenu.ConstraintTrainHierarchy`. | `UI/RiryToolsRightClickMenu.cs`, `VRChat/UI/RiryToolsVRChatMenus.cs` |

### Git menu

| Name | Use | Scripts |
|---|---|---|
| `Git/Configuration` | (2) The AutoSync Configuration window. | `UI/RiryToolsMenuBar.cs` |
| `Git/Sync` | (1) Turns AutoSync on or off; check mark. | `UI/RiryToolsMenuBar.cs` |

## Publishing

### Source workflow

| Name | Use | Scripts |
|---|---|---|
| `CHANNEL` | Step env: `stable` for `main`, `beta` for `beta`. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |
| `GH_TOKEN` | Step env: `secrets.VPM_DISPATCH_TOKEN`. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |
| `notify-vpm-<branch>` | Concurrency group: one run at a time per branch, in push order. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |
| `SHA` | Step env: `github.sha`, the pushed commit. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |
| `VPM_DISPATCH_TOKEN` | Secret: fine-grained token for `Rionquosue/rirytools_vpm` only, **Actions: Read and write**. | `riryunityworkflow/.github/workflows/notify-vpm.yml` |

### Destination workflows

| Name | Use | Scripts |
|---|---|---|
| `build-package` | Concurrency group; never cancelled. | `rirytools_vpm/.github/workflows/build-package.yml` |
| `channel` | Input, required: `stable` or `beta`. | `rirytools_vpm/.github/workflows/build-package.yml` |
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
| `PACKAGES` | `{com.riry.rirytools}`: packages the listing must contain. | `rirytools_vpm/scripts/verify_listing.py` |
| `REPOSITORY` | `Rionquosue/rirytools_vpm`. | `rirytools_vpm/scripts/verify_listing.py`, `rirytools_vpm/scripts/triage_read.py` |
| `REVIEW_SCORE` | `0.45`: issue score that asks for review. | `rirytools_vpm/scripts/issue_check.py` |
| `WEBSITE`, `SOURCE_JSON`, `TEMPLATED` | What the listing is checked against. | `rirytools_vpm/scripts/verify_listing.py` |
