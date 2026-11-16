# Restructure review

Changes made on `betastaging` after `cf8c34c` to apply the file structure rules in `.instructions~/filestructure.md`. Paths are relative to `com.riry.rirytools/Editor/` unless they start with a repository folder.

## Status

- Not compiled. No `dotnet` or Unity reference assemblies were available, so the compile check in `for_agents.md` was not run.
- Checked after every commit: brace balance of every script, a `.meta` for every file and folder (moved files keep their GUID), every script path in code comments and `docs/variables.md` exists, and the tree in `filestructure.md` matches the package.
- Still to test in Unity: no SDK, VRChat SDK only, and VRChat SDK with Gesture Manager.

## Commits

| Commit | Scope |
| --- | --- |
| `45f674c` | Instructions: rules, tool name table, entry points, target tree |
| `4efb5bf` | AutoSync |
| `f4d90c3` | Avatar Migration Assistant |
| `4119e66` | Blendshape Sync |
| `91a52c0` | Clean Animation Keys |
| `be717d7` | GUID Tools |
| `aa43bd1` | IconCap |
| `63360ea` | Import Presets |
| `f57a024` | Debug |
| `aa6f5bb` | Utilities |
| `267b5b2` | Shared UI components |
| `70fb03e` | Find and Replace |
| `9629628` | Constraint Tool |
| `96fa63d` | Menu Tool and the Gesture Manager bridge |
| `bdd0795` | Folder check: `Editor/` and `UI/` |
| `5ede152` | Folder check: `.instructions~/` |
| `0171425` | Folder check: `docs/` and `README.md` |

## Instructions

- `filestructure.md` has the rules: assemblies, logic folders, category folders (`Debug/`, `Utilities/`, `AnimatorTools/`), `UI/<folder>/` for every window, `Components/` for shared code, one job per script, names, and moves. It also has the tool name table (display name, folder, code prefix), an entry point map and the script tree.
- Main Menu, menu bar and right-click scripts sit directly in `UI/` (and `VRChat/UI/RiryToolsVRChatMenus.cs` in `VRChat/UI/`).
- Exception to the prefix rule: `IconGenerator` keeps its name, because users' `.asset` files store it.
- `important_information.md` follows the same rules. `menustructure.md` uses Refresh Scripts and the canonical spellings (AutoSync, Menu Tool, GUID Tools). `fixes_and_features.md` merges GUID Tool / GUID Tools, and Reload Domain into Refresh Scripts.

## Changes by tool

### AutoSync

- Git code used by several tools moves to `Components/`: `GitRunner` (was `AutoSyncGitRunner`), `GitState`, `GitNotInstalledException`.
- `GitRunner.Result.RequireSuccess(action)` replaces the identical `Check` helpers in `AutoSyncGitActions`, `AutoSyncJob` and the large-file rewrite, and Git LFS's copy.
- Split and prefixed: `AutoSyncGitTemplates`, `AutoSyncGitLfs`, `AutoSyncGitLargeFiles`, `AutoSyncGitLfsQuery`, `AutoSyncSaveProcessor`.
- Windows in `UI/AutoSync/`: `AutoSyncGitConfigureWindow(.UI)` and `AutoSyncGitLargeFilesWindow(.UI)` (was `LargeFilesWindow`).

### Avatar Migration Assistant

- Folder `AvatarMigrationAssistant/` is `AvatarMigrator/`; types carry the `AvatarMigrator` prefix. The display name stays Avatar Migration Assistant.
- `AvatarMigratorCopyRun` partials are named `AvatarMigratorCopyRun.<Role>.cs`; the VRCFury mover is `AvatarMigratorRetargetAnim.VRCFuryMover.cs`.
- Split: `AvatarMigratorPrefabScope`, `AvatarMigratorRepack`, `AvatarMigratorYaml`.
- Rotation and scale corrections share VRC constraint source pairing (`AvatarMigratorCopyRun.VrcSources.cs`); each correction stays separate.
- Windows in `UI/AvatarMigrator/`: `AvatarMigratorWindow`, `AvatarMigratorPreFlightWindow`, `AvatarMigratorRunReportWindow`, `AvatarMigratorMatchWindow` (was `ManualMatching.cs`), `AvatarMigratorSdkNoticeWindow` (was `UI/Components/SdkNotice.cs`).
- Saved keys renamed to `RiryTools.AvatarMigrator.Settings` and `RiryTools.AvatarMigrator.SdkNoticeOff`.

