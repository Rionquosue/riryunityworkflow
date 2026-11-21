# UI language spec

Where the text of every UI element is written, for translating the UI. Source
paths are relative to `com.riry.rirytools/Editor/`, except those starting with
`com.riry.ftemulator/` (FTEmulator's own package). Each file lists only the
lines that write UI text, grouped by method, with the kind of text and its line
numbers on `betastaging`. Line numbers drift as files change; method names
don't.

## Kinds of text

1. **Menu entries:** `[MenuItem]` paths, and the `const string` menu paths at
   the top of `UI/RiryToolsMenuBar.cs` and `UI/RiryToolsRightClickMenu.cs`.
   Unity reads them when scripts compile, so a language change needs a script
   reload.
2. **Window titles, tabs and toolbars, labels, field labels, buttons, toggles,
   foldouts, help boxes, tooltips, context menu items, dialogs, progress bars,
   notifications:** text written in the UI call itself.
3. **Text shown through a variable:** text written here and handed to a UI call
   elsewhere: `Title` and `DisplayName` constants, status and error messages,
   validation messages, Menu Tools scan notes and plan errors, and exception
   messages a window shows.

Not listed: UI calls whose text is only a variable, a symbol (`x`, `▾`, `≡`,
`›`) or runtime data (names, paths, values); `RiryLog` and `Debug.Log` messages;
settings keys, file contents and other strings the user never sees as UI.

## Main Menu and menus

### `UI/Components/RiryToolGUI.cs`

| Method | Text |
|---|---|
| `Stopped` | Help boxes (1, line 17) |

### `UI/RiryToolsMainMenu.cs`

| Method | Text |
|---|---|
| `RiryToolsMainMenu` | Tabs and toolbars (4, lines 26-33) |
| `Open` | Window titles (1, line 87) |
| `OnGUI` | Buttons (1, line 131) |
| `DrawTab` | Toggles (3, lines 158-231), Help boxes (5, lines 160-207), Buttons (12, lines 201-225: PopUp Tools' labels passed to `PopUpButton`) |
| `DrawStopToggle` | Toggles (1, line 251) |

### `UI/RiryToolsMenuBar.cs`

| Method | Text |
|---|---|
| `attributes and constants` | Menu entries (28, lines 23-64) |
| `VrcSdkMissing` | Dialogs (1, line 278) |
| `FTEmulatorUnavailable` | Text shown through a variable (1, line 320) |
| `FTEmulatorToolsItem` | Dialogs (1, line 330) |

### `UI/RiryToolsRightClickMenu.cs`

| Method | Text |
|---|---|
| `attributes and constants` | Menu entries (16, lines 26-45) |

### `Components/StopAllToolScripts.cs`

| Method | Text |
|---|---|
| `StopAllToolScripts` | Text shown through a variable (1, line 15) |

## Animator Tools: Clean Animation Keys

### `UI/AnimatorTools/CleanAnimationKeysButton.cs`

| Method | Text |
|---|---|
| `InstallButton` | Tooltips (1, line 84) |

### `UI/AnimatorTools/CleanAnimationKeysWhitelistWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (1, line 44), Buttons (1, line 56) |

### `UI/AnimatorTools/CleanAnimationKeysWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (3, lines 152-160), Toggles (4, lines 165-171), Buttons (1, line 183) |
| `DrawPreview` | Labels (1, line 196) |
| `DrawAnimations` | Buttons (1, line 251) |
| `DrawWhitelist` | Buttons (1, line 267) |
| `Clean` | Dialogs (2, lines 347-360) |

### `AnimatorTools/CleanAnimationKeys.cs`

| Method | Text |
|---|---|
| `CleanAnimationKeys` | Text shown through a variable (1, line 17) |
| `Clean` | Text shown through a variable (1, line 140) |
| `TryValidateEditableClip` | Text shown through a variable (2, lines 305-313) |
| `TryRemoveBindings` | Text shown through a variable (1, line 410) |

## Animator Tools: Controller Cleaner

### `UI/AnimatorTools/ControllerCleanerWindow.cs`

| Method | Text |
|---|---|
| `ControllerCleanerWindow` | Text shown through a variable (1, line 16) |
| `OnGUI` | Field labels (1, line 36), Buttons (5, lines 39-67), Labels (2, lines 51-64) |
| `Clean` | Dialogs (1, line 87) |

## Animator Tools: Find and Replace

### `VRChat/UI/AnimatorTools/BehaviourFindReplaceWindow.cs`

| Method | Text |
|---|---|
| `OpenPopup` | Window titles (1, line 47) |
| `OnGUI` | Field labels (5, lines 62-98), Buttons (2, lines 74-93), Help boxes (1, line 87), Labels (3, lines 102-111) |
| `DrawMap` | Field labels (1, line 123) |
| `Apply` | Dialogs (2, lines 180-191) |
| `ApplyToChild` | Dialogs (2, lines 221-229) |

### `VRChat/AnimatorTools/BehaviourFindReplaceChildPrefab.cs`

| Method | Text |
|---|---|
| `Make` | Text shown through a variable (2, lines 55-66) |
| `Remap` | Text shown through a variable (1, line 122) |

## Animator Tools: Extract Animations

### `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (2, lines 95-109), Toggles (1, line 115), Buttons (2, lines 117-138) |
| `SettingsChanged` | Dialogs (1, line 164) |

### `ImportPresets/ImportPresetAnimations.cs`

| Method | Text |
|---|---|
| `Extract` | Dialogs (1, line 389) |

## Attach To

### `UI/Utilities/AttachToWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (2, lines 43-98), Help boxes (3, lines 49-65), Buttons (1, line 85) |

### `Utilities/AttachTo.cs`

| Method | Text |
|---|---|
| `Attach` | Dialogs (3, lines 100-123) |

## AutoSync

### `UI/AutoSync/AutoSyncConflictNotice.cs`

| Method | Text |
|---|---|
| `Show` | Dialogs (1, line 18) |

### `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs`

| Method | Text |
|---|---|
| `Open` | Text shown through a variable (1, line 18) |
| `OnGUI` | Labels (3, lines 28-53), Field labels (7, lines 33-81), Buttons (2, lines 63-107), Help boxes (1, line 102) |
| `SaveButton` | Buttons (1, line 115), Dialogs (1, line 119) |

### `UI/AutoSync/AutoSyncGitLargeFilesWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (1, line 22), Buttons (1, line 43) |

### `AutoSync/AutoSync.cs`

| Method | Text |
|---|---|
| `CommitNow` | Text shown through a variable (1, line 132) |
| `LargeFileNotice` | Dialogs (1, line 227) |

### `AutoSync/AutoSyncGitActions.cs`

| Method | Text |
|---|---|
| `Guard` | Dialogs (4, lines 35-115) |
| `EnsureTemplates` | Dialogs (1, line 74) |
| `CopyTemplate` | Dialogs (1, line 90) |

### `AutoSync/AutoSyncGitLargeFiles.cs`

| Method | Text |
|---|---|
| `Ignore` | Text shown through a variable (2, lines 65-72) |

### `AutoSync/AutoSyncGitLfs.cs`

| Method | Text |
|---|---|
| `Enable` | Text shown through a variable (1, line 23) |

### `AutoSync/AutoSyncGitTemplates.cs`

| Method | Text |
|---|---|
| `Download` | Progress bars (1, line 118), Text shown through a variable (1, line 131) |

### `AutoSync/AutoSyncJob.cs`

| Method | Text |
|---|---|
| `Open` | Text shown through a variable (2, lines 85-88) |

## Avatar Migration Assistant

### `UI/AvatarMigrator/AvatarMigratorMatchWindow.cs`

| Method | Text |
|---|---|
| `AvatarMigratorMatchWindow` | Text shown through a variable (1, line 21) |
| `OnGUI` | Labels (5, lines 132-139), Notifications (1, line 179), Help boxes (1, line 186), Buttons (2, lines 191-194) |

### `UI/AvatarMigrator/AvatarMigratorPreFlightWindow.cs`

| Method | Text |
|---|---|
| `Run` | Window titles (1, line 41) |
| `Test` | Text shown through a variable (3, lines 72-135) |
| `OnGUI` | Labels (2, lines 250-256), Tabs and toolbars (1, line 262), Buttons (2, lines 270-271) |

### `UI/AvatarMigrator/AvatarMigratorRunReportWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 18) |
| `OnGUI` | Buttons (1, line 46) |

### `UI/AvatarMigrator/AvatarMigratorSdkNoticeWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Toggles (1, line 38), Buttons (1, line 40) |

### `UI/AvatarMigrator/AvatarMigratorWindow.cs`

| Method | Text |
|---|---|
| `AvatarMigratorWindow` | Text shown through a variable (2, lines 25-28) |
| `OnGUI` | Buttons (2, lines 139-140) |
| `DrawMatching` | Foldouts (1, line 150), Field labels (1, line 153), Toggles (1, line 156) |
| `DrawCopy` | Foldouts (1, line 164), Labels (1, line 167) |
| `DrawType` | Toggles (19, lines 227-260), Field labels (1, line 230) |
| `DrawIgnore` | Foldouts (1, line 269), Notifications (1, line 279), Toggles (1, line 290) |
| `DrawAnimations` | Foldouts (1, line 297), Labels (2, lines 302-318), Buttons (2, lines 304-331), Toggles (1, line 322) |

### `AvatarMigrator/AvatarMigratorCopyRun.UpgradeConstraints.cs`

| Method | Text |
|---|---|
| `ConvertOnCopy` | Text shown through a variable (1, line 118) |

### `AvatarMigrator/AvatarMigratorCopyTypes.cs`

| Method | Text |
|---|---|
| `CopyTemplateIfMissing` | Text shown through a variable (1, line 155) |

### `AvatarMigrator/AvatarMigratorMigratedFolder.cs`

| Method | Text |
|---|---|
| `Choose` | Dialogs (1, line 37) |

## Blendshape Sync

### `UI/BlendshapeSync/BlendshapeSyncJobList.cs`

| Method | Text |
|---|---|
| `Draw` | Help boxes (1, line 17), Tooltips (1, line 35), Buttons (5, lines 42-60), Dialogs (1, line 51) |

### `UI/BlendshapeSync/BlendshapeSyncJobWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 26) |

### `UI/BlendshapeSync/BlendshapeSyncWindow.UI.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 16) |
| `OnGUI` | Help boxes (2, lines 24-121), Field labels (1, line 31), Labels (1, line 52), Buttons (3, lines 72-115), Foldouts (1, line 74), Toggles (1, line 89), Tooltips (1, line 97) |
| `DrawManualLinks` | Labels (2, lines 138-144) |

### `UI/BlendshapeSync/BlendshapeSyncWindow.cs`

| Method | Text |
|---|---|
| `IndexLink` | Text shown through a variable (1, line 100) |
| `OnUndoRedo` | Text shown through a variable (1, line 161) |
| `ShowAmbiguityDialog` | Dialogs (1, line 251) |

### `BlendshapeSync/BlendshapeSyncWindow.Checks.cs`

| Method | Text |
|---|---|
| `CanAssign` | Text shown through a variable (2, lines 22-26) |
| `LoopError` | Text shown through a variable (1, line 57) |
| `RendererError` | Text shown through a variable (5, lines 85-92) |

### `BlendshapeSync/BlendshapeSyncWindow.Plan.cs`

| Method | Text |
|---|---|
| `BuildPlan` | Text shown through a variable (6, lines 45-95) |
| `TryBuildLinks` | Text shown through a variable (5, lines 118-155) |

### `BlendshapeSync/BlendshapeSyncWindow.Sync.cs`

| Method | Text |
|---|---|
| `SyncNow` | Text shown through a variable (3, lines 39-83) |
| `RestoreOriginals` | Text shown through a variable (2, lines 176-182) |

## Constraint Tools

### `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (1, line 23) |
| `DrawChain` | Labels (2, lines 42-60), Tooltips (2, lines 44-46) |
| `DrawOptions` | Labels (1, line 65), Tooltips (5, lines 67-82) |
| `DrawRig` | Labels (3, lines 88-105), Buttons (2, lines 113-117) |

### `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`

| Method | Text |
|---|---|
| `ChainError` | Text shown through a variable (4, lines 74-81) |
| `Generate` | Text shown through a variable (2, lines 128-240) |
| `RemoveRig` | Text shown through a variable (3, lines 407-461), Dialogs (1, line 441) |

### `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs`

| Method | Text |
|---|---|
| `ConstraintToolWindow` | Text shown through a variable (1, line 15), Tabs and toolbars (1, line 16) |

### `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (1, line 23) |
| `DrawRealChain` | Labels (2, lines 44-67), Field labels (2, lines 45-46), Tooltips (1, line 50) |
| `DrawTargets` | Labels (1, line 72), Field labels (1, line 73), Tooltips (2, lines 82-87), Buttons (1, line 96) |
| `DrawChainRow` | Buttons (1, line 110) |
| `DrawTiming` | Labels (1, line 120), Tooltips (2, lines 124-132), Buttons (1, line 126) |
| `DrawPrefabSave` | Help boxes (2, lines 173-178), Buttons (2, lines 175-181) |
| `DrawOutput` | Labels (1, line 186), Tooltips (1, line 188), Buttons (2, lines 192-193) |

### `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`

| Method | Text |
|---|---|
| `SaveToPrefab` | Text shown through a variable (4, lines 124-152), Dialogs (2, lines 127-141) |
| `GenerationError` | Text shown through a variable (2, lines 173-175) |
| `Generate` | Text shown through a variable (2, lines 184-190) |
| `GenerateMissing` | Text shown through a variable (1, line 209) |
| `TryGather` | Text shown through a variable (6, lines 226-274) |
| `SetUpConstraints` | Dialogs (1, line 350), Text shown through a variable (1, line 396) |
| `IsInImmutablePackage` | Text shown through a variable (2, lines 431-443), Dialogs (1, line 434) |

### `VRChat/UI/ConstraintTools/InsertablePropWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (1, line 25) |
| `DrawPair` | Labels (1, line 47), Tooltips (3, lines 49-54) |
| `DrawOptions` | Labels (1, line 59), Field labels (1, line 60), Tooltips (6, lines 69-84) |
| `DrawAnimator` | Labels (2, lines 91-110), Tooltips (2, lines 95-102), Buttons (2, lines 97-104) |
| `DrawHandles` | Labels (2, lines 118-135), Help boxes (1, line 122), Buttons (1, line 132) |
| `DrawGenerate` | Labels (1, line 144), Buttons (1, line 153) |

### `VRChat/UI/ConstraintTools/InsertablePropWindow.cs`

| Method | Text |
|---|---|
| `GenerateError` | Text shown through a variable (9, lines 114-127) |
| `Generate` | Text shown through a variable (4, lines 143-175), Dialogs (1, line 149) |
| `RemovePair` | Text shown through a variable (2, lines 188-208), Dialogs (1, line 194) |
| `NewController` | Text shown through a variable (1, line 229) |
| `NewParameters` | Text shown through a variable (1, line 242) |

### `VRChat/Components/ConstraintToolChains.cs`

| Method | Text |
|---|---|
| `TryCollectChain` | Text shown through a variable (2, lines 24-29) |

### `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainChains.cs`

| Method | Text |
|---|---|
| `TryFollowChain` | Text shown through a variable (2, lines 32-44) |
| `AnimationPathError` | Text shown through a variable (2, lines 115-120) |

### `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPlanner.cs`

| Method | Text |
|---|---|
| `Validate` | Text shown through a variable (5, lines 86-97) |

### `VRChat/ConstraintTools/ConstraintTrain/ConstraintTrainPrefabs.cs`

| Method | Text |
|---|---|
| `SaveAsRootVariant` | Text shown through a variable (1, line 161) |

### `VRChat/ConstraintTools/InsertableProp/InsertablePropRig.cs`

| Method | Text |
|---|---|
| `AvatarError` | Text shown through a variable (2, lines 330-334) |

### `VRChat/UI/ConstraintTools/TiltDetectorWindow.cs`

| Method | Text |
|---|---|
| `TiltDetectorWindow` | Text shown through a variable (2, lines 15-16: Flat plane and Space choices) |
| `OnGUI` | Help boxes (1, line 57), Labels (3, lines 61-89), Field labels (5, lines 67-76, with tooltips), Buttons (1, line 97) |
| `Build` | Text shown through a variable (2, lines 109-115) |

### `VRChat/ConstraintTools/TiltDetector/TiltDetectorBuilder.cs`

| Method | Text |
|---|---|
| `Problem` | Text shown through a variable (7, lines 61-68) |

## FTEmulator

### `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorDebugWindow.cs`

| Method | Text |
|---|---|
| `FTEmulatorDebugWindow` | Text shown through a variable (2, lines 19-26), Tabs and toolbars (1, line 24) |
| `OnGUI` | Help boxes (1, line 119), Labels (1, line 131) |
| `DrawHardware` | Labels (1, line 150) |
| `DrawRow` | Labels (2, lines 228-229) |
| `DrawMappings` | Labels (5, lines 288-310), Field labels (2, lines 311-312), Buttons (2, lines 318-322) |
| `OverrideMapping` | Text shown through a variable (1, line 341) |
| `DrawDiagnostics` | Buttons (1, line 354) |

### `com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs`

| Method | Text |
|---|---|
| `FTEmulatorWindow` | Text shown through a variable (1, line 21) |
| `Draw` | Help boxes (2, lines 89-100), Buttons (1, line 90), Labels (1, line 99) |
| `DrawAvatar` | Field labels (2, lines 121-145), Help boxes (2, lines 124-164), Labels (2, lines 127-158), Buttons (1, line 130), Foldouts (1, line 153) |
| `DrawInputAndHardware` | Field labels (3, lines 172-189), Buttons (1, line 198) |
| `DrawControls` | Buttons (4, lines 207-240), Toggles (2, lines 228-232) |
| `DrawOsc` | Labels (6, lines 247-260), Field labels (1, line 249), Toggles (1, line 251), Buttons (2, lines 267-272) |
| `DrawRecording` | Labels (3, lines 282-284), Buttons (5, lines 291-310) |
| `DrawManual` | Labels (2, lines 338-345), Toggles (1, line 339) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulator.cs`

| Method | Text |
|---|---|
| `FTEmulator` | Text shown through a variable (1, line 24) |
| `Start` | Text shown through a variable (1, line 141) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorCatalog.cs`

| Method | Text |
|---|---|
| `LoadFromPackage` | Text shown through a variable (1, line 159) |
| `Parse` | Text shown through a variable (4, lines 190-222) |
| `Root` | Text shown through a variable (1, line 246) |
| `Id` | Text shown through a variable (1, line 253) |
| `Evidence` | Text shown through a variable (1, line 260) |
| `ReadSchema` | Text shown through a variable (4, lines 280-291) |
| `ReadHardware` | Text shown through a variable (6, lines 316-362) |
| `ReadAvatar` | Text shown through a variable (10, lines 397-462) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorExpression.cs`

| Method | Text |
|---|---|
| `Compile` | Text shown through a variable (5, lines 62-88) |
| `Next` | Text shown through a variable (2, lines 150-179) |
| `Primary` | Text shown through a variable (8, lines 231-283) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorJson.cs`

| Method | Text |
|---|---|
| `Parse` | Text shown through a variable (2, lines 22-23) |
| `Write` | Text shown through a variable (2, lines 162-200) |
| `List` | Text shown through a variable (1, line 240) |
| `Text` | Text shown through a variable (1, line 246) |
| `Number` | Text shown through a variable (1, line 252) |
| `Flag` | Text shown through a variable (1, line 258) |
| `Texts` | Text shown through a variable (1, line 266) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscCodec.cs`

| Method | Text |
|---|---|
| `TryDecode` | Text shown through a variable (2, lines 47-48) |
| `Element` | Text shown through a variable (5, lines 60-69) |
| `Message` | Text shown through a variable (12, lines 81-127) |
| `TryDecodeNativeFace` | Text shown through a variable (1, line 166) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscQuery.cs`

| Method | Text |
|---|---|
| `Start` | Text shown through a variable (1, line 188) |
| `StartHttp` | Text shown through a variable (1, line 229) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorOscReceiver.cs`

| Method | Text |
|---|---|
| `FTEmulatorPortInUseException` | Text shown through a variable (1, line 15) |
| `Start` | Text shown through a variable (1, line 101) |

### `com.riry.ftemulator/Editor/FTEmulator/FTEmulatorSession.cs`

| Method | Text |
|---|---|
| `Start` | Text shown through a variable (3, lines 224-226) |
| `Snapshot` | Text shown through a variable (3, lines 862-889) |

### `com.riry.ftemulator/Editor/FTEmulator/GestureManager/FTEmulatorGestureManager.cs`

| Method | Text |
|---|---|
| `Bind` | Text shown through a variable (2, lines 48-49) |

## GUID Tools

### `UI/GuidTools/GuidToolBrokenReferenceWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (4, lines 26-83), Buttons (5, lines 30-114, with tooltips), Help boxes (1, line 48), Foldouts (1, line 91) |
| `Header` | Buttons (1, line 125) |
| `DrawLocations` | Tooltips (1, line 145) |
| `DrawCandidate` | Labels (1, line 178) |

### `UI/GuidTools/GuidToolBrokenReferenceWindow.cs`

| Method | Text |
|---|---|
| `OpenForOpenScenes` | Dialogs (1, line 53) |
| `Rescan` | Dialogs (1, line 96) |
| `ScrubUnusedOverrides` | Dialogs (2, lines 207-212) |
| `OfferControllerCleaner` | Dialogs (1, line 236) |
| `ApplyRedirects` | Dialogs (5, lines 261-312) |

### `UI/GuidTools/GuidToolBrokenSaveWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (2, lines 52-54), Buttons (1, line 60) |

### `UI/GuidTools/GuidToolClearReferenceCache.cs`

| Method | Text |
|---|---|
| `GuidToolClearReferenceCache` | Text shown through a variable (1, line 13) |
| `Run` | Dialogs (4, lines 27-58) |

### `UI/GuidTools/GuidToolRegenerate.Prompts.cs`

| Method | Text |
|---|---|
| `RegenerateSelectedGuids` | Dialogs (5, lines 42-103), Text shown through a variable (1, line 120), Progress bars (1, line 139) |
| `ReconcileAfterRefresh` | Dialogs (1, line 211) |
| `ConfirmUnusualFiles` | Dialogs (1, line 252) |

### `UI/GuidTools/GuidToolsWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 12) |
| `OnGUI` | Labels (3, lines 22-56), Help boxes (1, line 23), Buttons (4, lines 30-48) |

### `UI/GuidTools/GuidToolPrefabCleanerWindow.cs`

| Method | Text |
|---|---|
| `GuidToolPrefabCleanerWindow` | Text shown through a variable (3, line 15: Target choices) |
| `Open` | Window titles (1, line 25, `GuidToolUnusedOverrides.Title`) |
| `OnGUI` | Labels (2, lines 43-51), Field labels (2, lines 45-49, with tooltips), Buttons (1, line 55) |

### `UI/GuidTools/GuidToolTargetsWindow.cs`

| Method | Text |
|---|---|
| `RegeneratorTitle` / `RepairTitle` | Text shown through a variable (2, lines 17-18: window titles and the Main Menu's buttons) |
| `Open` | Window titles (1, line 32) |
| `OnGUI` | Labels (2, lines 49-53), Field labels (1, line 69), Buttons (2, lines 64-75) |

### `GuidTools/GuidToolUnusedOverrides.cs`

| Method | Text |
|---|---|
| `Title` | Text shown through a variable (1, line 20: `Prefab Cleaner`, the dialog, popup and menu name) |

### `GuidTools/GuidToolBrokenReferenceMonitor.cs`

| Method | Text |
|---|---|
| `PromptOnOpen` | Dialogs (1, line 98) |
| `Prompt` | Dialogs (1, line 119) |

### `GuidTools/GuidToolReferenceRepair.cs`

| Method | Text |
|---|---|
| `DepthOf` | Text shown through a variable (1, line 139) |
| `Scan` | Progress bars (1, line 196) |
| `Restore` | Text shown through a variable (1, line 668) |
| `FindInHistory` | Progress bars (1, line 952) |

### `GuidTools/GuidToolRegenerate.Patch.cs`

| Method | Text |
|---|---|
| `ApplyPatches` | Progress bars (1, line 130) |
| `EnsureWritable` | Text shown through a variable (1, line 279) |

### `GuidTools/GuidToolRegenerate.Scan.cs`

| Method | Text |
|---|---|
| `CachedOffsetsStillMatch` | Progress bars (1, line 143) |

### `GuidTools/GuidToolRegenerate.cs`

| Method | Text |
|---|---|
| `BuildTargets` | Text shown through a variable (2, lines 233-251) |
| `MakeProjectRelative` | Text shown through a variable (1, line 284) |

## IconCap

### `UI/IconCap/IconCapEffectStackGUI.cs`

| Method | Text |
|---|---|
| `IconCapEffectStackGUI` | Labels (1, line 27) |
| `Draw` | Buttons (1, line 44) |
| `DrawElement` | Tooltips (7, lines 123-187) |

### `UI/IconCap/IconCapNamePrompt.cs`

| Method | Text |
|---|---|
| `OnGUI` | Buttons (2, lines 41-44) |

### `UI/IconCap/IconCapWindow.UI.cs`

| Method | Text |
|---|---|
| `DrawTop` | Field labels (2, lines 81-91), Buttons (2, lines 87-92) |
| `DrawPreview` | Field labels (1, line 107) |
| `DrawCaptureStack` | Foldouts (1, line 113) |
| `DrawExport` | Field labels (1, line 121), Buttons (3, lines 122-138) |
| `DrawObjects` | Foldouts (1, line 146), Toggles (1, line 149), Field labels (1, line 171), Buttons (1, line 175) |
| `DrawCamera` | Foldouts (1, line 184), Field labels (3, lines 188-203), Buttons (1, line 204) |
| `DrawBackground` | Foldouts (1, line 212), Field labels (2, lines 216-220), Toggles (1, line 218) |
| `DrawCaptureOptions` | Toggles (7, lines 228-247), Field labels (2, lines 230-234), Labels (1, line 240) |
| `DrawRestore` | Foldouts (1, line 254) |
| `DrawRestoreRow` | Buttons (2, lines 277-278) |
| `DrawGenerator` | Foldouts (2, lines 285-328), Field labels (6, lines 290-323), Buttons (2, lines 291-295), Toggles (1, line 308), Help boxes (1, line 332) |
| `DrawSources` | Labels (1, line 363) |
| `DrawJobs` | Foldouts (1, line 385) |
| `DrawCurrentJob` | Labels (2, lines 423-425), Buttons (4, lines 428-479), Field labels (1, line 469) |
| `DrawSavedJobs` | Buttons (3, lines 519-531) |

### `UI/IconCap/IconCapWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 78) |
| `Capture` | Dialogs (1, line 416) |
| `ImageOf` | Dialogs (1, line 438) |
| `Export` | Dialogs (1, line 471) |
| `SaveJob` | Dialogs (1, line 556) |
| `Run` | Dialogs (2, lines 571-581) |
| `SavePreset` | Dialogs (1, line 592) |
| `Regenerate` | Dialogs (1, line 640) |

### `IconCap/IconCapEffects.cs`

| Method | Text |
|---|---|
| `IconCapEffects` | Text shown through a variable (1, line 29) |

### `IconCap/IconCapFiles.cs`

| Method | Text |
|---|---|
| `Run` | Progress bars (1, line 159) |

### `IconCap/IconCapRenderer.cs`

| Method | Text |
|---|---|
| `Targets` | Text shown through a variable (4, lines 56-87) |
| `Draws` | Text shown through a variable (1, line 234) |

### `IconCap/IconGenerator.cs`

| Method | Text |
|---|---|
| `Regenerate` | Progress bars (1, line 91) |

## Import Presets

### `UI/ImportPresets/ImportPresetDragOverlay.cs`

| Method | Text |
|---|---|
| `Draw` | Labels (3, lines 84-94) |

### `UI/ImportPresets/ImportPresetFolderWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (1, line 35), Help boxes (3, lines 59-74), Buttons (3, lines 69-102), Toggles (1, line 85) |
| `DrawTags` | Labels (2, lines 112-122), Toggles (2, lines 116-119), Foldouts (1, line 124), Buttons (1, line 145) |
| `SaveTags` | Text shown through a variable (1, line 172) |
| `DrawBlenderSettings` | Labels (2, lines 180-192), Field labels (1, line 183), Buttons (1, line 184), Tooltips (1, line 195) |

### `UI/ImportPresets/ImportPresetFolderWindow.cs`

| Method | Text |
|---|---|
| `LoadConfigurations` | Text shown through a variable (1, line 59) |
| `Apply` | Text shown through a variable (2, lines 143-146) |

### `UI/ImportPresets/ImportPresetQuickWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 42) |
| `OnGUI` | Toggles (1, line 96), Buttons (2, lines 112-115) |
| `DrawGroup` | Foldouts (1, line 135), Labels (1, line 139) |
| `Apply` | Dialogs (1, line 183) |

### `UI/ImportPresets/ImportPresetSaveWindow.UI.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 18) |
| `OnGUI` | Help boxes (1, line 52), Labels (2, lines 56-57), Field labels (3, lines 62-64), Buttons (3, lines 76-87) |

### `UI/ImportPresets/ImportPresetSaveWindow.cs`

| Method | Text |
|---|---|
| `Save` | Dialogs (1, line 29), Text shown through a variable (2, lines 39-43) |

### `ImportPresets/ImportPresetBetterFBX.cs`

| Method | Text |
|---|---|
| `OptionsError` | Text shown through a variable (1, line 92) |
| `Export` | Text shown through a variable (3, lines 191-243) |
| `Run` | Progress bars (1, line 269) |

### `ImportPresets/ImportPresetFolderRules.cs`

| Method | Text |
|---|---|
| `ConfirmApply` | Dialogs (1, line 358) |

### `ImportPresets/ImportPresetLibrary.cs`

| Method | Text |
|---|---|
| `ValidateName` | Text shown through a variable (5, lines 74-84) |
| `ReadConfig` | Text shown through a variable (1, line 134) |

### `ImportPresets/ImportPresetPreferences.cs`

| Method | Text |
|---|---|
| `ValidateTag` | Text shown through a variable (1, line 116) |

## Material Cleaner

### `UI/Utilities/MaterialCleanerWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (1, line 40), Buttons (5, lines 43-73), Labels (2, lines 55-70) |

### `Utilities/MaterialCleaner.cs`

| Method | Text |
|---|---|
| `MaterialCleaner` | Text shown through a variable (1, line 15) |

## Menu Tools

### `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (5, lines 106-129), Context menu items (1, line 124), Help boxes (1, line 131), Buttons (2, lines 136-139) |

### `VRChat/UI/MenuTool/MenuToolWindow.cs`

| Method | Text |
|---|---|
| `MenuToolWindow` | Text shown through a variable (1, line 21), Tabs and toolbars (1, line 22) |
| `AddMoveItems` | Context menu items (6, lines 343-369) |
| `OnRadialContext` | Context menu items (9, lines 410-442) |
| `DrawWindow` | Help boxes (2, lines 523-544) |
| `DrawHeader` | Buttons (2, lines 603-604) |
| `DrawBudget` | Help boxes (1, line 618), Labels (1, line 623) |
| `DrawSummary` | Labels (2, lines 633-643), Help boxes (1, line 634), Foldouts (1, line 646) |
| `DrawShared` | Foldouts (1, line 673), Tooltips (1, line 678), Buttons (1, line 685) |
| `DrawBlocks` | Foldouts (1, line 692), Labels (1, line 703), Buttons (1, line 707) |
| `DrawMenuChecks` | Foldouts (2, lines 722-752: the two warning boxes' titles), Labels (2, lines 729-755), Links (1, line 737: each asset's name, with a tooltip), Buttons (1, line 757) |
| `ApplyIconPreset` | Dialogs (1, line 767) |
| `DrawNotes` | Foldouts (1, line 790) |
| `DrawManager` | Help boxes (1, line 816), Buttons (3, lines 822-827), Labels (1, line 832) |
| `DrawPlan` | Labels (1, line 848) |
| `ChangePlan` | Buttons (1, line 881) |
| `DrawRadial` | Help boxes (1, line 942), Labels (1, line 964) |
| `DrawBits` | Labels (2, lines 991-1002), Foldouts (1, line 1004) |
| `DrawEntryState` | Buttons (3, lines 1020-1031) |
| `Discard` | Dialogs (1, line 1106) |
| `ApplyPlan` | Text shown through a variable (1, line 1119), Dialogs (2, lines 1124-1139) |

### `VRChat/MenuTool/MenuToolApply.cs`

| Method | Text |
|---|---|
| `Repath` | Text shown through a variable (1, line 313) |
| `NewRule` | Text shown through a variable (1, line 465) |
| `NewMaObject` | Text shown through a variable (1, line 496) |

### `VRChat/MenuTool/MenuToolBudget.cs`

| Method | Text |
|---|---|
| `Estimate` | Text shown through a variable (1, line 77) |

### `VRChat/MenuTool/MenuToolPlanner.cs`

| Method | Text |
|---|---|
| `Check` | Text shown through a variable (2, lines 90-100) |
| `CheckIcon` | Text shown through a variable (1, line 150) |
| `CheckRename` | Text shown through a variable (1, line 238) |
| `Decide` | Text shown through a variable (2, lines 264-341) |
| `CheckAdd` | Text shown through a variable (3, lines 289-306) |
| `CheckNewItem` | Text shown through a variable (2, lines 366-367) |
| `Ownership` | Text shown through a variable (1, line 418) |
| `IsInstallRoot` | Text shown through a variable (2, lines 446-455) |
| `Destructive` | Text shown through a variable (4, lines 495-523) |

### `VRChat/MenuTool/MenuToolScanner.ModularAvatar.cs`

| Method | Text |
|---|---|
| `AddModularAvatar` | Text shown through a variable (1, line 44) |
| `AddInstallerContent` | Text shown through a variable (1, line 67) |
| `AddMaItem` | Text shown through a variable (1, line 95) |
| `AddMaParameters` | Text shown through a variable (1, line 152) |

### `VRChat/MenuTool/MenuToolScanner.Native.cs`

| Method | Text |
|---|---|
| `AddMenuAsset` | Text shown through a variable (2, lines 63-84) |

### `VRChat/MenuTool/MenuToolScanner.VRCFury.cs`

| Method | Text |
|---|---|
| `AddVrcfuryFeature` | Text shown through a variable (1, line 225) |
| `AddVrcfuryToggle` | Text shown through a variable (1, line 251) |
| `Declare` | Text shown through a variable (1, line 346) |

### `VRChat/MenuTool/MenuToolScanner.cs`

| Method | Text |
|---|---|
| `Scan` | Text shown through a variable (1, line 84) |
| `CollectParameterUse` | Text shown through a variable (1, line 252) |

### `VRChat/MenuTool/MenuToolVrcfuryMenu.cs`

| Method | Text |
|---|---|
| `Apply` | Text shown through a variable (3, lines 262-273) |

## Debug Tools: Find Null References

### `UI/Debug/FindNullReferencesWindow.cs`

| Method | Text |
|---|---|
| `Run` | Window titles (1, line 16) |
| `OnGUI` | Labels (1, line 31) |

## Debug Tools: Menu Mockup

### `UI/Debug/MenuMockupComponentsWindow.cs`

| Method | Text |
|---|---|
| `CreateGUI` | Tooltips (1, line 61) |
| `Load` | Dialogs (1, line 114) |

### `UI/Debug/MenuMockupEditWindow.cs`

| Method | Text |
|---|---|
| `Save` | Dialogs (1, line 687) |

### `UI/Debug/MenuMockupNameWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 23) |
| `OnGUI` | Field labels (1, line 34), Buttons (2, lines 41-42) |
| `Save` | Dialogs (1, line 52) |

### `UI/Debug/MenuMockupPreview.cs`

| Method | Text |
|---|---|
| `Create` | Help boxes (1, line 43) |

### `UI/Debug/MenuMockupTextWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 23) |
| `OnGUI` | Labels (1, line 41) |

### `Debug/MenuMockupTypes.cs`

| Method | Text |
|---|---|
| `MenuMockupTypes` | Tabs and toolbars (1, line 15) |

## Debug Tools: Script Load Guard

### `UI/Debug/ScriptLoadGuardFailureWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 19) |
| `OnGUI` | Labels (2, lines 32-42), Help boxes (1, line 44) |
| `Finish` | Buttons (3, lines 59-68), Dialogs (1, line 70) |

## Refresh Scripts

### `Utilities/RefreshScripts.cs`

| Method | Text |
|---|---|
| `RefreshScripts` | Text shown through a variable (1, line 20) |

## Shared

### `Components/GestureManagerNotFoundException.cs`

| Method | Text |
|---|---|
| `GestureManagerNotFoundException` | Text shown through a variable (1, line 13) |

### `Components/GitNotInstalledException.cs`

| Method | Text |
|---|---|
| `GitNotInstalledException` | Text shown through a variable (1, line 18) |

### `Components/RiryToolsProjectFolder.cs`

| Method | Text |
|---|---|
| `attributes and constants` | Menu entries (1, line 11) |

### `Components/VrcSdkNotFoundException.cs`

| Method | Text |
|---|---|
| `VrcSdkNotFoundException` | Text shown through a variable (1, line 13) |

