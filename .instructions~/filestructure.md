# File structure

How `Rionquosue/riryunityworkflow` is laid out, where each tool lives, and why it is in that folder. `.meta` files sit next to every file and folder in the package and are not listed.

## Repository root

- `.claude~/CLAUDE.md`: instructions for Claude. The `~` keeps Unity and the VPM build from picking the folder up.
- `.instructions~/`: instructions for LLMs (this file, `fixes_and_features.md`, `important_information.md`, `menustructure.md`). Hidden from Unity for the same reason.
- `.github/workflows/notify-vpm.yml`: starts Build Package in `Rionquosue/rirytools_vpm` on every push to `main` (stable) or `beta`, which builds, versions, releases and lists the package. GitHub only reads workflows from this folder.
- `README.md`: the repository's front page.
- `docs/`: documentation, kept outside the package so it is not shipped in the VPM build.
  - `docs/variables.md`: every stored key, path, GUID, symbol, constant, menu path and workflow variable. Always kept current.
  - `docs/design/`: one design note per tool (AutoSync, Blendshape Sync, Clean Animation Keys, Constraint Train, GUID Tools, Import Presets, VPM publishing).
  - `docs/tutorials/`: one how-to per tool, the same tools plus Stop Auto Game.
- `com.riry.rirytools/`: the Unity package itself. Everything here ships in the VPM build.

## Package: `com.riry.rirytools/`

- `README.md`, `CHANGELOG.md`: shown by the VPM listing and Unity's Package Manager.
- `ProjectFolder~/README.md`: copied into the user's project by `RiryToolsProjectFolder.cs`. The `~` keeps Unity from importing it from the package.
- `Editor/`: all code. Editor-only, so nothing ends up in an avatar upload.

The package is split into three assemblies by folder, so a tool that needs an optional SDK only compiles when that SDK is installed:

1. `Editor/` (`Riry.RiryTools.Editor`): tools that need only Unity.
2. `Editor/VRChat/` (`Riry.RiryTools.Editor.VRChat`): tools that need the VRChat SDK (`RIRYTOOLS_VRC_CONSTRAINTS`).
3. `Editor/VRChat/MenuTool/GestureManager/` (`Riry.RiryTools.Editor.GestureManager`): the Menu Tool's bridge to Gesture Manager (`RIRYTOOLS_GESTURE_MANAGER`).

Inside each assembly, a tool's logic sits in its own folder (or a single file at `Editor/` when it is small), and its window sits in `Editor/UI/`. This keeps the logic usable without the GUI and puts all windows in one place.

### `Editor/` (core assembly, Unity only)

- `Riry.RiryTools.Editor.asmdef`, `AssemblyInfo.cs`: the core assembly. `AssemblyInfo.cs` lets the VRChat assembly use its internal members.
- Shared helpers, at the root because every tool uses them:
  - `RiryLog.cs`: logging with the RiryTools prefix.
  - `RiryToolsPaths.cs`: the RiryTools folders in Library, Logs and Documents.
  - `RiryToolsProjectFolder.cs`: `Assets/RiryTools` in the user's project, filled from `ProjectFolder~`.
  - `FuzzyMatch.cs`: name matching used by several search fields.
  - `VrcSdkNotFoundException.cs`: the "VRCSDK not found" message for VRChat tools when the SDK is missing.
- Single-file tools, at the root because they have no helpers of their own:
  - `AttachTo.cs`: Attach To (window in `UI/AttachToWindow.cs`).
  - `BlendshapeSyncJob.cs`, `BlendshapeSyncJobs.cs`, `BlendshapeSyncWindow.cs`: Blendshape Sync (windows in `UI/`).
  - `CleanAnimationKeys.cs`: Clean Animation Keys (window in `UI/CleanAnimationKeysWindow.cs`).
  - `FindNullReferences.cs`: Find Null references, the logic and its small popup in one file.
  - `MenuMockup.cs`: Menu Mockup (windows in `UI/MenuMockupWindows.cs`).
  - `StopAutoGame.cs`: Stop Auto Game, a toggle with no window.
  - `AvatarMigrationAssistant.cs`: the Avatar Migration Assistant's entry point and window; its steps are in the folder of the same name.