### Blendshape Sync

- `BlendshapeSyncJob.cs` and `BlendshapeSyncJobs.cs` move to `BlendshapeSync/`. `BlendshapeSyncJob.cs` draws no UI, so it stays a logic file under its name.
- The window's logic moves into partials in `BlendshapeSync/`: `BlendshapeSyncWindow.Checks.cs`, `.Plan.cs`, `.Sync.cs`. `UI/BlendshapeSync/` keeps state, lifecycle and drawing.
- `BlendshapeSyncJobList` (the Main Menu tab) gets its own file.

### Clean Animation Keys

- Windows in `UI/AnimatorTools/`, one per file: `CleanAnimationKeysWindow`, `CleanAnimationKeysWhitelistWindow`, `CleanAnimationKeysButton`.
- The Tools entry is Tools > RiryTools > Animator Tools > Clean Animation Keys (priority 1994, before Find and Replace).

### GUID Tools

- `GuidToolReferenceCache` owns the cache: path (`Library/RiryTools/GuidReferenceCache`), format, load, save, and a one-pass `Measure` plus `Delete`. The window button runs `GuidToolClearReferenceCache` (`UI/GuidTools/`), which asks and reports.
- `GuidToolRegenerate` is a partial class: data and setup, `.Scan.cs`, `.Patch.cs`, `.Rollback.cs` in `GuidTools/`, and the run's prompts and progress in `UI/GuidTools/GuidToolRegenerate.Prompts.cs`. `GuidToolLog` has its own file.
- Regeneration and repair share `GuidToolBytes.Matches`.
- Split and prefixed: `GuidToolMetaHistoryParser`, `GuidToolBrokenReferenceSaveGuard`, `GuidToolRepairPlan`, `GuidToolRepairCache`, `GuidToolRepairCandidate`, `GuidToolBreakDecision`.
- Windows in `UI/GuidTools/`: `GuidToolsWindow`, `GuidToolBrokenReferenceWindow(.UI)`, `GuidToolBrokenSaveWindow`.

### IconCap

- Windows in `UI/IconCap/`. `IconCapWindow` is split into state and actions (`IconCapWindow.cs`) and drawing (`IconCapWindow.UI.cs`); `IconCapNamePrompt` has its own file.

### Import Presets

- Windows in `UI/ImportPresets/`, one per file with state apart from drawing: `ImportPresetFolderWindow(.UI)`, `ImportPresetSaveWindow(.UI)`, `ImportPresetQuickWindow`, `ImportPresetExtractAnimationWindow` (was `ExtractAnimationWindow`).
- `ImportPresetSaveWindow.Open` and `UseImporter` share `DefaultPresetName`.

### Debug

- Menu Mockup moves into `Debug/`, split by job: `MenuMockup` (with `MenuMockupBlock`), `MenuMockupTypes`, `MenuMockupFile`, `MenuMockupModel`. Its windows are in `UI/Debug/`, one per file.
- `FindNullReferencesWindow` and `ScriptLoadGuardFailureWindow` (was `ScriptLoadFailureWindow` in `UI/Components/`) are in `UI/Debug/`. `ScriptLoadGuardSaveBlocker` has its own file.

### Utilities

- Attach To (`AttachTo`, `AttachToBones`, `AttachToAvatarTracker`), Stop Auto Game and Refresh Scripts move into `Utilities/`; the popup is `UI/Utilities/AttachToWindow` (was `AvatarAttachWindow`).
- `RefreshScripts.DisplayName` is shared by the Tools menu path and the Main Menu button; `RefreshScripts.cs` has its glossary.

### Shared UI

- `UI/Components/` holds `RiryToolGUI` and `IRiryHostedTool` (moved out of `RiryToolsMainMenu.cs`).

### Find and Replace

- Window in `VRChat/UI/AnimatorTools/`.
- `RiryToolsMenuBar.FindReplaceLabel` builds the Tools, Project and Hierarchy paths, so all three read Animator Tools > Find and Replace.

### Constraint Tool

