# File structure

## Existing structure

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.claude~/`, `.instructions~/`: instructions for LLMs.
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `design/` and `tutorials/` (one per tool).
- `com.riry.rirytools/`: the package. `README.md`, `CHANGELOG.md`; `ProjectFolder~/` is copied into the user's `Assets/RiryTools`.

Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it. A tool's logic is in its own folder (or one file when small); every file that draws UI is in a `UI/` folder.

Scheme: code that relies on a dependency goes in a folder for that dependency (its own assembly), holding a `UI/` folder, one folder per tool, and a `Components/` folder only for shared code that needs that dependency's assembly.

- `Editor/` (Unity only):
  - `Components/`: code shared by several tools: `RiryLog`, `RiryToolsPaths`, `RiryToolsProjectFolder`, `FuzzyMatch`, `VrcSdkNotFoundException`, `RiryTypes` (type by name), `HierarchyPath` (transform path).
  - `AssemblyInfo` opens internals to the VRChat and Gesture Manager assemblies.
  - Single files: `AttachTo`, `BlendshapeSyncJob(s)`, `CleanAnimationKeys`, `FindNullReferences`, `MenuMockup`, `StopAutoGame`.
  - Folders: `AutoSync/` (Git sync), `AvatarMigrationAssistant/` (one file per step, copy types, `types.yml`), `GuidTools/`, `IconCap/` (with `Shaders/`), `ImportPresets/`; `ConstraintBezier/`, `ConstraintTrain/`, `InsertableProp/` hold Constraint Tool math, here because it needs no SDK.
- `Editor/UI/`: every core window (Avatar Migration Assistant and its dialogs, Blendshape Sync, Broken references, Find Null references, Git, Import Presets and others), `RiryMenus.cs` (all core menu entries and the "VRCSDK not found" stand-ins), `RiryToolsMainMenu.cs` (Main Menu; `Tabs` sets tab order), `RiryGui.cs`.
- `Editor/VRChat/` (needs the VRChat SDK):
  - `UI/`: `RiryVRChatMenus.cs` (VRChat menu entries and Main Menu hooks) and the VRChat tool windows: Constraint Tool, Train, Bezier, Insertable prop, Menu Tool (window, trees, Add dialog), Find and Replace.
  - `AnimatorTools/`: Find and Replace logic.
  - `ConstraintTool/`: Insertable prop rig and animator.
  - `MenuTool/`: scanner, model, budget, plan, apply, preview, entry state.
- `Editor/VRChat/MenuTool/GestureManager/` (needs Gesture Manager): the radial preview and Play Mode entry state replay, separate so the Menu Tool works without Gesture Manager.

## Script locations

```text
com.riry.rirytools/
└── Editor/
    ├── AnimatorTools/
    │   └── CleanAnimationKeys.cs
    ├── AutoSync/
    │   ├── AutoSync.cs
    │   ├── AutoSyncGitActions.cs
    │   ├── AutoSyncGitRemotes.cs
    │   ├── AutoSyncGitRunner.cs
    │   ├── AutoSyncGitSetup.cs
    │   └── AutoSyncJob.cs
    ├── AvatarMigrationAssistant/
    │   ├── AvatarMigratorCopyPrefabs.cs
    │   ├── AvatarMigratorCopyRun.cs
    │   ├── AvatarMigratorCopyTypes.cs
    │   ├── AvatarMigratorCorrectRotation.cs
    │   ├── AvatarMigratorCorrectScale.cs
    │   ├── AvatarMigratorIgnoreList.cs
    │   ├── AvatarMigratorMatchObjects.cs
    │   ├── AvatarMigratorMigratedFolder.cs
    │   ├── AvatarMigratorPreFlightAnswers.cs
    │   ├── AvatarMigratorRepackPrefabs.cs
    │   ├── AvatarMigratorRepointReferences.cs
    │   ├── AvatarMigratorRetargetAnim.cs
    │   ├── AvatarMigratorRunReport.cs
    │   ├── AvatarMigratorSettings.cs
    │   ├── AvatarMigratorTypeRules.cs
    │   ├── AvatarMigratorUpgradeConstraints.cs
    │   └── AvatarMigratorVRCFuryMover.cs
    ├── Components/
    │   ├── FuzzyMatch.cs
    │   ├── GestureManagerNotFoundException.cs
    │   ├── HierarchyPath.cs
    │   ├── RiryLog.cs
    │   ├── RiryToolsPaths.cs
    │   ├── RiryToolsProjectFolder.cs
    │   ├── RiryToolsTypes.cs
    │   └── VrcSdkNotFoundException.cs
    ├── Debug/
    │   ├── FindNullReferences.cs
    │   └── ScriptLoadGuard.cs
    ├── GuidTools/
    │   ├── GuidToolBrokenReferenceMonitor.cs
    │   ├── GuidToolReferenceCache.cs
    │   ├── GuidToolReferenceRepair.cs
    │   ├── GuidToolRegenerate.cs
    │   └── GuidToolRemapLedger.cs
    ├── IconCap/
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
    │   ├── Components/
    │   │   ├── ScriptLoadFailureWindow.cs
    │   │   └── SdkNotice.cs
    │   ├── AutoSyncGitConfigureWindow.cs
    │   ├── AutoSyncGitWindow.cs
    │   ├── AvatarAttachWindow.cs
    │   ├── AvatarMigrationAssistant.cs
    │   ├── AvatarMigrationPreFlightChecks.cs
    │   ├── AvatarRunReportWindow.cs
    │   ├── BlendshapeSyncJobWindow.cs
    │   ├── BlendshapeSyncWindow.UI.cs
    │   ├── BlendshapeSyncWindow.cs
    │   ├── BrokenReferenceWindow.UI.cs
    │   ├── BrokenReferenceWindow.cs
    │   ├── CleanAnimationKeysWindow.cs
    │   ├── ExtractAnimationWindow.cs
    │   ├── FindNullReferencesWindow.cs
    │   ├── GuidToolsWindow.cs
    │   ├── IconCapEffectStackGUI.cs
    │   ├── IconCapWindow.cs
    │   ├── ImportPresetQuickWindow.cs
    │   ├── ImportPresetWindowActions.cs
    │   ├── ImportPresetWindows.cs
    │   ├── ManualMatching.cs
    │   ├── MenuMockupWindows.cs
    │   ├── RiryToolGUI.cs
    │   ├── RiryToolsMainMenu.cs
    │   ├── RiryToolsMenuBar.cs
    │   └── RiryToolsRightClickMenu.cs
    ├── VRChat/
    │   ├── AnimatorTools/
    │   │   └── BehaviourFindReplace.cs
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
    │   │   ├── BehaviourFindReplaceWindow.cs
    │   │   ├── ConstraintBezierWindow.UI.cs
    │   │   ├── ConstraintBezierWindow.cs
    │   │   ├── ConstraintToolWindow.cs
    │   │   ├── ConstraintTrainWindow.UI.cs
    │   │   ├── ConstraintTrainWindow.cs
    │   │   ├── InsertablePropWindow.UI.cs
    │   │   ├── InsertablePropWindow.cs
    │   │   ├── MenuToolAddMenuDialog.cs
    │   │   ├── MenuToolTrees.cs
    │   │   ├── MenuToolWindow.cs
    │   │   └── RiryToolsVRChatMenus.cs
    │   ├── AssemblyInfo.cs
    │   └── Riry.RiryTools.Editor.VRChat.asmdef
    ├── AssemblyInfo.cs
    ├── AttachTo.cs
    ├── BlendshapeSyncJob.cs
    ├── BlendshapeSyncJobs.cs
    ├── MenuMockup.cs
    ├── Riry.RiryTools.Editor.asmdef
    └── StopAutoGame.cs
```
