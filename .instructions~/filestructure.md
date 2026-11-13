# File structure

## Repository

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.instructions~/`: Working instructions for LLMs while maintaining this repo. Start with `for_agents.md`.
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `About/` (`Tools.md`, `CHANGELOG.md`), `tutorials/`.
- `com.riry.rirytools/`: Is the extension package. Nothing from it is copied into the user's project.

## Rules

1. **Assemblies.** Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it: `Editor/` (Unity only), `Editor/VRChat/` (VRChat SDK) and `Editor/VRChat/MenuTool/GestureManager/` (Gesture Manager). Code that relies on a dependency goes in that dependency's folder. A tool that needs a dependency for any part of it keeps all of its code in that dependency's folder (Constraint Tool lives entirely in `Editor/VRChat/`).
2. **Logic folders.** A tool's logic is in its own folder, named after the tool (`AutoSync/`, `AvatarMigrator/`, `BlendshapeSync/`, `GuidTools/`, `IconCap/`, `ImportPresets/`, `MenuTool/`, `ConstraintTools/`).
3. **Category folders.** A small tool (a single script with a single job) does not get its own folder. It goes in a category folder: `Debug/` (debugging aids), `Utilities/` (general helpers) or `AnimatorTools/`. A tool the owner places in a category keeps all its files there, even when it has several (Menu Mockup in `Debug/`, Attach To in `Utilities/`).
4. **UI and feature logic.** Give UI and feature logic separate scripts and types. Put windows, panels, controls, dialogs, and progress displays in the owning assembly’s `UI/<ToolOrCategory>/` folder. Keep Main Menu and menu registration scripts directly under `UI/`, and put reusable controls in their own scripts under `UI/Components/`. UI scripts gather input, call feature operations, and present their results. Put feature state, operation validation, planning, execution, and persistence in the tool’s logic folder. Feature types must run without opening or depending on a UI type. Use inputs, results, and progress or cancellation callbacks where needed. A partial type follows the same rule across all its files.
5. **Components.** Code reused by several tools, or by several subtools of one tool, goes in that assembly's `Components/` folder. Shared drawing code goes in `UI/Components/`.
6. **One job per script.** Each script does one job, so no script becomes too large to maintain. Example: a tool that enumerates the avatar hierarchy into a searchable index, then applies changes based on a search of that index, has one script for the index and search, and another for applying the changes.
7. **Names.** File names match the main type they hold. A class split over several files uses `Type.Role.cs` (`BlendshapeSyncWindow.UI.cs`). Every type and file carries its tool's code prefix (below). Exception: a type Unity stores by name in users' assets keeps its name (`IconGenerator`, a ScriptableObject saved as `.asset` files). Reserve `Window`, `Panel`, `Dialog`, `View`, and other UI names for types and files that implement UI. Name feature types for their job. For example, `BlendshapeSyncJob` runs the sync and `BlendshapeSyncWindow` displays and edits it.
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
- **Shared code:** `Components/` (logging, paths, project folder, fuzzy search, type lookup, hierarchy paths, Git runner and state, missing-SDK and missing-Git exceptions), `UI/Components/` (`RiryToolGUI`: shared drawing and Undo for window settings; `IRiryHostedTool`: tools hosted in a Main Menu tab), `VRChat/Components/` (Constraint Tool's chains, sources and generated objects, shared by Train, Bezier and Insertable prop).
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
    │   ├── MenuMockupFile.cs
    │   ├── MenuMockupModel.cs
    │   ├── MenuMockupTypes.cs
    │   ├── ScriptLoadGuard.cs
    │   └── ScriptLoadGuardSaveBlocker.cs
    ├── GuidTools/
    │   ├── GuidToolBrokenReferenceMonitor.cs
    │   ├── GuidToolBrokenReferenceSaveGuard.cs
    │   ├── GuidToolBytes.cs
    │   ├── GuidToolLog.cs
    │   ├── GuidToolMetaHistoryParser.cs
    │   ├── GuidToolReferenceCache.cs
    │   ├── GuidToolReferenceRepair.cs
    │   ├── GuidToolRegenerate.cs
    │   ├── GuidToolRegenerate.Patch.cs
    │   ├── GuidToolRegenerate.Rollback.cs
    │   ├── GuidToolRegenerate.Scan.cs
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
    │   │   ├── CleanAnimationKeysButton.cs
    │   │   ├── CleanAnimationKeysWhitelistWindow.cs
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
    │   │   ├── IRiryHostedTool.cs
    │   │   └── RiryToolGUI.cs
    │   ├── Debug/
    │   │   ├── FindNullReferencesWindow.cs
    │   │   ├── MenuMockupComponentsWindow.cs
    │   │   ├── MenuMockupEditWindow.cs
    │   │   ├── MenuMockupNameWindow.cs
    │   │   ├── MenuMockupPreview.cs
    │   │   ├── MenuMockupTextWindow.cs
    │   │   └── ScriptLoadGuardFailureWindow.cs
    │   ├── GuidTools/
    │   │   ├── GuidToolBrokenReferenceWindow.cs
    │   │   ├── GuidToolBrokenReferenceWindow.UI.cs
    │   │   ├── GuidToolBrokenSaveWindow.cs
    │   │   ├── GuidToolClearReferenceCache.cs
    │   │   ├── GuidToolRegenerate.Prompts.cs
    │   │   └── GuidToolsWindow.cs
    │   ├── IconCap/
    │   │   ├── IconCapEffectStackGUI.cs
    │   │   ├── IconCapNamePrompt.cs
    │   │   ├── IconCapWindow.cs
    │   │   └── IconCapWindow.UI.cs
    │   ├── ImportPresets/
    │   │   ├── ImportPresetExtractAnimationWindow.cs
    │   │   ├── ImportPresetFolderWindow.cs
    │   │   ├── ImportPresetFolderWindow.UI.cs
    │   │   ├── ImportPresetQuickWindow.cs
    │   │   ├── ImportPresetSaveWindow.cs
    │   │   └── ImportPresetSaveWindow.UI.cs
    │   ├── Utilities/
    │   │   └── AttachToWindow.cs
    │   ├── RiryToolsMainMenu.cs
    │   ├── RiryToolsMenuBar.cs
    │   └── RiryToolsRightClickMenu.cs
    ├── Utilities/
    │   ├── AttachTo.cs
    │   ├── AttachToAvatarTracker.cs
    │   ├── AttachToBones.cs
    │   ├── RefreshScripts.cs
    │   └── StopAutoGame.cs
    ├── VRChat/
    │   ├── AnimatorTools/
    │   │   ├── BehaviourFindReplace.cs
    │   │   ├── BehaviourFindReplaceApply.cs
    │   │   ├── BehaviourFindReplaceChildPrefab.cs
    │   │   └── BehaviourFindReplaceUsers.cs
    │   ├── Components/
    │   │   ├── ConstraintToolChains.cs
    │   │   ├── ConstraintToolObjects.cs
    │   │   └── ConstraintToolSources.cs
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
    │   │   │   ├── GestureManagerMenuPath.cs
    │   │   │   ├── GestureManagerRadial.cs
    │   │   │   └── Riry.RiryTools.Editor.GestureManager.asmdef
    │   │   ├── IMenuToolRadial.cs
    │   │   ├── MenuToolApply.cs
    │   │   ├── MenuToolAvatarKeys.cs
    │   │   ├── MenuToolBudget.cs
    │   │   ├── MenuToolCompressor.cs
    │   │   ├── MenuToolEntryState.cs
    │   │   ├── MenuToolModel.cs
    │   │   ├── MenuToolPlan.cs
    │   │   ├── MenuToolPlanner.cs
    │   │   ├── MenuToolPreviewMenus.cs
    │   │   ├── MenuToolScanner.cs
    │   │   ├── MenuToolScanner.ModularAvatar.cs
    │   │   ├── MenuToolScanner.Native.cs
    │   │   ├── MenuToolScanner.VRCFury.cs
    │   │   └── MenuToolVrcfuryMenu.cs
    │   ├── UI/
    │   │   ├── AnimatorTools/
    │   │   │   └── BehaviourFindReplaceWindow.cs
    │   │   ├── ConstraintTools/
    │   │   │   ├── ConstraintBezierWindow.cs
    │   │   │   ├── ConstraintBezierWindow.UI.cs
    │   │   │   ├── ConstraintToolWindow.cs
    │   │   │   ├── ConstraintTrainWindow.cs
    │   │   │   ├── ConstraintTrainWindow.UI.cs
    │   │   │   ├── InsertablePropWindow.cs
    │   │   │   └── InsertablePropWindow.UI.cs
    │   │   ├── MenuTool/
    │   │   │   ├── MenuToolAddMenuDialog.cs
    │   │   │   ├── MenuToolBudgetTree.cs
    │   │   │   ├── MenuToolIcons.cs
    │   │   │   ├── MenuToolManagerTree.cs
    │   │   │   └── MenuToolWindow.cs
    │   │   └── RiryToolsVRChatMenus.cs
    │   ├── AssemblyInfo.cs
    │   └── Riry.RiryTools.Editor.VRChat.asmdef
    ├── AssemblyInfo.cs
    └── Riry.RiryTools.Editor.asmdef
```
