# UI language spec

Where the text of every UI element lives, for translating the UI. Source paths
are relative to `com.riry.rirytools/Editor/`. Each file lists the methods that
hold UI text, the kind of text and its line numbers on `betastaging`; line
numbers drift as files change, the methods don't.

## Kinds of text

1. **Menu entries:** `[MenuItem]` paths. Unity reads them when scripts compile,
   so a language change needs a script reload. Menu bar paths are `const string`
   fields at the top of `UI/RiryToolsMenuBar.cs`; the Assets and Hierarchy
   right-click paths and the VRChat menus are written in their attributes.
2. **Window titles:** `titleContent` and `GetWindow` titles, set in each window's
   `Open` (or the method that opens it).
3. **Tabs and toolbars:** `string[]` fields (for example `Tabs`, `ToolsTabs`,
   `AnimatorTabs`, `ImportTabs` in `UI/RiryToolsMainMenu.cs`) and inline
   `Toolbar` arrays.
4. **Labels, field labels, buttons, toggles, foldouts, help boxes, tooltips:**
   string literals and `GUIContent` in the drawing methods (`OnGUI`, `Draw*`).
5. **Context menu items:** `GenericMenu.AddItem` / `AddDisabledItem`.
6. **Dialogs:** `EditorUtility.DisplayDialog*` title, message and button text.
7. **Progress bars:** `DisplayProgressBar` / `DisplayCancelableProgressBar`
   titles and messages, some of them in feature scripts.
8. **Notifications:** `ShowNotification` text.

Not covered: `RiryLog` and `Debug.Log` messages, settings keys, asset paths and
other strings the user never sees as UI.

## Main Menu and menus

### `UI/Components/RiryToolGUI.cs`

| Method | Text |
|---|---|
| `Stopped` | Help boxes (1, line 17) |
| `ExitAfterModal` | Progress bars (1, line 58) |

### `UI/RiryToolsMainMenu.cs`

| Method | Text |
|---|---|
| `RiryToolsMainMenu` | Tabs and toolbars (4, lines 26-36) |
| `Open` | Window titles (1, line 81) |
| `OnGUI` | Buttons (1, line 124) |
| `DrawTab` | Toggles (3, lines 139-211), Help boxes (5, lines 141-194), Buttons (3, lines 183-219) |

### `UI/RiryToolsMenuBar.cs`

| Method | Text |
|---|---|
| `attributes` | Menu entries (50, lines 69-311) |
| `VrcSdkMissing` | Dialogs (1, line 271) |

### `UI/RiryToolsRightClickMenu.cs`

| Method | Text |
|---|---|
| `attributes` | Menu entries (22, lines 46-143) |

### `VRChat/UI/RiryToolsVRChatMenus.cs`

| Method | Text |
|---|---|
| `attributes` | Menu entries (16, lines 26-83) |

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
| `Open` | Window titles (1, line 51) |
| `OpenFor` | Window titles (1, line 68) |
| `OnGUI` | Labels (3, lines 144-152), Toggles (4, lines 157-163), Help boxes (1, line 169), Buttons (1, line 172) |
| `DrawAnimations` | Buttons (1, line 204) |
| `DrawWhitelist` | Buttons (2, lines 220-249) |
| `Clean` | Dialogs (2, lines 300-311) |

## Animator Tools: Controller Cleaner

### `UI/AnimatorTools/ControllerCleanerWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 26) |
| `OnGUI` | Field labels (1, line 36), Buttons (5, lines 39-67), Labels (2, lines 51-64) |
| `Clean` | Dialogs (1, line 87) |

## Animator Tools: Find and Replace

### `VRChat/UI/AnimatorTools/BehaviourFindReplaceWindow.cs`

| Method | Text |
|---|---|
| `OpenPopup` | Window titles (1, line 44) |
| `OnGUI` | Field labels (4, lines 59-83), Buttons (2, lines 71-78), Labels (3, lines 87-96) |
| `DrawMap` | Field labels (1, line 108) |
| `Apply` | Dialogs (2, lines 158-169) |
| `ApplyToChild` | Dialogs (2, lines 199-207) |

## Animator Tools: Extract Animations

### `UI/ImportPresets/ImportPresetExtractAnimationWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (2, lines 95-109), Toggles (1, line 115), Buttons (2, lines 117-138), Help boxes (1, line 133) |
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

