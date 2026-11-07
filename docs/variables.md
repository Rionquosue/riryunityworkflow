# Variables

Every name RiryTools stores, reads or writes: settings keys, files and folders,
Git names, GUIDs, build symbols, tunable constants, menu paths, and the
publishing workflows' inputs, environment variables and secrets. Source paths
are relative to `com.riry.rirytools/Editor/` unless they start with a repository
name.

## Naming rules

- **Product name:** `RiryTools` in every label, menu, folder and key. Lower
  case (`rirytools`) only where the format needs it: file names, Git refs, UI
  Toolkit element names.
- **Settings keys:** `RiryTools.<Tool>.<Setting>`.
- **Folders:** `<Library|Logs|Documents>/RiryTools/<Tool>`, built by
  `RiryToolsPaths.cs`. The project folder is `Assets/RiryTools`
  (`RiryToolsProjectFolder.cs`).
- **Code:** namespace `Riry.RiryTools.Editor`. `Riry` is the author and
  `RiryTools` the product.
- **Tool names:** Attach To, AutoSync, Avatar Migration Assistant, Blendshape Sync, Clean
  Animation Keys, Constraint Tool (subtools Train, Bezier and Insertable prop), GUID Tools, IconCap, Import Presets, Menu
  Mockup, Menu Tool (tabs Parameter budget and Menu manager), Stop Auto Game. Class, file, folder and
  document names follow them; Constraint Tool's subtools are `ConstraintTrain*`, `ConstraintBezier*` and `InsertableProp*`.

## Stored settings

EditorPrefs: per user, shared by every project.

| Key | Type | Default | Owner |
|---|---|---|---|
| `RiryTools.StopAutoGame.Enabled` | bool | `true` | `StopAutoGame.Enabled` |
| `RiryTools.BetterFbx.BlenderPath` | string | `""` (detect) | `BetterFbxExporter.BlenderPath` |
| `RiryTools.BetterFbx.Options` | string | `""` | `BetterFbxExporter.Options` |
| `RiryTools.AttachTo.AddedBones` | string | `""` | `AttachTo.AddedBonesKey`: bones added with **Add bone** (HumanBodyBones names, comma separated), listed after `AttachTo.CommonBones`; **Remove** takes one off |
| `com.vrcfury.parameterCompressor` | int | unset (Compress) | VRCFury's own Parameter Compressor setting (0 Compress, 1 Ask, 2 Fail): `MenuToolCompressor` reads it; **Compare exactly** sets it to 2 and then 0 for its two test builds and puts it back (deleted again if it was unset) |
| `RiryTools.AvatarMigrationAssistant.Settings` | JSON | unset: `StructureTolerance` 0.001, `ManualMatching` off, each entry as its `enabled` in types.yml, `MigratedFolder` empty | `AvatarMigrationSettings` (per-entry options, sub-options, ignore children, migrated folder) |

EditorUserSettings: per user and per project, in `UserSettings/`.

| Key | Values | Default | Owner |
|---|---|---|---|
| `RiryTools.AutoSync.Enabled` | `1` / `0` | `0` | `AutoSync.Enabled` (**Sync**) |
| `RiryTools.AutoSync.Remote` | remote name | unset: `origin`, else the first remote | `GitRemotes.Selected` (**Remote**) |
| `RiryTools.AutoSync.LargeFileNoticeOff` | remote URLs, one per line | unset | `AutoSync.LargeFileNotice`: **Do Not Show Again** |
| `RiryTools.GuidTools.BrokenPromptOff` | scene and prefab paths, one per line | unset | `BrokenReferenceMonitor.PromptOnOpen`: **Do Not Show Again** |
| `RiryTools.AvatarMigrationAssistant.SdkNoticeOff` | `1` | unset | `AvatarMigrationSdkNotice`: **Do not show again** on the VRC SDK not detected popup |

SessionState: until the editor quits; survives script reloads.

| Key | Type | Owner |
|---|---|---|
| `RiryTools.AutoSync.Pending` | one save per line: title, then paths, tab-separated | `AutoSync`: saves not yet committed when scripts reloaded |
| `RiryTools.AutoSync.LargeFileNoticeShown` | remote URLs, one per line | `AutoSync.LargeFileNotice`: **Push Held** was shown this session |
| `RiryTools.StopAutoGame.FocusedWindows` | comma-separated instance IDs | `StopAutoGame`: windows focused when Play Mode started |
| `RiryTools.StopAutoGame.GameViewWasOpen` | bool | `StopAutoGame`: a Game view existed before Play Mode |
| `RiryTools.ScriptLoadGuard.Guarded` | newline-separated asset paths | `ScriptLoadGuard` |
| `RiryTools.AttachTo.LastAvatar` | instance ID | `AttachToAvatarTracker`: the last avatar selected in the Hierarchy (VRCAvatarDescriptor, else humanoid Animator); fills **Target avatar** |
| `RiryTools.MenuTool.UnsavedAvatars` | `<instance ID>=<key>` lines | `MenuToolAvatarKeys`: keys of avatars in scenes not saved yet; moved into `avatars.json` when the scene is saved |