- `VRChat/Components/` holds what the subtools share: `ConstraintToolChains` (`TryCollectChain`, `CreateChain`, `EndpointSuffix`, `DeepestSelectedBelow`), `ConstraintToolSources` (`SourceWeightProperty`, `AddSources`, `ApplyDefaults`) and `ConstraintToolObjects` (`CreateEmpty`). `FreezeToWorld`, Undo grouping and prefab recording stay with each caller.
- The Main Menu hosts `ConstraintToolWindow` through one hook (`CreateConstraintTool`), so `ConstraintToolWindow.Tabs` is the only tab list and the tool lives entirely in the VRChat assembly. Hosted, the window sizes its sub-tabs to the Main Menu; `Open` never picks the hosted copy.
- `InsertablePropMath.Reach()`, which nothing called, is removed.
- Windows in `VRChat/UI/ConstraintTools/`.

### Menu Tool

- `MenuToolPlanner`: `CheckCapacity` is shared by moves and both adds; `CheckNewItem`, `CanAddNative` and `CanAddMa` are shared by Add menu and Add New Control. The VRCFury toggle rule stays in `CheckAddControl`. Messages are unchanged.
- `MenuToolScanner` is split along its sections: `MenuToolScanner.cs`, `.Native.cs`, `.ModularAvatar.cs`, `.VRCFury.cs`. `MenuToolVrcfuryMenu` has its own file.
- Gesture Manager bridge: `GestureManagerMenuPath` shares `RadialMenu._menuPath` / `OpenMenu` and the clear-and-reopen used by the preview and the Play Mode entry state.
- Split: `MenuToolAvatarKeys`, `MenuToolPlanner`, `MenuToolCompressor`, `IMenuToolRadial` (`MenuToolPreview.cs` is now `MenuToolPreviewMenus.cs`), and `MenuToolTrees.cs` into `MenuToolIcons`, `MenuToolBudgetTree`, `MenuToolManagerTree`.
- Types carry the `MenuTool` prefix (`MenuToolEntry`, `MenuToolScan`, `MenuToolPlan`, `MenuToolMoveEdit` and the rest). Saved plan JSON stores no type names, so existing plans still load.
- Windows in `VRChat/UI/MenuTool/`.

### Docs

- `README.md` points at `docs/About/Tools.md` and no longer links the removed package README or VPM Publishing design note.
- The seven tutorial links into the removed `docs/design/` are dropped.
- `variables.md`: Refresh Scripts is a canonical tool name, `RiryTools.RefreshScripts.Reopen` is in the SessionState table, every script path is current, and every table is in A-Z order.

## Behaviour changes to test

1. Clean Animation Keys moved in the Tools menu to Animator Tools > Clean Animation Keys.
2. The Project and Hierarchy right-click entries read Animator Tools > Find and Replace.
3. Avatar Migration Assistant settings and its "Do not show again" choice reset once (saved keys renamed; no migration was asked for).
4. Windows whose class was renamed don't restore from a saved layout, and Refresh Scripts skips them, the first time.
5. The Main Menu's Constraint Tool tab hosts the whole Constraint Tool window: check sub-tab width, hover, and that Tools > RiryTools > Constraint Tool opens its own window.
6. Menu Tool: moves, Add menu, Add New Control and the radial preview, with and without Gesture Manager.

## Not done or different from the findings

1. Import Presets: `Open()` does not call `UseImporter()`. `UseImporter()` clears the message that `OnEnable` sets when the library can't be read, so the shared part is the `DefaultPresetName` helper instead.
2. Logic still inside window files: Bezier rig generation, Train constraint setup and clip writing. `GuidToolReferenceRepair.cs` (about 840 lines) still holds scan, suggestions and apply. The scan and apply loops in GUID regeneration still show their own progress bars.
3. About 35 scripts have no GLOSSARY block. They had none before; `for_agents.md` says glossaries are updated only when instructed.
4. Data-model files hold several types each (`MenuToolModel.cs`, `MenuToolPlan.cs`, `IconCapData.cs`, `AvatarMigratorMatchObjects.cs` and similar), kept as one job each.
5. `GestureManagerNotFoundException` stays in `Components/` although only Menu Tool uses it.
6. Refresh Scripts is in `Utilities/` as asked, while the menu spec lists it under Debug Tools.
7. The Main Menu's tab order doesn't match `menustructure.md`; it wasn't part of this work.
