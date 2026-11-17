# Review: components not necessary for a tool's functionality

Assessed on `betastaging` at `14f8055`. A component is listed when removing it, or folding it into the copy it repeats, changes nothing a tool does. Found by reading the code and by scripted checks (repeated blocks, declarations nothing references); nothing was compiled or run in Unity. Nothing listed here has been removed.

Paths are relative to `com.riry.rirytools/Editor/`.

## Never compiled

1. **Menu Tool.** Every `#if RIRYTOOLS_MENUTOOL_MA` block: the whole of `VRChat/MenuTool/MenuToolScanner.ModularAvatar.cs` and 47 blocks in `MenuToolModel.cs` (3), `MenuToolPlan.cs` (4), `MenuToolPlanner.cs` (27), `MenuToolApply.cs` (7), `MenuToolScanner.cs` (5) and `VRChat/UI/MenuTool/MenuToolIcons.cs` (1). The symbol is never defined (`docs/variables.md`), so none of it is in the built tool. Kept on purpose until Modular Avatar support returns.

## Never referenced

1. **FTEmulator.** `FTEmulatorDiagnostics.ProfileData` (`VRChat/FTEmulator/FTEmulatorDiagnostics.cs:33`): a constant only its own glossary names.
2. **FTEmulator.** `FTEmulatorDebugWindow.DiagnosticsTab` (`VRChat/UI/FTEmulator/FTEmulatorDebugWindow.cs:24`): a constant only its own glossary names.

## Repeated code: one copy is enough

1. **Menu Tool.** `VRChat/MenuTool/MenuToolApply.cs`: the last 10 lines of `Move` (204) and of `MoveInPlace` (222) are the same.
2. **Controller Cleaner, Material Cleaner.** `AnimatorTools/ControllerCleanerSources.cs` and `Utilities/MaterialCleanerSources.cs`: `InOpenScenes` walks the open scenes' components and properties the same way, and `InProject` and `Editable` differ only by the asset type.
3. **Controller Cleaner, Material Cleaner.** `UI/AnimatorTools/ControllerCleanerWindow.cs` and `UI/Utilities/MaterialCleanerWindow.cs`: `Scan` is the same apart from the type.
4. **Blendshape Sync, IconCap.** `BlendshapeSync/BlendshapeSyncJob.cs` `Id` and `Find` repeat `IconCapAssets.SceneId` and `Find` (`IconCap/IconCapData.cs`).
5. **Avatar Migration Assistant, Blendshape Sync.** `BlendshapeNames` (`AvatarMigrator/AvatarMigratorCopyRun.TypeRules.cs`) and `GetNames` (`BlendshapeSync/BlendshapeSyncWindow.Plan.cs`) are the same; `ShapeNames` (`AvatarMigrator/AvatarMigratorMatchObjects.cs`) is the same loop into a set.
6. **Avatar Migration Assistant, Clean Animation Keys.** `BlendshapePrefix = "blendShape."` is declared in `AvatarMigrator/AvatarMigratorRetargetAnim.cs` and in `AnimatorTools/CleanAnimationKeys.cs`.
7. **Avatar Migration Assistant.** `Step(name, step)` in `AvatarMigrator/AvatarMigratorCopyRun.cs` (97) and `AvatarMigrator/AvatarMigratorRetargetAnim.cs` (77) is the same apart from where the target root is read.
8. **Constraint Tool.** `Act(action)` in `ConstraintTrainWindow.cs`, `ConstraintBezierWindow.cs` and `InsertablePropWindow.cs` (`VRChat/UI/ConstraintTools/`) is the same apart from the tab name in the log line.
9. **Menu Tool.** `VRChat/MenuTool/MenuToolPlanner.cs`: the two checks at 193 and 226 open with the same 6 lines (find the entry, "is no longer in the menu").
10. **Clean Animation Keys, Extract Animations, Menu Mockup, Constraint Tool.** The `OnEnable` and `OnDisable` pair that subscribes `Undo.undoRedoPerformed` is repeated in `CleanAnimationKeysWindow`, `ImportPresetExtractAnimationWindow`, `MenuMockupEditWindow`, `ConstraintBezierWindow`, `ConstraintTrainWindow` and `InsertablePropWindow`. Each window needs the subscription; the repeated text is what is not needed.

## Not needed for a tool to work, and seen by the user

Removing these changes what the user sees, so each is the owner's decision.

1. **AutoSync.** `Git/Sync` and `Git/Configuration` (`UI/RiryToolsMenuBar.cs`) call the same methods as `Tools/RiryTools/AutoSync/Sync` and `Configuration`.
2. **Menus.** The `&` menu keys in `UI/RiryToolsMenuBar.cs` (`Avatar Migratio&n Assistant`, `Ic&onCap`, `M&enu Tool`, `Constraint &Tool`): every entry opens the same without them.

## Not needed to run the tool: its checks

1. **FTEmulator.** `VRChat/FTEmulator/Tests/` (`FTEmulatorCoreTests.cs`, `FTEmulatorOscTests.cs`, `FTEmulatorPlayModeTests.cs` and their assembly): compiled only with the Unity Test Framework (`RIRYTOOLS_TEST_FRAMEWORK`), and FTEmulator never calls them.