- Tool folders, one per tool with several files:
  - `AutoSync/`: AutoSync, the Git sync (`AutoSync`, `AutoSyncJob`, `GitActions`, `GitRemotes`, `GitRunner`, `GitSetup`, `ScriptLoadGuard`). Windows in `UI/GitWindows.cs` and `UI/ScriptLoadFailureWindow.cs`.
  - `AvatarMigrationAssistant/`: one file per migration step (copy, match, correct rotation and scale, repoint references, retarget animations, repack prefabs, upgrade constraints, VRCFury prefabs, report), plus `types.yml`, its component type rules.
  - `ConstraintBezier/`: the Bezier curve math for Constraint Tool's Bezier mode. Kept here, not under `VRChat/`, because the math needs no SDK.
  - `ConstraintTrain/`: Constraint Tool's Train mode planning (chains, planner, prefabs), SDK-free for the same reason.
  - `InsertableProp/`: Insertable prop math, SDK-free for the same reason.
  - `GuidTools/`: GUID Tools (regenerate GUIDs, repair broken references, the reference cache and remap ledger). Windows in `UI/GuidToolsWindow.cs` and `UI/BrokenReferenceWindow.UI.cs`.
  - `IconCap/`: IconCap, the icon renderer (data, effects, files, renderer, generator) and its `Shaders/`. Windows in `UI/IconCapWindow.cs` and `UI/IconCapEffectStackGUI.cs`.
  - `ImportPresets/`: Import Presets (folder rules, preset library, postprocessor, animation extraction, FBX export). Windows in `UI/ImportPresetWindows.cs`, `UI/ImportPresetQuickWindow.cs` and `UI/ExtractAnimationWindow.cs`.

### `Editor/UI/` (core assembly, windows and menus)

- `RiryMenus.cs`: every menu entry of the core tools (Tools, Assets, GameObject and Git menus), plus the "VRCSDK not found" stand-ins for VRChat tools when the SDK is missing.
- `RiryToolsMainMenu.cs`: the Main Menu window, a tab per tool. Its `Tabs` array sets the tab order.
- `RiryGui.cs`: shared GUI helpers.
- One window file per tool, as listed above.

### `Editor/VRChat/` (VRChat SDK assembly)

- `Riry.RiryTools.Editor.VRChat.asmdef`, `AssemblyInfo.cs`: the VRChat assembly; compiles only with the VRChat SDK.
- `RiryVRChatMenus.cs`: menu entries for the VRChat tools, and the hooks that put them into the Main Menu.
- Constraint Tool: `ConstraintToolWindow.cs` (the window with its Train, Bezier and Insertable prop tabs), `ConstraintTrainWindow(.UI).cs`, `ConstraintBezierWindow(.UI).cs`, and Insertable prop's `InsertablePropWindow(.UI).cs`, `InsertablePropAnimator.cs`, `InsertablePropRig.cs`. Here because they create VRChat constraints and components.
- `AnimatorTools/BehaviourFindReplaceWindow.cs`: Animator Tools > Find and Replace. Here because it edits VRChat state behaviours (Play Audio, Parameter Driver).
- `MenuTool/`: the Menu Tool (Parameter budget and Menu manager), here because it reads VRChat menus and parameters.
  - `MenuToolWindow.cs`: the window.
  - `MenuToolScanner.cs`: builds the assembled menu from the descriptor, VRCFury and Modular Avatar.
  - `MenuToolModel.cs`: the scan's data types.
  - `MenuToolBudget.cs`: parameter cost.
  - `MenuToolPlan.cs`: the planned moves and their checks.
  - `MenuToolApply.cs`: applies the plan to its sources.
  - `MenuToolPreview.cs`: the radial preview interface and its menus.
  - `MenuToolTrees.cs`: the tree views.
  - `MenuToolAddMenuDialog.cs`: the Add menu and Add New Control dialog.
  - `MenuToolEntryState.cs`: the saved entry state.
  - `GestureManager/`: the Gesture Manager bridge, its own assembly so the Menu Tool still works without Gesture Manager:
    - `GestureManagerRadial.cs`: the radial preview.
    - `GestureManagerEntryState.cs`: the Play Mode entry state replay.