Window fields marked `[SerializeField]` survive script reloads while the window
is open. Each window file lists them in its glossary.

## Files and folders

In the project:

| Path | Contents | Owner |
|---|---|---|
| `Assets/RiryTools/` | Files unpacked from the package's `ProjectFolder~`, Constraint Tool Train clips, `_ROOT` prefab variants | `RiryToolsProjectFolder.AssetPath` |
| `Library/RiryTools/GuidReferenceCache/` | `<name>_<hash>.rguiddb` reference caches | `RegenerateGuid.CacheDirectoryPath` |
| `Library/RiryTools/ImportPresets/` | `owned.txt` (path, size, write time and, for a clip, its model GUID), `Pending/<sha1>.txt`, `better_fbx_export.py`, `previous.fbx` | `ImportPresetOwnedFiles`, `ImportPresetAnimations`, `BetterFbxExporter` |
| `Library/RiryTools/GuidTools/` | `restore`, `restore-meta` while a Git backup is written | `GuidReferenceRepair.Restore` |
| `Logs/RiryTools/GuidTools/` | `guid-tools.log`, `archive/guid-tools_<UTC time>.log` | `RegenerateGuid.LogDirectoryPath`, `GuidToolLog` |
| `ProjectSettings/RiryToolsGuidRemap.tsv` | GUID remap ledger; keep it in version control | `GuidRemapLedger.RelativePath` |
| `.gitignore`, `.gitattributes` | Copied from a template by **Sync** (new repository) and **Configuration** | `GitActions.CopyTemplate` |
| `Library/RiryTools/AutoSync/rewrite-index` | Temporary index while **Large Files** rewrites commits | `GitLargeFiles.Ignore` |
| `<folder>/ImportPreset.md` | Import preset rules for a folder | `ImportPresetMarker.FileName` |
| `<model folder>/Animations/<Model>_<Clip>.anim` | Extracted clips (or the model's Destination) | `ImportPresetAnimations.FolderName` |
| `<model folder>/.<Model>_ani-extract.json~` | Extract Animation settings; no `.meta`, hidden on Windows, staged in Git | `AnimationExtractSettings.PathFor` |
| `<name>.fbx` beside `<name>.blend` | Better FBX export | `BetterFbxExporter.FbxPathFor` |
| `<model>_ROOT.prefab` | Constraint Tool Train prefab variant | `ConstraintTrainPrefabs.RootSuffix` |
| `<export folder>/.iconcap.json` | IconCap jobs of the folder (entries with camera, objects as GlobalObjectIds, settings, effect stack); hidden on Windows | `IconCapFiles.JobFileName` |
| `<export folder>/OLD_<n>/` | Icons a job's **Run** replaced, moved here without their `.meta` (one folder per Run) | `IconCapFiles.OldFolderPrefix` |
| `<migrated folder>/<original path without Assets/>` | Avatar Migration Assistant's duplicated controllers and clips (files from `Packages/` under `<migrated folder>/Packages/`); never overwritten | `AvatarMigratedFolder.Destination` |
| `<Icon Generator folder>/<prefix><source name><suffix>.png` | Icon Generator outputs, beside the asset | `IconGenerator.OutputName` |
| `Assets/RiryTools/MenuTool/<avatar>/<menu>.asset` | Menu Tool's copies of menu assets anything else uses (another slot of the avatar, Modular Avatar or VRCFury, a package, or any other file in the project), made by **Apply** before a native move and linked into that one slot; Ctrl+Z does not delete them | `MenuToolApply.Owned` |

In the user's Documents folder (`%USERPROFILE%\Documents` on Windows, including
a OneDrive redirect; `~/Documents` elsewhere):

| Path | Contents | Owner |
|---|---|---|
| `Documents/RiryTools/ImportPresets/<configuration>/` | `*.preset`, optional `config.json` | `ImportPresetLibrary.RootPath` |
| `Documents/RiryTools/GitTemplates/` | `<name>.gitignore`, `<name>.gitattributes` | `GitTemplates.RootPath` |
| `Documents/RiryTools/ImportPresets/preferences.yml` | Import Presets tags (`tags:` list) | `ImportPresetPreferences.FilePath` |
| `UserSettings/RiryTools/BlendshapeSync/<scene GUID>.json` | Blendshape Sync jobs of a scene (renderers as GlobalObjectIds) | `BlendshapeSyncJobs` |
| `UserSettings/RiryTools/MenuTool/<avatar key>.json` | Menu Tool's pathing JSON: the planned moves of one avatar (`Avatar`, `Key`, `Moves`: `From`, `To`, `Index`, `Choice`); deleted when empty | `MenuPlan` |
| `UserSettings/RiryTools/MenuTool/avatars.json` | The GUID stamped on each avatar Menu Tool planned for (`Object`: its GlobalObjectId, `Key`); nothing is written on the avatar | `MenuToolAvatarKeys` |
| `Documents/RiryTools/Mockups/` | `<name>.yml` saved mockups (annotated; Unity never compiles them) | `MenuMockup.Folder`, `MockupFile` |
| `Documents/RiryTools/migrator/types.yml` | Avatar Migration Assistant's type list (categories of entries: id, label, common, enabled, sdk, rule, types); copied from the package's `AvatarMigrationAssistant/types.yml` when missing, then read from here | `AvatarCopyTypes.FilePath` |
| `Documents/RiryTools/iconcap presets/` | `<name>.json` IconCap presets (size, camera settings, background, capture options, effect stack) | `IconCapFiles.PresetFolder` |

