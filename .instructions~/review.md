# Review: components not necessary for a tool's functionality

Assessed on `betastaging` at `14f8055`. A component is listed when removing it, or folding it into the copy it repeats, changes nothing a tool does. Found by reading the code and by scripted checks (repeated blocks, declarations nothing references); nothing was compiled or run in Unity. What has been removed since is under Removed; everything else listed is still in the package.

Paths are relative to `com.riry.rirytools/Editor/`.

## Never compiled

1. **Menu Tool.** Every `#if RIRYTOOLS_MENUTOOL_MA` block: the whole of `VRChat/MenuTool/MenuToolScanner.ModularAvatar.cs` and 47 blocks in `MenuToolModel.cs` (3), `MenuToolPlan.cs` (4), `MenuToolPlanner.cs` (27), `MenuToolApply.cs` (7), `MenuToolScanner.cs` (5) and `VRChat/UI/MenuTool/MenuToolIcons.cs` (1). The symbol is never defined (`docs/variables.md`), so none of it is in the built tool. Kept on purpose until Modular Avatar support returns.

## Removed

Not compiled or run in Unity.

1. **FTEmulator.** The unused constants `FTEmulatorDiagnostics.ProfileData` and `FTEmulatorDebugWindow.DiagnosticsTab`.
2. **Menu Tool.** `MenuToolApply.cs`: `Move` and `MoveInPlace` share `MoveControl`. `MenuToolPlanner.cs`: three checks share `FindEntry`.
3. **Controller Cleaner, Material Cleaner.** The scene walk and the project search are in `Components/ReferencedAssets.cs`; both `Sources` scripts call it. Both windows' `Scan` use `RiryToolGUI.ProgressEach`.
4. **Blendshape Sync, IconCap.** Scene object ids are in `Components/SceneObjectId.cs`.
5. **Avatar Migration Assistant, Blendshape Sync, Clean Animation Keys.** Blendshape names and the `blendShape.` curve prefix are in `Components/BlendshapeNames.cs`.
6. **Avatar Migration Assistant.** Both runs' `Step` call `AvatarMigrator/AvatarMigratorStep.cs`.
7. **Constraint Tool.** The three tabs' `Act` call `VRChat/Components/ConstraintToolStatus.cs`.
8. **Windows that refresh after Undo and Redo.** `CleanAnimationKeysWindow`, `ImportPresetExtractAnimationWindow`, `MenuMockupEditWindow`, `IconCapWindow`, `ConstraintBezierWindow`, `ConstraintTrainWindow`, `InsertablePropWindow` and `MenuToolWindow` inherit the subscription from `UI/Components/RiryToolUndoWindow.cs`.
9. **FTEmulator.** The `Tests/` folder (`FTEmulatorCoreTests.cs`, `FTEmulatorOscTests.cs`, `FTEmulatorPlayModeTests.cs` and the `Riry.RiryTools.Editor.FTEmulatorTests` assembly), and the `InternalsVisibleTo` that let it reach FTEmulator.
