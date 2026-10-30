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
- **Tool names:** AutoSync, Blendshape Sync, Clean Animation Keys, Constraint
  Train, GUID Tools, Import Presets, Stop Auto Game. Class, file, folder and
  document names follow them.

## Stored settings

EditorPrefs: per user, shared by every project.

| Key | Type | Default | Owner |
|---|---|---|---|
| `RiryTools.StopAutoGame.Enabled` | bool | `true` | `StopAutoGame.Enabled` |
| `RiryTools.BetterFbx.BlenderPath` | string | `""` (detect) | `BetterFbxExporter.BlenderPath` |
| `RiryTools.BetterFbx.Options` | string | `""` | `BetterFbxExporter.Options` |

EditorUserSettings: per user and per project, in `UserSettings/`.

| Key | Values | Default | Owner |
|---|---|---|---|
| `RiryTools.AutoSync.Enabled` | `1` / `0` | `0` | `AutoSync.Enabled` (**Sync**) |
| `RiryTools.AutoSync.Remote` | remote name | unset: `origin`, else the first remote | `GitRemotes.Selected` (**Remote**) |
| `RiryTools.AutoSync.LargeFileNoticeOff` | remote URLs, one per line | unset | `AutoSync.LargeFileNotice`: **Do Not Show Again** |

SessionState: until the editor quits; survives script reloads.

| Key | Type | Owner |
|---|---|---|
| `RiryTools.AutoSync.Pending` | one save per line: title, then paths, tab-separated | `AutoSync`: saves not yet committed when scripts reloaded |
| `RiryTools.AutoSync.LargeFileNoticeShown` | remote URLs, one per line | `AutoSync.LargeFileNotice`: **Push Held** was shown this session |
| `RiryTools.StopAutoGame.FocusedWindows` | comma-separated instance IDs | `StopAutoGame`: windows focused when Play Mode started |
| `RiryTools.StopAutoGame.GameViewWasOpen` | bool | `StopAutoGame`: a Game view existed before Play Mode |
| `RiryTools.ScriptLoadGuard.Guarded` | newline-separated asset paths | `ScriptLoadGuard` |

Window fields marked `[SerializeField]` survive script reloads while the window
is open. Each window file lists them in its glossary.

## Files and folders

In the project:

| Path | Contents | Owner |
|---|---|---|
| `Assets/RiryTools/` | Files unpacked from the package's `ProjectFolder~`, Constraint Train clips, `_ROOT` prefab variants | `RiryToolsProjectFolder.AssetPath` |
| `Library/RiryTools/GuidReferenceCache/` | `<name>_<hash>.rguiddb` reference caches | `RegenerateGuid.CacheDirectoryPath` |
| `Library/RiryTools/ImportPresets/` | `owned.txt`, `Pending/<sha1>.txt`, `better_fbx_export.py`, `previous.fbx` | `ImportPresetOwnedFiles`, `ImportPresetAnimations`, `BetterFbxExporter` |
| `Logs/RiryTools/GuidTools/` | `guid-tools.log`, `archive/guid-tools_<UTC time>.log` | `RegenerateGuid.LogDirectoryPath`, `GuidToolLog` |
| `ProjectSettings/RiryToolsGuidRemap.tsv` | GUID remap ledger; keep it in version control | `GuidRemapLedger.RelativePath` |
| `.gitignore`, `.gitattributes` | Copied from a template by **Sync** (new repository) and **Configuration** | `GitActions.CopyTemplate` |
| `Library/RiryTools/AutoSync/rewrite-index` | Temporary index while **Large Files** rewrites commits | `GitLargeFiles.Ignore` |
| `<folder>/ImportPreset.md` | Import preset rules for a folder | `ImportPresetMarker.FileName` |
| `<model folder>/Animations/<Model>_<Clip>.anim` | Extracted clips | `ImportPresetAnimations.FolderName` |
| `<name>.fbx` beside `<name>.blend` | Better FBX export | `BetterFbxExporter.FbxPathFor` |
| `<model>_ROOT.prefab` | Constraint Train prefab variant | `ConstraintTrainPrefabs.RootSuffix` |

In the user's Documents folder (`%USERPROFILE%\Documents` on Windows, including
a OneDrive redirect; `~/Documents` elsewhere):

| Path | Contents | Owner |
|---|---|---|
| `Documents/RiryTools/ImportPresets/<configuration>/` | `*.preset`, optional `config.json` | `ImportPresetLibrary.RootPath` |
| `Documents/RiryTools/ImportPresets/README.txt` | How the library works | `ImportPresetLibrary.EnsureRoot` |
| `Documents/RiryTools/GitTemplates/` | `<name>.gitignore`, `<name>.gitattributes` | `GitTemplates.RootPath` |

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
| Assembly `Riry.RiryTools.Editor` | Editor only, auto-referenced | All tools except the VRChat window |
| Assembly `Riry.RiryTools.Editor.VRChat` | Editor only, needs `VRC.Dynamics.dll` and `VRC.SDK3.Dynamics.Constraint.dll` | Constraint Train window and menu |
| `InternalsVisibleTo("Riry.RiryTools.Editor.VRChat")` | `AssemblyInfo.cs` | Lets the VRChat assembly use internal types and menu paths |

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