## Git names (AutoSync)

| Name | Value | Owner |
|---|---|---|
| Branch | The checked-out branch | `AutoSyncJob.Commit` |
| Remote | The one chosen in Configuration; **Add Remote** adds `origin` | `GitRemotes.DefaultName`, `AutoSyncJob.PushBranch` |
| Reflog message | `RiryTools AutoSync: drop large files` | `GitLargeFiles.Ignore` |
| `.gitignore` lines | `/<path>`, pattern characters escaped | `GitLargeFiles.AppendToGitignore` |
| LFS query | HTTP(S): `POST <remote>.git/info/lfs/objects/batch` (or `remote.<name>.lfsurl`, `lfs.url`), one all-zero object; SSH: `git-lfs-authenticate <path> download` with `core.sshCommand` or `ssh` | `GitLfsQuery` |
| Commit title | Time of the save, `yyyy-MM-dd'T'HH:mm:sszzz` (`2026-10-30T14:05:12+01:00`) | `AutoSync.TimeFormat` |
| Commit contents | The saved `.unity` and `.prefab` files and their `.meta` files | `AutoSyncJob.Commit` |
| Fallback identity | `RiryTools AutoSync` / `autosync@localhost`, only when `user.name` or `user.email` is unset | `AutoSyncJob.Open` |
| Default templates | `Unity.gitignore`, `Unity.gitattributes` | `GitTemplates.DefaultName` |

Environment variables set for Git commands:

- `GIT_TERMINAL_PROMPT=0` (`GitRunner`, every command): Git never waits on a
  terminal prompt; sign-in goes through Git's credential manager.
- `GIT_LITERAL_PATHSPECS=1` (`AutoSyncJob`): saved paths are not patterns.
- `GIT_AUTHOR_*`, `GIT_COMMITTER_*` (`AutoSyncJob`): the fallback identity.
- Arguments: `-c core.quotepath=off` always; `-c core.fsync=committed` for AutoSync commits.

## GUIDs and build symbols

