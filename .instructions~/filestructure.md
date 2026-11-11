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
    ├── AutoSync/
    │   ├── AutoSync.cs
    │   ├── AutoSyncJob.cs
    │   ├── GitActions.cs > AutoSyncGitActions.cs
    │   ├── GitRemotes.cs > AutoSyncGitRemotes.cs
    │   ├── GitRunner.cs > AutoSyncGitRunner.cs
    │   ├── GitSetup.cs > AutoSyncGitSetup.cs
    │   └── ScriptLoadGuard.cs > Move this to Debug, change codebase so that this is called much like "StopAutoGame"
    ├── AvatarMigrationAssistant/
    │   ├── CopyPrefabs.cs > AvatarMigratorCopyPrefabs.cs
    │   ├── CopyRun.cs > AvatarMigratorCopyRun.cs
    │   ├── CopyTypes.cs > AvatarMigratorCopyTypes.cs
    │   ├── CorrectRotation.cs > AvatarMigratorCorrectRotation.cs
    │   ├── CorrectScale.cs > AvatarMigratorCorrectScale.cs
    │   ├── IgnoreList.cs > AvatarMigratorIgnoreList.cs
    │   ├── MatchObjects.cs > AvatarMigratorMatchObjects.cs
    │   ├── MigratedFolder.cs > AvatarMigratorMigratedFolder.cs
    │   ├── RepackPrefabs.cs > AvatarMigratorRepackPrefabs.cs
    │   ├── RepointReferences.cs > AvatarMigratorRepointReferences.cs
    │   ├── RetargetAnimations.cs > AvatarMigratorRetargetAnim.cs
    │   ├── RunReport.cs > AvatarMigratorRunReport.cs
    │   ├── Settings.cs > AvatarMigratorSettings.cs
    │   ├── TypeRules.cs > AvatarMigratorTypeRules.cs
    │   ├── UpFrontAnswers.cs > AvatarMigratorPreFlightAnswers.cs
    │   ├── UpgradeConstraints.cs > AvatarMigratorUpgradeConstraints.cs
    │   └── VrcFuryPrefabs.cs > AvatarMigratorVRCFuryMover.cs
    ├── Components/
    │   ├── FuzzyMatch.cs
    │   ├── HierarchyPath.cs
    │   ├── RiryLog.cs
    │   ├── RiryToolsPaths.cs
    │   ├── RiryToolsProjectFolder.cs
    │   ├── RiryTypes.cs > RiryToolsTypes.cs
    │   └── VrcSdkNotFoundException.cs > (Add a GestureManagerNotFOundException.cs for when tools call Gesture Manager and it isn't located. Move relevant code to this file.)
    ├── GuidTools/
    │   ├── BrokenReferenceMonitor.cs > Prefix with "GuidTool"
    │   ├── GuidReferenceCache.cs > Prefix with "GuidTool",  do not repeat Guid in the name.
    │   ├── GuidReferenceRepair.cs > Prefix with "GuidTool",  do not repeat Guid in the name.
    │   ├── GuidRemapLedger.cs > Prefix with "GuidTool",  do not repeat Guid in the name.
    │   └── RegenerateGuid.cs > Prefix with "GuidTool"
    ├── IconCap/
    │   ├── IconCapData.cs
    │   ├── IconCapEffects.cs
    │   ├── IconCapFiles.cs
    │   ├── IconCapRenderer.cs
    │   └── IconGenerator.cs
    ├── ImportPresets/
    │   ├── AnimationExtractSettings.cs > ImportPresetAnimationExtraction.cs
    │   ├── BetterFbxExporter.cs > ImportPresetBetterFBX.cs
    │   ├── ImportPresetActions.cs
    │   ├── ImportPresetAnimations.cs
    │   ├── ImportPresetFolderRules.cs
    │   ├── ImportPresetLibrary.cs
    │   ├── ImportPresetMarker.cs
    │   ├── ImportPresetOwnedFiles.cs
    │   ├── ImportPresetPostprocessor.cs
    │   └── ImportPresetPreferences.cs
    ├── UI/
    │   ├── AttachToWindow.cs > AvatarAttachWindow.cs
    │   ├── AvatarMigrationAssistant.cs
    │   ├── AvatarRunReportWindow.cs
    │   ├── BlendshapeSyncJobWindow.cs
    │   ├── BlendshapeSyncWindow.UI.cs
    │   ├── BlendshapeSyncWindow.cs
    │   ├── BrokenReferenceWindow.UI.cs
    │   ├── BrokenReferenceWindow.cs
    │   ├── CleanAnimationKeysWindow.cs
    │   ├── ExtractAnimationWindow.cs
    │   ├── FindNullReferencesWindow.cs
    │   ├── GitConfigureWindow.cs > AutoSyncGitConfigureWindow.cs
    │   ├── GitWindows.cs > AutoSyncGitWindow.cs
    │   ├── GuidToolsWindow.cs
    │   ├── IconCapEffectStackGUI.cs
    │   ├── IconCapWindow.cs
    │   ├── ImportPresetQuickWindow.cs
    │   ├── ImportPresetWindowActions.cs
    │   ├── ImportPresetWindows.cs
    │   ├── ManualMatching.cs
    │   ├── MenuMockupWindows.cs
    │   ├── RiryGui.cs > RiryToolGUI.cs
    │   ├── RiryMenus.cs > ( Expand this into RiryToolsRightClickMenu.cs & RiryToolsMenuBar.cs )
    │   ├── RiryToolsMainMenu.cs
    │   ├── ScriptLoadFailureWindow.cs > Move to /components subfolder of UI folder.
    │   ├── SdkNotice.cs > Move to /components subfolder of UI folder.
    │   └── UpFrontCheck.cs > Rename to AvatarMigrationPreFlightChecks.cs
    ├── VRChat/
    │   ├── AnimatorTools/
    │   │   └── BehaviourFindReplace.cs
    |   ├── ConstraintTools > (Add this folder, move all constraint tools into it. Future constraint tools should go in here. They are all dependent on the VRChat SDK and should all be moved to a single folder.)
    |   |   ├── ConstraintBezier/
    |   |   │   └── ConstraintBezierCurve.cs
    |   |   ├── ConstraintTrain/
    |   |   │   ├── ConstraintTrainChains.cs
    |   |   │   ├── ConstraintTrainPlanner.cs
    |   |   │   └── ConstraintTrainPrefabs.cs
    |   |   └── InsertableProp/
    |   |       ├── InsertablePropMath.cs 
    |   |       ├── InsertablePropAnimator.cs
    │   |       └── InsertablePropRig.cs
    │   │   
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
    │   │   └── RiryVRChatMenus.cs > RiryToolsVRCHatMenus.cs
    │   ├── AssemblyInfo.cs
    │   └── Riry.RiryTools.Editor.VRChat.asmdef
    ├── AssemblyInfo.cs
    ├── AttachTo.cs
    ├── BlendshapeSyncJob.cs
    ├── BlendshapeSyncJobs.cs
    ├── CleanAnimationKeys.cs > 
    ├── FindNullReferences.cs > Debug folder
    ├── MenuMockup.cs
    ├── Riry.RiryTools.Editor.asmdef
    └── StopAutoGame.cs
```
