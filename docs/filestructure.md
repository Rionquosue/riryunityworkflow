# File structure

## Repository

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.instructions~/`: Working instructions for LLMs while maintaining this repo. Start with `for_agents.md`.
<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
- `.github/workflows/notify-vpm.yml`: starts the VPM build of `com.riry.rirytools` in `rirytools_vpm` on pushes to `main` and `beta` that change `com.riry.rirytools/`.
- `.github/workflows/notify-vpm-ftemulator.yml`: the same for `com.riry.ftemulator`, on pushes that change `com.riry.ftemulator/`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `About/` (`Tools.md`, `CHANGELOG.md`), `tutorials/`.
=======
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
=======
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`, for both packages.
<<<<<<< HEAD
>>>>>>> 4e9dc36 (FTEmulator moved into its own package, com.riry.ftemulator, for the VPM listing)
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `ui-languagespec.md` (where each UI element's text lives), `About/` (`Tools.md`, `CHANGELOG.md`), `tutorials/`.
>>>>>>> 5490d61 (Add UI language spec listing where each UI element's text lives)
=======
=======
- `.github/workflows/notify-vpm.yml`: starts the VPM build of `com.riry.rirytools` in `rirytools_vpm` on pushes to `main` and `beta` that change `com.riry.rirytools/`.
- `.github/workflows/notify-vpm-ftemulator.yml`: the same for `com.riry.ftemulator`, on pushes that change `com.riry.ftemulator/`.
>>>>>>> 2c2a932 (Notify VPM: FTEmulator gets its own workflow; each package's build starts only when its folder changed)
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `ui-languagespec.md` (where each UI element's text lives), `About/` (`Tools.md`, `CHANGELOG.md`, and a page per tool in `About/Tools/`), `tutorials/` (a page per tool, and a folder per tool with a page per subtool, such as `tutorials/constrainttool/` with `train-rig.md`, `bezier-rig.md`, `Insertable-prop.md`, `dual-axis-detector.md` and `three-axis-detector.md`).
>>>>>>> 9d9b681 (Docs: Dual axis detector and Three axis detector tutorials, docs layout in the file structure)
- `com.riry.rirytools/`: Is the extension package. Nothing from it is copied into the user's project.
- `com.riry.ftemulator/`: FTEmulator, its own package (listed in the VPM as `com.riry.ftemulator`). It needs `com.riry.rirytools` and uses its internals (`InternalsVisibleTo`). Only Unity Editor code lives here; the VRCFaceTracking companion (C# module; Python Recorder app and command-line app) is in `Rionquosue/VRCFT-FtEmuCompanion`.

## Rules

1. **Assemblies.** Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it: `Editor/` (Unity only), `Editor/VRChat/` (VRChat SDK) and `Editor/VRChat/MenuTool/GestureManager/` (Gesture Manager). FTEmulator's package has two more: `com.riry.ftemulator/Editor/` (`Riry.RiryTools.Editor.FTEmulator`, VRChat SDK) and `com.riry.ftemulator/Editor/FTEmulator/GestureManager/` (`Riry.RiryTools.Editor.FTEmulator.GestureManager`, Gesture Manager). Code that relies on a dependency goes in that dependency's folder. A tool that needs a dependency for any part of it keeps all of its code in that dependency's folder (Constraint Tools lives entirely in `Editor/VRChat/`).
2. **Logic folders.** A tool's logic is in its own folder, named after the tool (`AutoSync/`, `AvatarMigrator/`, `BlendshapeSync/`, `GuidTools/`, `IconCap/`, `ImportPresets/`, `MenuTool/`, `ConstraintTools/`, `FTEmulator/`). A tool's data files are in folders inside it (`FTEmulator/HardwareProfiles/`, `AvatarProfiles/`, `MappingSchemas/`).
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
| Controller Cleaner | `AnimatorTools/` | `ControllerCleaner` |
| Constraint Tools (Train, Bezier, Insertable prop, Dual and Three axis detector) | `VRChat/ConstraintTools/` | `ConstraintTool`, `ConstraintTrain`, `ConstraintBezier`, `InsertableProp`, `TiltDetector` |
| Find and Replace | `VRChat/AnimatorTools/` | `BehaviourFindReplace` |
| Find Null references | `Debug/` | `FindNullReferences` |
| FTEmulator | `com.riry.ftemulator/Editor/FTEmulator/` | `FTEmulator` |
| GUID Tools | `GuidTools/` | `GuidTool` |
| IconCap | `IconCap/` | `IconCap` |
| Import Presets | `ImportPresets/` | `ImportPreset` |
| Material Cleaner | `Utilities/` | `MaterialCleaner` |
| Menu Mockup | `Debug/` | `MenuMockup` |
| Menu Tools | `VRChat/MenuTool/` | `MenuTool` |
| Refresh Scripts | `Utilities/` | `RefreshScripts` |
| Script Load Guard | `Debug/` | `ScriptLoadGuard` |
| Stop all tool scripts | `Components/` | `StopAllToolScripts` |
| Stop Auto Game | `Utilities/` | `StopAutoGame` |

## Entry points

Paths are relative to `com.riry.rirytools/Editor/`.

- **Main Menu:** `UI/RiryToolsMainMenu.cs`. Hosts each tool's window in a tab. VRChat tools are handed to it by `VRChat/UI/RiryToolsVRChatMenus.cs`.
- **Tools > RiryTools menu:** `UI/RiryToolsMenuBar.cs`; the VRChat entries are in `VRChat/UI/RiryToolsVRChatMenus.cs`.
- **Right click menus (Hierarchy, Project):** `UI/RiryToolsRightClickMenu.cs`.
- **Shared code:** `Components/` (logging, paths, project folder, fuzzy search, type lookup, hierarchy paths, scene object ids, blendshape names, referenced assets, Controller Cleaner's scan and clean (also used by GUID Tools), Git runner and state, missing-SDK and missing-Git exceptions), `UI/Components/` (`RiryToolGUI`: shared drawing and Undo for window settings; `RiryToolUndoWindow`: the base of windows that refresh after Undo and Redo; `IRiryHostedTool`: tools hosted in a Main Menu tab), `VRChat/Components/` (Constraint Tools' chains, sources, generated objects and logged status, shared by Train, Bezier and Insertable prop).
- **A tool:** its logic folder and its `UI/<folder>/` window, per the table above.
- **FTEmulator:** `com.riry.ftemulator/Editor/FTEmulator/FTEmulator.cs` (lifecycle, one session), `FTEmulatorSession.cs` (the per-frame pipeline); its Gesture Manager output is `com.riry.ftemulator/Editor/FTEmulator/GestureManager/FTEmulatorGestureManager.cs`. Its data is JSON, found by folder GUID (`FTEmulatorCatalog`). Its windows are in `com.riry.ftemulator/Editor/UI/FTEmulator/`, its menu entry and Main Menu hosting in `com.riry.ftemulator/Editor/UI/FTEmulatorMenus.cs`.

## Script locations

```text
com.riry.rirytools/
└── Editor/
    ├── AnimatorTools/
    │   ├── CleanAnimationKeys.cs
    │   └── ControllerCleanerSources.cs
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
    │   ├── AvatarMigratorStep.cs
    │   ├── AvatarMigratorYaml.cs
    │   └── types.yml
    ├── BlendshapeSync/
    │   ├── BlendshapeSyncJob.cs
    │   ├── BlendshapeSyncJobs.cs
    │   ├── BlendshapeSyncWindow.Checks.cs
    │   ├── BlendshapeSyncWindow.Plan.cs
    │   └── BlendshapeSyncWindow.Sync.cs
    ├── Components/
    │   ├── BlendshapeNames.cs
    │   ├── ControllerCleanerClean.cs
    │   ├── ControllerCleanerScan.cs
    │   ├── FuzzyMatch.cs
    │   ├── GestureManagerNotFoundException.cs
    │   ├── GitNotInstalledException.cs
    │   ├── GitRunner.cs
    │   ├── GitState.cs
    │   ├── HierarchyPath.cs
    │   ├── ReferencedAssets.cs
    │   ├── RiryLog.cs
    │   ├── RiryToolsPaths.cs
    │   ├── RiryToolsProjectFolder.cs
    │   ├── RiryToolsTypes.cs
    │   ├── SceneObjectId.cs
    │   ├── StopAllToolScripts.cs
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
    │   ├── GuidToolIgnoredReferences.cs
    │   ├── GuidToolInactiveReferences.cs
    │   ├── GuidToolReferenceLocation.cs
    │   ├── GuidToolPrefabStageRepair.cs
    │   ├── GuidToolUnusedOverrides.cs
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
    │   ├── ImportPresetDropChoices.cs
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
    │   │   ├── CleanAnimationKeysWindow.cs
    │   │   └── ControllerCleanerWindow.cs
    │   ├── AutoSync/
    │   │   ├── AutoSyncConflictNotice.cs
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
    │   │   ├── RiryToolGUI.cs
    │   │   └── RiryToolUndoWindow.cs
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
    │   │   ├── GuidToolPrefabCleanerWindow.cs
    │   │   ├── GuidToolRegenerate.Prompts.cs
    │   │   ├── GuidToolTargetsWindow.cs
    │   │   └── GuidToolsWindow.cs
    │   ├── IconCap/
    │   │   ├── IconCapEffectStackGUI.cs
    │   │   ├── IconCapNamePrompt.cs
    │   │   ├── IconCapWindow.cs
    │   │   └── IconCapWindow.UI.cs
    │   ├── ImportPresets/
    │   │   ├── ImportPresetDragOverlay.cs
    │   │   ├── ImportPresetExtractAnimationWindow.cs
    │   │   ├── ImportPresetFolderWindow.cs
    │   │   ├── ImportPresetFolderWindow.UI.cs
    │   │   ├── ImportPresetQuickWindow.cs
    │   │   ├── ImportPresetSaveWindow.cs
    │   │   └── ImportPresetSaveWindow.UI.cs
    │   ├── Utilities/
    │   │   ├── AttachToWindow.cs
    │   │   └── MaterialCleanerWindow.cs
    │   ├── RiryToolsMainMenu.cs
    │   ├── RiryToolsMenuBar.cs
    │   └── RiryToolsRightClickMenu.cs
    ├── Utilities/
    │   ├── AttachTo.cs
    │   ├── AttachToAvatarTracker.cs
    │   ├── AttachToBones.cs
    │   ├── MaterialCleaner.cs
    │   ├── MaterialCleanerSources.cs
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
    │   │   ├── ConstraintToolSources.cs
    │   │   └── ConstraintToolStatus.cs
    │   ├── ConstraintTools/
    │   │   ├── ConstraintBezier/
    │   │   │   └── ConstraintBezierCurve.cs
    │   │   ├── ConstraintTrain/
    │   │   │   ├── ConstraintTrainChains.cs
    │   │   │   ├── ConstraintTrainPlanner.cs
    │   │   │   └── ConstraintTrainPrefabs.cs
    │   │   ├── InsertableProp/
    │   │   │   ├── InsertablePropAnimator.cs
    │   │   │   ├── InsertablePropMath.cs
    │   │   │   └── InsertablePropRig.cs
    │   │   └── TiltDetector/
    │   │       └── TiltDetectorBuilder.cs
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
    │   │   ├── MenuToolIconPreset.cs
    │   │   ├── MenuToolModel.cs
    │   │   ├── MenuToolPlan.cs
    │   │   ├── MenuToolPlanner.cs
    │   │   ├── MenuToolPreviewMenus.cs
    │   │   ├── MenuToolRichText.cs
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
    │   │   │   ├── InsertablePropWindow.UI.cs
    │   │   │   └── TiltDetectorWindow.cs
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

```text
com.riry.ftemulator/
└── Editor/
    ├── FTEmulator/
    │   ├── AvatarProfiles/
    │   │   ├── adjerry-legacy.json
    │   │   ├── pawlygon-legacy.json
    │   │   ├── pawlygon-native.json
    │   │   ├── vrcft-unified-v2.json
    │   │   └── vrchat-native.json
    │   ├── GestureManager/
    │   │   ├── FTEmulatorGestureManager.cs
    │   │   └── Riry.RiryTools.Editor.FTEmulator.GestureManager.asmdef
    │   ├── HardwareProfiles/
    │   │   ├── babble-lower-v2.1.2.json
    │   │   ├── quest-pro-base-fb1.json
    │   │   ├── quest-pro-official-fb2.json
    │   │   ├── unrestricted.json
    │   │   ├── vive-facial-tracker.json
    │   │   ├── vive-pro-eye-plus-facial.json
    │   │   ├── vive-pro-eye.json
    │   │   └── vive-pro-stock.json
    │   ├── MappingSchemas/
    │   │   └── unified-expressions.json
    │   ├── FTEmulator.cs
    │   ├── FTEmulatorAvatarScanner.cs
    │   ├── FTEmulatorBlendshapeWriter.cs
    │   ├── FTEmulatorCatalog.cs
    │   ├── FTEmulatorDiagnostics.cs
    │   ├── FTEmulatorDnsPacket.cs
    │   ├── FTEmulatorExpression.cs
    │   ├── FTEmulatorHardwareFilter.cs
    │   ├── FTEmulatorJson.cs
    │   ├── FTEmulatorLegacyMapper.cs
    │   ├── FTEmulatorNativeDescriptor.cs
    │   ├── FTEmulatorNativeEvaluator.cs
    │   ├── FTEmulatorNativeSetWeights.cs
    │   ├── FTEmulatorOscCodec.cs
    │   ├── FTEmulatorOscQuery.cs
    │   ├── FTEmulatorOscReceiver.cs
    │   ├── FTEmulatorParameterStore.cs
    │   ├── FTEmulatorRecording.cs
    │   ├── FTEmulatorSession.cs
    │   ├── FTEmulatorSettings.cs
    │   ├── FTEmulatorTemplateResolver.cs
    │   └── FTEmulatorTypes.cs
    ├── UI/
    │   ├── FTEmulator/
    │   │   ├── FTEmulatorDebugWindow.cs
    │   │   └── FTEmulatorWindow.cs
    │   └── FTEmulatorMenus.cs
    ├── AssemblyInfo.cs
    └── Riry.RiryTools.Editor.FTEmulator.asmdef
```