| Name | Value | Use |
|---|---|---|
| `RIRYTOOLS_VRC_CONSTRAINTS` | defined when `com.vrchat.avatars` is 3.7.0 or newer | Compiles `Riry.RiryTools.Editor.VRChat`; without it, `RiryMenus.cs` shows the **VRCSDK not found** popup |
| `RIRYTOOLS_VRC_BIT_WIDTH` | defined when `com.vrchat.avatars` is 3.10.6 or newer | Menu Tool reads each parameter's synced width and flipped flag (`GetBitWidthByType`, `flippedRepresentation`; `MenuToolScanner.WidthOf` / `FlippedOf`); older SDKs count 8 bits |
| Assembly `Riry.RiryTools.Editor` | Editor only, auto-referenced | All tools except the VRChat window |
| Assembly `Riry.RiryTools.Editor.VRChat` | Editor only, needs `VRC.Dynamics.dll`, `VRC.SDK3.Dynamics.Constraint.dll`, `VRC.SDK3.Dynamics.Contact.dll`, `VRC.SDK3.Dynamics.PhysBone.dll`, `VRCSDK3A.dll` and `VRCSDKBase.dll` | Constraint Tool window (Train, Bezier and Insertable prop tabs), Menu Tool, and their menus |
| `InternalsVisibleTo("Riry.RiryTools.Editor.VRChat")` | `AssemblyInfo.cs` | Lets the VRChat assembly use internal types and menu paths |
| `RIRYTOOLS_GESTURE_MANAGER` | defined when `vrchat.blackstartx.gesture-manager` is 3.9.0 or newer | Compiles `Riry.RiryTools.Editor.GestureManager` (Menu Tool's radial preview); without it the Menu manager says to install Gesture Manager |
| Assembly `Riry.RiryTools.Editor.GestureManager` | `VRChat/MenuTool/GestureManager/`; Editor only, references Gesture Manager's assemblies, `VRCSDK3A.dll` and `VRCSDKBase.dll`; needs `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER` | `GestureManagerRadial`, built against Gesture Manager 3.9.9 |
| `InternalsVisibleTo("Riry.RiryTools.Editor.GestureManager")` | `VRChat/AssemblyInfo.cs` | Lets the bridge implement `IMenuToolRadial` |
| Type names read by Menu Tool | `VF.Model.VRCFury` (features `Toggle`, `Puppet`, `FullController`, `MoveMenuItem`, `ReorderMenuItem`, `SetIcon`), `nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller`, `...MenuItem`, `...MenuGroup`, `...MenuInstallTarget`, `...Parameters`, `...MergeAnimator`, `VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks` | `MenuToolScanner`, `MenuToolBudget`, `MenuToolApply`: by name, so neither VRCFury nor Modular Avatar is needed to compile |
| `0756bf5a933b437db4405f7a01a55498` | `AvatarMigrationAssistant/types.yml.meta` | Avatar Migration Assistant's type list template, found by GUID (`AvatarCopyTypes.TemplateGuid`) |
| Shader `Hidden/RiryTools/IconCap/Effects` | `IconCap/Shaders/IconCapEffects.shader` | IconCap effect stack, composite and HDRI unwrap passes |
| Shader `Hidden/RiryTools/IconCap/Draw` | `IconCap/Shaders/IconCapDraw.shader` | IconCap alpha pass and Flat lighting; keyword `ICONCAP_CLIP` |

Every file and folder in `com.riry.rirytools/` has a `.meta` with a fixed GUID.
Moving or renaming a file must move its `.meta` with it.

## Tunable constants

AutoSync and Git:

| Constant | Value | Meaning |
|---|---|---|
| `GitState.CacheSeconds` | `5.0` | How long Git and repository state is cached |
| `AutoSyncJob.TimeoutMs` | `120000` | Timeout per Git command |
| `AutoSyncJob.PushTimeoutMs` | `600000` | Push timeout, long enough to sign in |
| `GitLargeFiles.ProxyLimitBytes` | 100 MiB | Cloudflare's proxied upload limit: the ! button and held pushes |
| `GitLfsQuery.TimeoutMs` | `30000` | LFS query timeout |
| `GitRunner.TimedOut` | `-1` | Exit code reported for a timed-out or cancelled command |
| `GitNotInstalledException.DownloadUrl` | `https://git-scm.com/install/` | **Download Git** link |
| `GitTemplates.UnityIgnoreUrl` | GitHub's `Unity.gitignore` | Default `.gitignore` template, saved as `Unity` |
| `GitTemplates.UnityAttributesUrl` | gitattributes project's `Unity.gitattributes` | Default `.gitattributes` template, saved as `Unity` |
| `GitTemplates.DefaultAttributes` | built-in text | Built-in LFS template, saved as `UnityLFS` |
| `ScriptLoadGuard.CaptureSeconds` | `5.0` | Errors this long before a prefab opens count as suspects |
| `ScriptLoadGuard.RecentLimit` | `50` | Logged errors kept |
| `BrokenReferenceMonitor.Threshold` / `WindowSeconds` | `3` in `30.0` s | Missing-reference log lines that trigger the prompt |

GUID Tools:

| Constant | Value | Meaning |
|---|---|---|
| `RegenerateGuid.GuidLength` | `32` | Hex digits in a GUID |
| `RegenerateGuid.ScanBlockSize` | 256 KiB | Read block for scanning |
| `RegenerateGuid.MaxDirectoryDepth` | `64` | Stops symbolic link loops |
| `RegenerateGuid.CacheExtension` / `CacheMagic` / `CacheVersion` | `.rguiddb` / `RGIX` / `1` | Cache file format |
| `RegenerateGuid.CacheNameLength` | `40` | Readable part of a cache file name |
| `RegenerateGuid.TextExtensions` | list | Files scanned for references |
| `RegenerateGuid.UnitySerializedExtensions` | list | Files that must be text to be patched |
| `GuidToolLog.ActiveLogLimit` / `ArchiveLimit` | 10 MiB / 100 MiB | Log rotation and archive trim |
| `GuidRemapLedger.MaxChain` | `64` | Longest old-to-new GUID chain followed |
| `GuidReferenceRepair.Title` | `Broken References` | Window and dialog title |
| `GuidReferenceRepair.MaxNameMatches` | `5` | Name matches offered per old name |

Import Presets and Better FBX:

| Constant | Value | Meaning |
|---|---|---|
| `ImportPresetMarker.FileName` | `ImportPreset.md` | Folder marker |
| `ImportPresetMarker.BetterFbxKey` | `betterfbx` | Marker key for Better FBX |
| `ImportPresetFolderRules.Labels` | `Image files`, `Model files`, `Audio files`, `Video files`, `Font files` | File type names; other importers show as `<Type> files` |
| `ImportPresetFolderWindow.MaxSuggestions` | `8` | Folder suggestions in Set Folder Import Presets |
| `ImportPresetLibrary.ConfigFileName` / `PresetExtension` | `config.json` / `.preset` | Configuration files |
| `ImportPresetLibrary.ReservedNames` | `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9` | Names Windows refuses |
| `BetterFbxExporter.SourceExtension` | `blend` | Only file type Blender exports |
| `BetterFbxExporter.TimeoutSeconds` | `600` | Blender run limit |
| `BetterFbxExporter.SuccessMarker` | `RIRYTOOLS_BETTER_FBX_OK` | Printed by the export script on success |

Other tools:

| Constant | Value | Meaning |
|---|---|---|
| `BlendshapeSyncWindow.Interval` | `0.5` s | Automatic sync period |
| `BlendshapeSyncWindow.SyncUndoName` / `RestoreUndoName` | `Sync Blendshape Weights` / `Restore Original Blendshape Weights` | Undo names; also mark the tool's own Undo steps |
| `CleanAnimationKeys.DisplayName` | `Clean Animation Keys` | Dialog, window and Undo name |
| `CleanAnimationKeysWindow.MaxAnimations` | `64` | Animations in the list |
| `CleanAnimationKeysWindow.AnimationListHeight` | `150` | Height of the scrolling Animations list |
| `CleanAnimationKeysWindow.MaxSuggestions` | `8` | Whitelist suggestions shown |
| `CleanAnimationKeys.BoneValueDecimalPlaces` | `3` | Rounding for bone endpoint comparison |
| `CleanAnimationKeysButton.ScanIntervalSeconds` | `0.5` | How often the focused Animation window is checked |
| `CleanAnimationKeysButton.ButtonElementName` | `rirytools-clean-animation-keys` | UI Toolkit name of the **Clean Keys** button |
| `CleanAnimationKeysButton.ButtonLeft` / `ButtonBottom` | `170` / `1` | **Clean Keys** button in the Animation window's hierarchy-pane bottom bar, right of Dopesheet and Curves |
| `RiryToolsMainMenu.TabBorder` / `SubTabGap` / `TopMargin` | `2` / `8` / `2` px | Tab outline thickness; space between the main tabs and the sub-tabs; space above the main tabs |
| `BlendshapeSyncJobList.WideRow` | `570` | Window width below which a Main Menu Blendshape Sync job row puts its buttons on a second line |
| `ImportPresetAnimations.UndoName` | `Extract Animation` | Undo step of the Extract button's extraction (replaced clips are recorded; new files are not removed by Undo). Automatic extractions (first import, re-export, Folder Rules Apply) leave no Undo step |
| `RiryLog.Format` | `[RiryTools/<tool>] <message>` | Every Console line (`Editor/RiryLog.cs`); tools: Attach To, AutoSync, Avatar Migration, Blendshape Sync, Clean Animation Keys, Constraint Tool, GUID Tools, IconCap, Import Presets, Menu Mockup, Menu Tool, Project Folder, Script Load Guard, Stop Auto Game. A line about an object or asset passes it, so clicking the line pings it |
| Settings Undo names | `Constraint Train settings`, `Bezier settings`, `Insertable prop settings`, `Blendshape Sync settings`, `Clean Animation Keys settings`, `Extract Animation settings`, `IconCap settings` | Undo steps of window settings edits (`RiryGui.Set`, `UI/RiryGui.cs`) |
| `ConstraintTrainPlanner.MaxChains` | `5` | Physics chains |
| `ConstraintTrainChains.RestPrefix` / `ChainPrefix(n)` / `EndpointSuffix` | `Rest_` / `Phys<n>_` / `_End` | Generated chain names |
| `ConstraintTrainWindow.UndoName` | `Constraint Train` | Undo group name |
| `ConstraintToolWindow.Title` / `Tabs` | `Constraint Tool` / `Train`, `Bezier`, `Insertable prop` | The Constraint Tool window and its tabs (also the Main Menu's `ConstraintTabs`); minSize 420 x 500 |
| `ConstraintBezierWindow.UndoName` | `Bezier rig` | Undo group of Generate rig; Remove rig is `Remove Bezier rig` |
| `ConstraintBezierWindow.RigPrefix` / `MechanismPrefix` | `BezierRig_` / `Bezier_` | The rig object and the mechanism chain, each a sibling of the chain start named prefix + its name |
| Bezier rig objects | `Anchor Start`, `Anchor End`, `Handle Start`, `Handle End`, `Samples/Sample NN`, `Twist/Twist NN` | Children of `BezierRig_<start>` (handles under their anchors with **Handles follow anchors**) |
| `ConstraintBezierWindow.MinAimDistance` | `1e-5` | Shorter directions are not aimed along |
| `ConstraintBezierCurve.ArcSteps` / `FitRounds` / `Regularization` | `256` / `4` / `1e-6` | Length table steps, fit rounds, and the pull of P1/P2 to straight-line handles |
| `InsertablePropRig.RootPrefix` / pair ID | `IP_` / `<avatar>_<prop>` (non-alphanumerics as `_`) | Names of the pair's objects, contact tag (`IP_<id>_Tip`), parameters, layers and clip folder |
| Insertable prop objects | `IP_<id>/Prop`, `Rig/Anchor/End`, `Rig/GripSense/Marker`, `Rig/Frame Remote`, `Frame Wearer L`, `Frame Wearer R`, `Frame Sheath`, `Rig/HandFrame`, `Rig/Hand`, `World/Body`, `World/Follower` (PC), `Drop/RayOrigin`, `Landed`, `DropYaw/Lying` (Android); on the prop `IP Grip`, `IP Tip`; on the socket `IP_<id> Seat`, `Opening`, `Entry` | Generated by `InsertablePropRig.Build` |
| Insertable prop parameters | `IP_<id>_IsGrabbed`, `IP_<id>_Grip_SenderType`, `_SenderLocalPositionX/Y/Z`, `_PointSeparation`, `_CollisionTag_FingerLittleL/R`, `IP_<id>_Sheath`, `IP_<id>_State` (synced Int: Seated 0, Held 1, Inserting 2, Dropped 3), `IP_<id>_One` | Animator and Expression parameters (`InsertablePropAnimator`) |
| Insertable prop layers | `IP_<id> Decide`, `Pose`, `Hand`, `Blend` | Appended to the chosen controller, else the avatar's FX |
| Insertable prop clips | `Assets/RiryTools/InsertableProps/IP_<id>_<first 8 of the controller GUID>/` | `InsertablePropAnimator.ClipFolderPath`; one folder per pair and controller; replaced in place on Generate |
| `InsertablePropWindow.Settings` defaults | capture radius `0.05` m, grip smoothing `0.08` s, insert smoothing `0.15` s, stiffness `1500` | Insertable prop tab options |
| `InsertablePropMath.MaxStretch` / `AnchorLength` / `EntryMargin` / `SettledFraction` | `5` / `1.7` m / `0.03` m / `0.99` | PhysBone stretch cap (SDK), root-to-end rest distance (reach about 10 m), entry point margin, slide step end |
| `InsertablePropRig.TipRadius` / `GripReceiverRadius` / `GrabRadius` / `PickupLayer` | `0.01` / `0.15` / `0.06` m / `13` | Tip sender, grip receiver, PhysBone grab radius, body layer |
| `InsertablePropAnimator.WeakSeparation` / `InsideSheath` | `0.01` m / `0.001` | Grip reading too weak below this; tip inside the capture radius above this proximity |
| `StopAutoGame.HandleDelayMs` | `100` | Delay after Play Mode starts before focus is restored |
| `RiryMenus.DocumentationLink` | `https://github.com/Rionquosue/riryunityworkflow` | Opened by **Documentation**; change it when the wiki is up |
| `RiryToolsProjectFolder.SourceFolderName` | `ProjectFolder~` | Package folder unpacked into `Assets/RiryTools` |

Attach To:

| Constant | Value | Meaning |
|---|---|---|
| `AttachTo.CommonBones` | Head, Chest, Hips, Left/Right Hand, Left/Right Foot, Left/Right Upper Leg | Bones listed before any added with **Add bone** |
| `AttachTo.UndoName` | `Attach To` | Popup title; Undo group `Attach To <bone>` |
| `AttachTo.DialogNames` | `10` | Names a dialog lists before "and n more" |
| `AttachToBones.IgnoredParts` | `j`, `bip`, `c`, `def`, `mixamorig`, `bone` | Name parts dropped before name matching |
| `AttachToBones.CentreNames` / `SidedNames` | e.g. `chest`, `head`, `hips`/`pelvis`; `hand`/`wrist`, `upperleg`/`thigh`, `foot` | Name matching: a name without its side (`L`, `R`, `Left`, `Right`) to its bone |

Avatar Migration Assistant:

| Constant | Value | Meaning |
|---|---|---|
| `AvatarMigrationSettings.DefaultTolerance` | `0.001` | Structure match distance in avatar-root space |
| `AvatarMigrationMap.TieEpsilon` | `1e-7` | Candidates this close in distance are a tie (left unmatched) |
| `AvatarCopyRun.ScaleDecimals` | `8` | Decimal places of every scale-corrected value |
| `AvatarCopyRun.ArmatureNames` | `armature`, `skeleton`, `root`, `rig` | Names treated as the armature, besides the parent of the hips |
| `AvatarCopyRun.ConverterClass` / `ConverterMethod` | `AvatarDynamicsSetup` / `DoConvertUnityConstraints` | The VRChat SDK's constraint converter, found by name |

IconCap:

| Constant | Value | Meaning |
|---|---|---|
| `IconCapRenderer.MaxSize` | `4096` | Largest capture size |
| `IconCapRenderer.SensorHeight` | `24` mm | Full-frame sensor for the 35, 50 and 85mm lenses |
| `IconCapWindow.WideWidth` | `640` | Window width from which the effect stack sits beside the preview |
| `IconCapWindow.PrefixSuffixRow` / `JobRow` / `RestoreRow` | `460` / `440` / `420` | Window widths below which Prefix/Suffix, the Job row's buttons and Restore's buttons go on a second line |
| `IconCapWindow.RowMargin` | `40` | Scroll bar and margins left out of a job row's width; a longer row puts its folder, count and Run on a second line |
| `IconCapWindow.PreviewMaxSide` / `PreviewMaxPixels` | `512` / `1024` | Largest preview on screen / in pixels |
| `IconCapWindow.PreviewInterval` | `0.1` s | Shortest time between live preview renders |
| `IconCapWindow.SmallThumbnail` / `LargeThumbnail` | `64` / `128` | Icon Generator source and result size (**Large previews**) |
| `MenuToolScanner.MaxControls` | `8` | Controls per menu (VRChat's limit); more are paged by VRCFury, otherwise flagged |
| `BudgetCount.MaxBits` / `MaxParameters` | `256` / `8192` | VRChat's synced bits and custom expression parameters |
| `MenuToolCompressor.BatchTime` | `0.1` s | VRCFury's time per compressed batch, used to pick between control type sets (a later set only if it halves the sync time) |
| `MenuToolCompressor.MaxCost()` | `256` (`MAX_PARAMETER_COST`), less `19` with DexProtect | VRCFury's limit for compression |
| `MenuToolWindow.MinWidth` / `MinHeight` | `640` / `760` | Menu Tool's own window; the Menu manager asks for this width in the Main Menu too |
| `MenuToolWindow.WriteLog` | `8` | Parameter writes listed under the radial |

## Menu paths

Defined in `UI/RiryMenus.cs`. Constraint Tool's and Menu Tool's entries are in
`VRChat/RiryVRChatMenus.cs` and use `RiryMenus.ConstraintTool`,
`RiryMenus.ConstraintTrainHierarchy`, `RiryMenus.ConstraintBezierHierarchy` and `RiryMenus.MenuTool`. The number is the priority.

- **Tools** (one entry or submenu per tool):
  - `Tools/RiryTools/Main Menu` (1990): the dockable RiryTools window, a tab per tool and a Tools tab opening IconCap and Avatar Migration Assistant in their own windows, with Reload domain and the Stop Auto Game toggle (Documentation link at its bottom right; a Menu Tool tab, no Menu Mockup tab)
  - `Tools/RiryTools/AutoSync/Sync` (2000), check mark
  - `Tools/RiryTools/AutoSync/Configuration` (2001)
  - `Tools/RiryTools/Avatar Migration Assistant` (2005), written `Avatar Migratio&n Assistant` so its menu key is N; without the VRChat SDK it still runs, and opening it shows the VRC SDK not detected popup
  - `Tools/RiryTools/Blendshape Sync` (2010): Add Job (a new job in its popup)
  - `Tools/RiryTools/Clean Animation Keys` (2020)
  - `Tools/RiryTools/Constraint Tool` (2030), written `Constraint\u00A0&Tool` so its menu key is T: the Constraint Tool window on the tab used last
  - `Tools/RiryTools/GUID Tools` (2040): the window, with Regenerate GUID, Repair Missing GUID References, Check for Broken References and Delete GUID Reference Cache
  - `Tools/RiryTools/IconCap` (2045), written `Ic&onCap` so its menu key is O: the IconCap window (also from the Main Menu's Tools tab, in its own window)
  - `Tools/RiryTools/Import Presets/Set Folder Import Presets` (2050)
  - `Tools/RiryTools/Import Presets/Save Import Preset` (2051)
  - `Tools/RiryTools/Import Presets/Extract Animation` (2052): the Extract Animation window
  - `Tools/RiryTools/Import Presets/Open Import Preset Library` (2053)
  - `Tools/RiryTools/Reload domain` (2058), menu key R: `EditorUtility.RequestScriptReload` (also a button on the Main Menu's Tools tab)
  - `Tools/RiryTools/Stop Auto Game` (2060), check mark
  - `Tools/RiryTools/Menu Mockup` (2065): opens the Edit and Components windows
  - `Tools/RiryTools/Menu Tool` (2066), written `M&enu Tool` so its menu key is E: the Menu Tool window (the Main Menu's Menu Tool tab shows its Parameter budget; its Menu manager opens this window); without the VRChat SDK, the **VRCSDK not found** popup
  - `Tools/RiryTools/Documentation` (2070): opens `RiryMenus.DocumentationLink`
- **Project window right-click:**
  - `Assets/RiryTools/Regenerate GUID` (2000)
  - `Assets/RiryTools/Set Folder Import Presets` (2001): the small popup (one preset per folder, or for all)
  - `Assets/RiryTools/Save Import Preset` (2002)
  - `Assets/RiryTools/Extract Animation` (2003): the Extract Animation window with the selected model; enabled only for a model with embedded animations
  - `Assets/RiryTools/Repair Missing GUID References` (2004)
  - `Assets/RiryTools/Clean Animation Keys` (2005)
- **Hierarchy right-click** (also in the GameObject menu):
  - `GameObject/RiryTools/Attach To` (19): the Attach To popup; enabled when scene objects are selected; in no other menu
  - `GameObject/RiryTools/Constraint Tool/Train` (20): the Train tab, with the selection as the chain
  - `GameObject/RiryTools/Constraint Tool/Bezier` (21): the Bezier tab, with the selection as the chain
- **Git menu:**
  - `Git/Sync` (1), check mark
  - `Git/Configuration` (2)

## Publishing

Source workflow: `riryunityworkflow/.github/workflows/notify-vpm.yml`

| Name | Kind | Value |
|---|---|---|
| `VPM_DISPATCH_TOKEN` | secret | Fine-grained token for `Rionquosue/rirytools_vpm` only, **Actions: Read and write** |
| `GH_TOKEN` | step env | `secrets.VPM_DISPATCH_TOKEN` |
| `CHANNEL` | step env | `stable` for `main`, `beta` for `beta` |
| `SHA` | step env | `github.sha`, the pushed commit |
| `notify-vpm-<branch>` | concurrency group | One run at a time per branch, in push order |

Destination workflow: `rirytools_vpm/.github/workflows/build-package.yml`

| Name | Kind | Value |
|---|---|---|
| `channel` | input, required | `stable` or `beta` |
| `sha` | input | 40 lowercase hex; empty for the head of the channel's branch |
| `version` | input | `X.Y.Z` (stable) or `X.Y.Z-beta.N` (beta), no leading zeros, `Z` at most 99; empty for automatic |
| `SOURCE_REPOSITORY` | workflow env | `Rionquosue/riryunityworkflow` |
| `CHANNEL`, `SHA`, `VERSION` | step env | The inputs, passed only through the environment |
| `GH_TOKEN` | step env | `github.token` |
| `PREVIOUS`, `TAG`, `ZIP` | step env | Step outputs below |
| `steps.inputs.outputs.ref` | step output | `sha`, or the channel's branch |
| `steps.version.outputs.previous` / `version` | step output | From `build_vpm.py plan` |
| `steps.build.outputs.tag` / `zip` / `version` / `digest` | step output | From `build_vpm.py build` |
| `build-package` | concurrency group | Never cancelled |

Destination workflow: `rirytools_vpm/.github/workflows/build-listing.yml`

| Name | Kind | Value |
|---|---|---|
| `listPublishDirectory` | workflow env | `Website` |
| `pathToCi` | workflow env | `ci` |
| `GITHUB_TOKEN` | step env | `github.token` |
| `github-pages` | environment | Pages deployment target; main only, no required reviewers |
| `pages` | concurrency group | Deploy job only |

Destination workflow: `rirytools_vpm/.github/workflows/issue-check.yml`

| Name | Kind | Value |
|---|---|---|
| `TRIPWIRE_HASHES` | repository variable | Comma-separated canary hashes |
| `GITHUB_TOKEN` | step env | `github.token` |
| `issue-check-<issue number>` | concurrency group | One run per issue |

Destination scripts (`rirytools_vpm/scripts/`):

- `build_vpm.py`:
  - `PACKAGE_NAME` = `com.riry.rirytools`: the source folder that is zipped,
    the package name, and the zip name `com.riry.rirytools-<version>.zip`.
  - `FIELDS` = `package.json`: the package fields; `version` and `url` are added.
  - `NUMBER`, `STABLE`, `BETA`: version patterns; `NUMBER` has no leading zeros.
  - `FIRST` = `0.1.0`: the first stable version.
  - `MAX_PATCH` = `99`: the highest patch; the next version is the next minor at patch 0.
  - `LFS_POINTER`: refuses LFS pointer files.
  - `FIXED_TIME` = 2020-01-01: zip timestamps, so builds are reproducible.
- `verify_listing.py`:
  - `REPOSITORY` = `Rionquosue/rirytools_vpm`, `PACKAGES` = `{com.riry.rirytools}`.
  - `WEBSITE`, `SOURCE_JSON`, `TEMPLATED`: what the listing is checked against.
- `issue_check.py`: `REVIEW_SCORE` = `0.45`, `BLOCK_SCORE` = `0.8`, and its label names.
- `triage_read.py`: `REPOSITORY` = `Rionquosue/rirytools_vpm`.