Import Presets and Better FBX:

| Constant | Value | Meaning |
|---|---|---|
| `ImportPresetMarker.FileName` | `ImportPreset.md` | Folder marker |
| `ImportPresetMarker.DefaultKey` / `AnimationsKey` / `BetterFbxKey` | `preset` / `animations` / `betterfbx` | Marker keys; reserved, so not usable as file types |
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
| `ConstraintTrainPlanner.MaxChains` | `5` | Physics chains |
| `ConstraintTrainChains.RestPrefix` / `ChainPrefix(n)` / `EndpointSuffix` | `Rest_` / `Phys<n>_` / `_End` | Generated chain names |
| `ConstraintTrainWindow.UndoName` | `Constraint Train` | Undo group name |
| `StopAutoGame.HandleDelayMs` | `100` | Delay after Play Mode starts before focus is restored |
| `RiryMenus.DocumentationLink` | `https://github.com/Rionquosue/riryunityworkflow` | Opened by **Documentation**; change it when the wiki is up |
| `RiryToolsProjectFolder.SourceFolderName` | `ProjectFolder~` | Package folder unpacked into `Assets/RiryTools` |

## Menu paths

Defined in `UI/RiryMenus.cs`. Constraint Train's entries are in
`VRChat/RiryVRChatMenus.cs` and use `RiryMenus.ConstraintTrain` and
`RiryMenus.ConstraintTrainHierarchy`. The number is the priority.

- **Tools** (one entry or submenu per tool):
  - `Tools/RiryTools/AutoSync/Sync` (2000), check mark
  - `Tools/RiryTools/AutoSync/Configuration...` (2001)
  - `Tools/RiryTools/Blendshape Sync` (2010)
  - `Tools/RiryTools/Clean Animation Keys` (2020)
  - `Tools/RiryTools/Constraint Train` (2030), written `Constraint\u00A0&Train` so its menu key is T
  - `Tools/RiryTools/GUID Tools/GUID Tools` (2040)
  - `Tools/RiryTools/GUID Tools/Regenerate GUID` (2041)
  - `Tools/RiryTools/GUID Tools/Repair Missing GUID References` (2042)
  - `Tools/RiryTools/GUID Tools/Check for Broken References...` (2043)
  - `Tools/RiryTools/GUID Tools/Delete GUID Reference Cache` (2044)
  - `Tools/RiryTools/Import Presets/Import Preset...` (2050)
  - `Tools/RiryTools/Import Presets/Save Import Settings to Preset Library...` (2051)
  - `Tools/RiryTools/Import Presets/Extract Animations (Generic)` (2052)
  - `Tools/RiryTools/Import Presets/Open Import Preset Library` (2053)
  - `Tools/RiryTools/Stop Auto Game` (2060), check mark
  - `Tools/RiryTools/Documentation` (2070): opens `RiryMenus.DocumentationLink`
- **Project window right-click:**
  - `Assets/RiryTools/Regenerate GUID` (2000)
  - `Assets/RiryTools/Import Preset...` (2001)
  - `Assets/RiryTools/Save Import Settings to Preset Library...` (2002)
  - `Assets/RiryTools/Extract Animations (Generic)` (2003)
  - `Assets/RiryTools/Repair Missing GUID References` (2004)
  - `Assets/RiryTools/Clean Animation Keys` (2005)
- **Hierarchy right-click:** `GameObject/RiryTools/Constraint Train...` (20)
- **Git menu:**
  - `Git/Sync` (1), check mark
  - `Git/Configuration...` (2)
  - `Git/Check for Broken References...` (20)

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
| `version` | input | `X.Y.Z` (stable) or `X.Y.Z-beta.N` (beta), no leading zeros; empty for automatic |
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
| `github-pages` | environment | Deployment waits for the owner's approval |
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
  - `LFS_POINTER`: refuses LFS pointer files.
  - `FIXED_TIME` = 2020-01-01: zip timestamps, so builds are reproducible.
- `verify_listing.py`:
  - `REPOSITORY` = `Rionquosue/rirytools_vpm`, `PACKAGES` = `{com.riry.rirytools}`.
  - `WEBSITE`, `SOURCE_JSON`, `TEMPLATED`: what the listing is checked against.
- `issue_check.py`: `REVIEW_SCORE` = `0.45`, `BLOCK_SCORE` = `0.8`, and its label names.
- `triage_read.py`: `REPOSITORY` = `Rionquosue/rirytools_vpm`.