### `UI/AutoSync/AutoSyncGitConfigureWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (3, lines 28-49), Field labels (6, lines 33-77), Buttons (2, lines 59-103), Help boxes (2, lines 96-98) |
| `SaveButton` | Buttons (1, line 111), Dialogs (1, line 115) |

### `UI/AutoSync/AutoSyncGitLargeFilesWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (1, line 21), Labels (1, line 22), Buttons (1, line 43) |

### `AutoSync/AutoSync.cs`

| Method | Text |
|---|---|
| `LargeFileNotice` | Dialogs (1, line 209) |

### `AutoSync/AutoSyncGitActions.cs`

| Method | Text |
|---|---|
| `Guard` | Dialogs (4, lines 35-115) |
| `EnsureTemplates` | Dialogs (1, line 74) |
| `CopyTemplate` | Dialogs (1, line 90) |

### `AutoSync/AutoSyncGitTemplates.cs`

| Method | Text |
|---|---|
| `Download` | Progress bars (1, line 118) |

## Avatar Migration Assistant

### `UI/AvatarMigrator/AvatarMigratorMatchWindow.cs`

| Method | Text |
|---|---|
| `Review` | Window titles (1, line 52) |
| `OnGUI` | Labels (5, lines 132-139), Notifications (1, line 179), Help boxes (1, line 186), Buttons (2, lines 191-194) |

### `UI/AvatarMigrator/AvatarMigratorPreFlightWindow.cs`

| Method | Text |
|---|---|
| `Run` | Window titles (1, line 41) |
| `OnGUI` | Labels (2, lines 250-256), Help boxes (1, line 251), Tabs and toolbars (1, line 262), Buttons (2, lines 270-271) |

### `UI/AvatarMigrator/AvatarMigratorRunReportWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 18) |
| `OnGUI` | Buttons (1, line 46) |
| `Section` | Help boxes (1, line 55) |

### `UI/AvatarMigrator/AvatarMigratorSdkNoticeWindow.cs`

| Method | Text |
|---|---|
| `ShowIfNeeded` | Window titles (1, line 23) |
| `OnGUI` | Toggles (1, line 38), Buttons (1, line 40) |

### `UI/AvatarMigrator/AvatarMigratorWindow.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (2, lines 134-139), Buttons (1, line 137) |
| `DrawMatching` | Foldouts (1, line 144), Field labels (1, line 147), Toggles (1, line 150) |
| `DrawCopy` | Foldouts (1, line 158), Labels (1, line 161), Tabs and toolbars (1, line 165) |
| `DrawType` | Toggles (19, lines 221-254), Field labels (1, line 224) |
| `DrawIgnore` | Foldouts (1, line 263), Notifications (1, line 273), Toggles (1, line 284) |
| `DrawAnimations` | Foldouts (1, line 291), Labels (2, lines 296-312), Buttons (2, lines 298-325), Toggles (1, line 316) |

### `AvatarMigrator/AvatarMigratorMigratedFolder.cs`

| Method | Text |
|---|---|
| `Choose` | Dialogs (1, line 37) |

### `AvatarMigrator/AvatarMigratorStep.cs`

| Method | Text |
|---|---|
| `Run` | Progress bars (1, line 15) |

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
| `OnGUI` | Help boxes (3, lines 24-121), Field labels (1, line 31), Labels (1, line 52), Buttons (3, lines 72-115), Foldouts (1, line 74), Toggles (1, line 89), Tooltips (1, line 97) |
| `DrawManualLinks` | Labels (2, lines 138-144), Buttons (1, line 148) |

### `UI/BlendshapeSync/BlendshapeSyncWindow.cs`

| Method | Text |
|---|---|
| `ShowAmbiguityDialog` | Dialogs (1, line 251) |

### `BlendshapeSync/BlendshapeSyncWindow.Checks.cs`

| Method | Text |
|---|---|
| `CanAssign` | Notifications (1, line 36) |

## Constraint Tool

### `VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (2, lines 23-37) |
| `DrawChain` | Labels (2, lines 42-60), Tooltips (2, lines 44-46), Help boxes (1, line 51) |
| `DrawOptions` | Labels (1, line 65), Tooltips (5, lines 67-82) |
| `DrawRig` | Labels (3, lines 88-105), Help boxes (1, line 93), Buttons (2, lines 113-117) |

### `VRChat/UI/ConstraintTools/ConstraintBezierWindow.cs`

