# File structure

## Repository

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.instructions~/`: Working instructions for LLMs while maintaining this repo. Start with `for_agents.md`.
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `About/` (`Tools.md`, `CHANGELOG.md`), `tutorials/`.
- `com.riry.rirytools/`: Is the extension package; `ProjectFolder~/` is copied into the user's `Assets/RiryTools` when imported.

## Rules

1. **Assemblies.** Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it: `Editor/` (Unity only), `Editor/VRChat/` (VRChat SDK) and `Editor/VRChat/MenuTool/GestureManager/` (Gesture Manager). Code that relies on a dependency goes in that dependency's folder. A tool that needs a dependency for any part of it keeps all of its code in that dependency's folder (Constraint Tool lives entirely in `Editor/VRChat/`).
2. **Logic folders.** A tool's logic is in its own folder, named after the tool (`AutoSync/`, `AvatarMigrator/`, `BlendshapeSync/`, `GuidTools/`, `IconCap/`, `ImportPresets/`, `MenuTool/`, `ConstraintTools/`).
3. **Category folders.** A tool that is a single script with a single job does not get its own folder. It goes in a category folder: `Debug/` (debugging aids), `Utilities/` (general helpers) or `AnimatorTools/`.
4. **UI folders.** Every file that draws UI is in its assembly's `UI/` folder, in a subfolder with the same name as the tool's logic or category folder (`UI/AutoSync/`, `UI/Debug/`, `UI/Utilities/`). Main Menu, the Tools menu bar and the right click menu (and the VRChat assembly's menu entries) sit directly in `UI/` so they are easy to find and edit.
5. **Components.** Code reused by several tools, or by several subtools of one tool, goes in that assembly's `Components/` folder. Shared drawing code goes in `UI/Components/`.
6. **One job per script.** Each script does one job, so no script becomes too large to maintain. Example: a tool that enumerates the avatar hierarchy into a searchable index, then applies changes based on a search of that index, has one script for the index and search, and another for applying the changes.
7. **Names.** File names match the main type they hold. A class split over several files uses `Type.Role.cs` (`BlendshapeSyncWindow.UI.cs`). Every type and file carries its tool's code prefix (below).
8. **Moves.** Always move a file's `.meta` with it (`git mv` both) so its GUID is kept. New files get a `.meta` with a fresh GUID. The namespace is `Riry.RiryTools.Editor` in every folder and does not follow the folder.

### Tool names

Display names are what the user sees (menus, window titles, docs, `docs/variables.md`). Folders and code use the folder and code prefix.

| Display name | Folder | Code prefix |
| --- | --- | --- |
| Attach To | `Utilities/` | `AttachTo` |
| AutoSync | `AutoSync/` | `AutoSync` |
| Avatar Migration Assistant | `AvatarMigrator/` | `AvatarMigrator` |
| Blendshape Sync | `BlendshapeSync/` | `BlendshapeSync` |
| Clean Animation Keys | `AnimatorTools/` | `CleanAnimationKeys` |
| Constraint Tool (Train, Bezier, Insertable prop) | `VRChat/ConstraintTools/` | `ConstraintTool`, `ConstraintTrain`, `ConstraintBezier`, `InsertableProp` |
| Find and Replace | `VRChat/AnimatorTools/` | `BehaviourFindReplace` |
| Find Null references | `Debug/` | `FindNullReferences` |
| GUID Tools | `GuidTools/` | `GuidTool` |
| IconCap | `IconCap/` | `IconCap` |
| Import Presets | `ImportPresets/` | `ImportPreset` |
| Menu Mockup | `Debug/` | `MenuMockup` |
| Menu Tool | `VRChat/MenuTool/` | `MenuTool` |
| Refresh Scripts | `Utilities/` | `RefreshScripts` |
| Script Load Guard | `Debug/` | `ScriptLoadGuard` |
| Stop Auto Game | `Utilities/` | `StopAutoGame` |

## Entry points

Paths are relative to `com.riry.rirytools/Editor/`.

- **Main Menu:** `UI/RiryToolsMainMenu.cs`. Hosts each tool's window in a tab. VRChat tools are handed to it by `VRChat/UI/RiryToolsVRChatMenus.cs`.
- **Tools > RiryTools menu:** `UI/RiryToolsMenuBar.cs`; the VRChat entries are in `VRChat/UI/RiryToolsVRChatMenus.cs`.
- **Right click menus (Hierarchy, Project):** `UI/RiryToolsRightClickMenu.cs`.
- **Shared code:** `Components/` (logging, paths, project folder, fuzzy search, type lookup, hierarchy paths, Git runner and state, missing-SDK and missing-Git exceptions), `UI/Components/RiryToolGUI.cs` (shared drawing and Undo for window settings).
- **A tool:** its logic folder and its `UI/<folder>/` window, per the table above.

## Script locations

```text
com.riry.rirytools/
└── Editor/
    ├── AnimatorTools/
    │   └── CleanAnimationKeys.cs
    ├── AutoSync/
    │   ├── AutoSync.cs
    │   ├── AutoSyncGitActions.cs
    │   ├── AutoSyncGitLargeFiles.cs
    │   ├── AutoSyncGitLfs.cs
    │   ├── AutoSyncGitLfsQuery.cs
    │   ├── AutoSyncGitRemotes.cs
    │   ├── AutoSyncGitTemplates.cs
    │   ├── AutoSyncJob.cs
    │   └── AutoSyncSaveProcessor.cs
    ├── AvatarMigrator/
    │   ├── AvatarMigratorCopyRun.CorrectRotation.cs
    │   ├── AvatarMigratorCopyRun.CorrectScale.cs
    │   ├── AvatarMigratorCopyRun.cs
    │   ├── AvatarMigratorCopyRun.Prefabs.cs
    │   ├── AvatarMigratorCopyRun.RepackPrefabs.cs
    │   ├── AvatarMigratorCopyRun.RepointReferences.cs
    │   ├── AvatarMigratorCopyRun.TypeRules.cs
    │   ├── AvatarMigratorCopyRun.UpgradeConstraints.cs
    │   ├── AvatarMigratorCopyRun.VrcSources.cs
    │   ├── AvatarMigratorCopyTypes.cs
    │   ├── AvatarMigratorIgnoreList.cs
    │   ├── AvatarMigratorMatchObjects.cs
    │   ├── AvatarMigratorMigratedFolder.cs
    │   ├── AvatarMigratorPrefabScope.cs
    │   ├── AvatarMigratorPreFlightAnswers.cs
    │   ├── AvatarMigratorRepack.cs
    │   ├── AvatarMigratorRetargetAnim.cs
    │   ├── AvatarMigratorRetargetAnim.VRCFuryMover.cs
    │   ├── AvatarMigratorRunReport.cs
    │   ├── AvatarMigratorSettings.cs
    │   ├── AvatarMigratorYaml.cs
    │   └── types.yml
    ├── BlendshapeSync/
    │   ├── BlendshapeSyncJob.cs
    │   ├── BlendshapeSyncJobs.cs
    │   ├── BlendshapeSyncWindow.Checks.cs
    │   ├── BlendshapeSyncWindow.Plan.cs
    │   └── BlendshapeSyncWindow.Sync.cs
    ├── Components/
    │   ├── FuzzyMatch.cs
    │   ├── GestureManagerNotFoundException.cs
    │   ├── GitNotInstalledException.cs
    │   ├── GitRunner.cs
    │   ├── GitState.cs
    │   ├── HierarchyPath.cs
    │   ├── RiryLog.cs
    │   ├── RiryToolsPaths.cs
    │   ├── RiryToolsProjectFolder.cs
    │   ├── RiryToolsTypes.cs
    │   └── VrcSdkNotFoundException.cs
    ├── Debug/
    │   ├── FindNullReferences.cs
    │   ├── MenuMockup.cs
    │   └── ScriptLoadGuard.cs
    ├── GuidTools/
    │   ├── GuidToolBrokenReferenceMonitor.cs
    │   ├── GuidToolReferenceCache.cs
    │   ├── GuidToolReferenceRepair.cs
    │   ├── GuidToolRegenerate.cs
    │   └── GuidToolRemapLedger.cs
    ├── IconCap/
    │   ├── Shaders/
    │   │   ├── IconCapDraw.shader
    │   │   └── IconCapEffects.shader
    │   ├── IconCapData.cs
    │   ├── IconCapEffects.cs
    │   ├── IconCapFiles.cs
    │   ├── IconCapRenderer.cs
    │   └── IconGenerator.cs
    ├── ImportPresets/
    │   ├── ImportPresetActions.cs
    │   ├── ImportPresetAnimationExtraction.cs
    │   ├── ImportPresetAnimations.cs
    │   ├── ImportPresetBetterFBX.cs
    │   ├── ImportPresetFolderRules.cs
    │   ├── ImportPresetLibrary.cs
    │   ├── ImportPresetMarker.cs
    │   ├── ImportPresetOwnedFiles.cs
    │   ├── ImportPresetPostprocessor.cs
    │   └── ImportPresetPreferences.cs
    ├── UI/
    │   ├── AnimatorTools/
    │   │   └── CleanAnimationKeysWindow.cs
    │   ├── AutoSync/
    │   │   ├── AutoSyncGitConfigureWindow.cs
    │   │   ├── AutoSyncGitConfigureWindow.UI.cs
    │   │   ├── AutoSyncGitLargeFilesWindow.cs
    │   │   └── AutoSyncGitLargeFilesWindow.UI.cs
    │   ├── AvatarMigrator/
    │   │   ├── AvatarMigratorMatchWindow.cs
    │   │   ├── AvatarMigratorPreFlightWindow.cs
    │   │   ├── AvatarMigratorRunReportWindow.cs
    │   │   ├── AvatarMigratorSdkNoticeWindow.cs
    │   │   └── AvatarMigratorWindow.cs
    │   ├── BlendshapeSync/
    │   │   ├── BlendshapeSyncJobList.cs
    │   │   ├── BlendshapeSyncJobWindow.cs
    │   │   ├── BlendshapeSyncWindow.cs
    │   │   └── BlendshapeSyncWindow.UI.cs
    │   ├── Components/
    │   │   └── RiryToolGUI.cs
    │   ├── Debug/
    │   │   ├── FindNullReferencesWindow.cs
    │   │   ├── MenuMockupWindows.cs
    │   │   └── ScriptLoadFailureWindow.cs
    │   ├── GuidTools/
    │   │   ├── GuidToolBrokenReferenceWindow.UI.cs
    │   │   ├── GuidToolBrokenReferenceWindow.cs
    │   │   └── GuidToolsWindow.cs
    │   ├── IconCap/
    │   │   ├── IconCapEffectStackGUI.cs
    │   │   └── IconCapWindow.cs
    │   ├── ImportPresets/
    │   │   ├── ImportPresetExtractAnimationWindow.cs
    │   │   ├── ImportPresetFolderWindow.UI.cs
    │   │   ├── ImportPresetFolderWindow.cs
    │   │   ├── ImportPresetQuickWindow.cs
    │   │   ├── ImportPresetSaveWindow.UI.cs
    │   │   └── ImportPresetSaveWindow.cs
    │   ├── Utilities/
    │   │   └── AttachToWindow.cs
    │   ├── RiryToolsMainMenu.cs
    │   ├── RiryToolsMenuBar.cs
    │   └── RiryToolsRightClickMenu.cs
    ├── Utilities/
    │   ├── AttachTo.cs
    │   ├── RefreshScripts.cs
    │   └── StopAutoGame.cs
    ├── VRChat/
    │   ├── AnimatorTools/
    │   │   └── BehaviourFindReplace.cs
    │   ├── Components/
    │   ├── ConstraintTools/
    │   │   ├── ConstraintBezier/
    │   │   │   └── ConstraintBezierCurve.cs
    │   │   ├── ConstraintTrain/
    │   │   │   ├── ConstraintTrainChains.cs
    │   │   │   ├── ConstraintTrainPlanner.cs
    │   │   │   └── ConstraintTrainPrefabs.cs
    │   │   └── InsertableProp/
    │   │       ├── InsertablePropAnimator.cs
    │   │       ├── InsertablePropMath.cs
    │   │       └── InsertablePropRig.cs
    │   ├── MenuTool/
    │   │   ├── GestureManager/
    │   │   │   ├── GestureManagerEntryState.cs
    │   │   │   ├── GestureManagerRadial.cs
    │   │   │   └── Riry.RiryTools.Editor.GestureManager.asmdef
    │   │   ├── MenuToolApply.cs
    │   │   ├── MenuToolBudget.cs
    │   │   ├── MenuToolEntryState.cs
    │   │   ├── MenuToolModel.cs
    │   │   ├── MenuToolPlan.cs
    │   │   ├── MenuToolPreview.cs
    │   │   └── MenuToolScanner.cs
    │   ├── UI/
    │   │   ├── AnimatorTools/
    │   │   │   └── BehaviourFindReplaceWindow.cs
    │   │   ├── ConstraintTools/
    │   │   │   ├── ConstraintBezierWindow.UI.cs
    │   │   │   ├── ConstraintBezierWindow.cs
    │   │   │   ├── ConstraintToolWindow.cs
    │   │   │   ├── ConstraintTrainWindow.UI.cs
    │   │   │   ├── ConstraintTrainWindow.cs
    │   │   │   ├── InsertablePropWindow.UI.cs
    │   │   │   └── InsertablePropWindow.cs
    │   │   ├── MenuTool/
    │   │   │   ├── MenuToolAddMenuDialog.cs
    │   │   │   ├── MenuToolTrees.cs
    │   │   │   └── MenuToolWindow.cs
    │   │   └── RiryToolsVRChatMenus.cs
    │   ├── AssemblyInfo.cs
    │   └── Riry.RiryTools.Editor.VRChat.asmdef
    ├── AssemblyInfo.cs
    └── Riry.RiryTools.Editor.asmdef
```