| Method | Text |
|---|---|
| `RemoveRig` | Dialogs (1, line 441) |

### `VRChat/UI/ConstraintTools/ConstraintToolWindow.cs`

| Method | Text |
|---|---|
| `ConstraintToolWindow` | Tabs and toolbars (1, line 15) |
| `Open` | Window titles (1, line 61) |

### `VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (2, lines 23-39) |
| `DrawRealChain` | Labels (2, lines 44-67), Field labels (2, lines 45-46), Tooltips (1, line 50), Help boxes (1, line 58) |
| `DrawTargets` | Labels (1, line 72), Field labels (1, line 73), Tooltips (2, lines 82-87), Buttons (1, line 96), Help boxes (1, line 99) |
| `DrawChainRow` | Buttons (1, line 110) |
| `DrawTiming` | Labels (1, line 120), Tooltips (2, lines 124-132), Buttons (1, line 126), Help boxes (1, line 159) |
| `DrawPrefabSave` | Help boxes (2, lines 173-178), Buttons (2, lines 175-181) |
| `DrawOutput` | Labels (1, line 186), Tooltips (1, line 188), Buttons (2, lines 192-193) |

### `VRChat/UI/ConstraintTools/ConstraintTrainWindow.cs`

| Method | Text |
|---|---|
| `SaveToPrefab` | Dialogs (2, lines 127-141) |
| `SetUpConstraints` | Dialogs (1, line 350) |
| `IsInImmutablePackage` | Dialogs (1, line 434) |

### `VRChat/UI/ConstraintTools/InsertablePropWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Help boxes (2, lines 25-42) |
| `DrawPair` | Labels (1, line 47), Tooltips (3, lines 49-54) |
| `DrawOptions` | Labels (1, line 59), Field labels (1, line 60), Tooltips (6, lines 69-84) |
| `DrawAnimator` | Labels (2, lines 91-110), Tooltips (2, lines 95-102), Buttons (2, lines 97-104) |
| `DrawHandles` | Labels (2, lines 118-135), Help boxes (1, line 122), Buttons (1, line 132) |
| `DrawGenerate` | Help boxes (1, line 141), Labels (1, line 144), Buttons (1, line 153) |

### `VRChat/UI/ConstraintTools/InsertablePropWindow.cs`

| Method | Text |
|---|---|
| `Generate` | Dialogs (1, line 136) |
| `RemovePair` | Dialogs (1, line 181) |

## FTEmulator

### `VRChat/UI/FTEmulator/FTEmulatorDebugWindow.cs`

| Method | Text |
|---|---|
| `FTEmulatorDebugWindow` | Tabs and toolbars (1, line 24) |
| `OnGUI` | Help boxes (1, line 119), Labels (2, lines 122-131) |
| `DrawHardware` | Labels (1, line 150) |
| `DrawRow` | Labels (2, lines 228-229) |
| `DrawAction` | Tabs and toolbars (1, line 238) |
| `DrawMappings` | Labels (6, lines 288-333), Field labels (2, lines 311-312), Help boxes (1, line 313), Buttons (2, lines 318-322) |
| `DrawDiagnostics` | Buttons (1, line 354) |

### `VRChat/UI/FTEmulator/FTEmulatorWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 53) |
| `Draw` | Help boxes (2, lines 89-100), Buttons (1, line 90), Labels (1, line 99) |
| `DrawAvatar` | Help boxes (3, lines 116-164), Field labels (2, lines 121-145), Labels (3, lines 127-158), Buttons (1, line 130), Foldouts (1, line 153) |
| `DrawInputAndHardware` | Field labels (3, lines 172-189), Buttons (1, line 198) |
| `DrawControls` | Buttons (4, lines 207-240), Help boxes (1, line 222), Toggles (2, lines 228-232) |
| `DrawOsc` | Labels (6, lines 247-260), Field labels (1, line 249), Toggles (1, line 251), Help boxes (1, line 255), Buttons (2, lines 267-272) |
| `DrawManual` | Labels (2, lines 279-286), Toggles (1, line 280) |

## GUID Tools

### `UI/GuidTools/GuidToolBrokenReferenceWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Labels (4, lines 26-78), Buttons (4, lines 30-109), Help boxes (1, line 43), Foldouts (1, line 86) |
| `Header` | Buttons (1, line 120) |
| `DrawLocations` | Tooltips (1, line 140) |
| `DrawCandidate` | Labels (2, lines 169-173) |
| `DrawBreak` | Help boxes (1, line 216) |

### `UI/GuidTools/GuidToolBrokenReferenceWindow.cs`

| Method | Text |
|---|---|
| `OpenForOpenScenes` | Dialogs (1, line 53) |
| `Rescan` | Dialogs (1, line 96) |
| `ScrubUnusedOverrides` | Dialogs (2, lines 201-206) |
| `OfferControllerCleaner` | Dialogs (1, line 230) |
| `ApplyRedirects` | Dialogs (5, lines 251-302) |

### `UI/GuidTools/GuidToolBrokenSaveWindow.cs`

| Method | Text |
|---|---|
| `Confirm` | Window titles (1, line 31) |
| `OnGUI` | Labels (2, lines 52-54), Buttons (1, line 60) |

### `UI/GuidTools/GuidToolClearReferenceCache.cs`

| Method | Text |
|---|---|
| `Run` | Dialogs (4, lines 27-58) |

### `UI/GuidTools/GuidToolRegenerate.Prompts.cs`

| Method | Text |
|---|---|
| `RegenerateSelectedGuids` | Dialogs (5, lines 42-103), Progress bars (1, line 139) |
| `ReconcileAfterRefresh` | Dialogs (1, line 211) |
| `ConfirmUnusualFiles` | Dialogs (1, line 252) |

### `UI/GuidTools/GuidToolsWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 12) |
| `OnGUI` | Labels (3, lines 22-56), Help boxes (1, line 23), Buttons (4, lines 30-48) |

### `GuidTools/GuidToolBrokenReferenceMonitor.cs`

| Method | Text |
|---|---|
| `PromptOnOpen` | Dialogs (1, line 98) |
| `Prompt` | Dialogs (1, line 119) |

### `GuidTools/GuidToolReferenceRepair.cs`

| Method | Text |
|---|---|
| `Scan` | Progress bars (1, line 196) |
| `FindInHistory` | Progress bars (1, line 952) |

### `GuidTools/GuidToolRegenerate.Patch.cs`

| Method | Text |
|---|---|
| `ApplyPatches` | Progress bars (1, line 130) |

### `GuidTools/GuidToolRegenerate.Scan.cs`

| Method | Text |
|---|---|
| `CachedOffsetsStillMatch` | Progress bars (1, line 143) |

## IconCap

### `UI/IconCap/IconCapEffectStackGUI.cs`

| Method | Text |
|---|---|
| `IconCapEffectStackGUI` | Labels (1, line 27) |
| `Draw` | Buttons (1, line 44) |
| `AddMenu` | Context menu items (1, line 61) |
| `DrawElement` | Tooltips (7, lines 123-187) |

### `UI/IconCap/IconCapNamePrompt.cs`

| Method | Text |
|---|---|
| `Ask` | Window titles (1, line 19) |
| `OnGUI` | Buttons (2, lines 41-44) |

### `UI/IconCap/IconCapWindow.UI.cs`

| Method | Text |
|---|---|
| `DrawTop` | Field labels (2, lines 81-91), Buttons (2, lines 87-92) |
| `DrawPreview` | Help boxes (1, line 106), Field labels (1, line 107) |
| `DrawCaptureStack` | Foldouts (1, line 113) |
| `DrawExport` | Field labels (1, line 121), Buttons (3, lines 122-138) |
| `DrawObjects` | Foldouts (1, line 146), Toggles (1, line 149), Field labels (1, line 171), Buttons (1, line 175) |
| `DrawCamera` | Foldouts (1, line 184), Field labels (4, lines 188-203), Buttons (1, line 204) |
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

### `IconCap/IconCapFiles.cs`

| Method | Text |
|---|---|
| `Run` | Progress bars (1, line 159) |

### `IconCap/IconGenerator.cs`

| Method | Text |
|---|---|
| `Regenerate` | Progress bars (1, line 91) |

## Import Presets

### `UI/ImportPresets/ImportPresetFolderWindow.UI.cs`

| Method | Text |
|---|---|
| `OnGUI` | Field labels (1, line 35), Help boxes (4, lines 59-93), Buttons (3, lines 69-102), Toggles (1, line 85) |
| `DrawTags` | Labels (2, lines 112-122), Toggles (2, lines 116-119), Foldouts (1, line 124), Buttons (2, lines 136-145), Help boxes (1, line 159) |
| `DrawBlenderSettings` | Labels (2, lines 180-192), Field labels (1, line 183), Buttons (1, line 184), Tooltips (1, line 195), Help boxes (1, line 198) |

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
| `OnGUI` | Help boxes (3, lines 52-71), Labels (2, lines 56-57), Field labels (3, lines 62-64), Buttons (3, lines 76-87) |

### `UI/ImportPresets/ImportPresetSaveWindow.cs`

| Method | Text |
|---|---|
| `Save` | Dialogs (1, line 29) |

### `ImportPresets/ImportPresetBetterFBX.cs`

| Method | Text |
|---|---|
| `Run` | Progress bars (1, line 269) |

### `ImportPresets/ImportPresetFolderRules.cs`

| Method | Text |
|---|---|
| `ConfirmApply` | Dialogs (1, line 353) |

## Material Cleaner

### `UI/Utilities/MaterialCleanerWindow.cs`

| Method | Text |
|---|---|
| `Open` | Window titles (1, line 23) |
| `OnGUI` | Field labels (1, line 33), Buttons (5, lines 36-66), Labels (2, lines 48-63) |

## Menu Tool

### `VRChat/UI/MenuTool/MenuToolAddMenuDialog.cs`

| Method | Text |
|---|---|
| `Show` | Window titles (1, line 67) |
| `OnGUI` | Field labels (5, lines 106-129), Buttons (3, lines 120-139), Context menu items (2, lines 123-124), Help boxes (1, line 131) |

### `VRChat/UI/MenuTool/MenuToolManagerTree.cs`

| Method | Text |
|---|---|
| `RowGUI` | Labels (1, line 122) |

### `VRChat/UI/MenuTool/MenuToolWindow.cs`

| Method | Text |
|---|---|
| `MenuToolWindow` | Tabs and toolbars (1, line 22) |
| `Open` | Window titles (1, line 138) |
| `AddMoveItems` | Context menu items (6, lines 345-371) |
| `AddMoveSegment` | Context menu items (2, lines 391-397) |
| `OnRadialContext` | Context menu items (9, lines 412-444) |
| `DrawWindow` | Help boxes (2, lines 525-546) |
| `DrawHeader` | Buttons (2, lines 605-606) |
| `DrawBudget` | Help boxes (1, line 620), Labels (1, line 625) |
| `DrawSummary` | Labels (2, lines 638-667), Help boxes (2, lines 639-647), Buttons (1, line 653), Foldouts (1, line 666) |
| `DrawShared` | Foldouts (1, line 693), Tooltips (1, line 698), Buttons (1, line 705) |
| `DrawBlocks` | Foldouts (1, line 712), Labels (1, line 723), Buttons (1, line 727) |
| `DrawNotes` | Foldouts (1, line 736), Help boxes (1, line 738) |
| `DrawManager` | Help boxes (1, line 754), Buttons (3, lines 760-765), Labels (1, line 770) |
| `DrawPlan` | Labels (2, lines 785-796) |
| `ChangePlan` | Buttons (1, line 818), Labels (2, lines 820-821) |
| `DrawBreadcrumbs` | Labels (1, line 859) |
| `DrawRadial` | Help boxes (1, line 879), Labels (1, line 901) |
| `DrawBits` | Labels (4, lines 928-945), Foldouts (1, line 941) |
| `DrawEntryState` | Buttons (3, lines 957-968) |
| `Discard` | Dialogs (1, line 1043) |
| `ApplyPlan` | Dialogs (2, lines 1061-1076) |

### `VRChat/MenuTool/GestureManager/GestureManagerRadial.cs`

| Method | Text |
|---|---|
| `Draw` | Help boxes (1, line 185) |

### `VRChat/MenuTool/MenuToolBudget.cs`

| Method | Text |
|---|---|
| `Exact` | Progress bars (2, lines 154-156) |

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
| `OnGUI` | Field labels (1, line 34), Help boxes (1, line 36), Buttons (2, lines 41-42) |
| `Save` | Dialogs (1, line 52) |

### `UI/Debug/MenuMockupPreview.cs`

| Method | Text |
|---|---|
| `Create` | Help boxes (1, line 43), Tabs and toolbars (1, line 46) |

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
| `OnGUI` | Labels (2, lines 32-42), Help boxes (2, lines 37-44) |
| `Finish` | Buttons (3, lines 59-68), Dialogs (1, line 70) |

