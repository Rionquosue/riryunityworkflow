# File structure

## Existing structure

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.claude~/`, `.instructions~/`: instructions for LLMs.
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `design/` and `tutorials/` (one per tool).
- `com.riry.rirytools/`: the package. `README.md`, `CHANGELOG.md`; `ProjectFolder~/` is copied into the user's `Assets/RiryTools`.

Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it. In each, a tool's logic is in its own folder (or one file when small) and its window in `Editor/UI/`.

- `Editor/` (Unity only):
  - `Components/`: code shared by several tools: `RiryLog`, `RiryToolsPaths`, `RiryToolsProjectFolder`, `FuzzyMatch`, `VrcSdkNotFoundException`, `RiryTypes` (type by name), `HierarchyPath` (transform path).
  - `AssemblyInfo` opens internals to the VRChat and Gesture Manager assemblies.
  - Single files: `AttachTo`, `BlendshapeSync*`, `CleanAnimationKeys`, `FindNullReferences` (with its popup), `MenuMockup`, `StopAutoGame`, `AvatarMigrationAssistant` (steps in its folder).
  - Folders: `AutoSync/` (Git sync), `AvatarMigrationAssistant/` (one file per step, `types.yml`), `GuidTools/`, `IconCap/` (with `Shaders/`), `ImportPresets/`; `ConstraintBezier/`, `ConstraintTrain/`, `InsertableProp/` hold Constraint Tool math, here because it needs no SDK.
- `Editor/UI/`: every core window, `RiryMenus.cs` (all core menu entries and the "VRCSDK not found" stand-ins), `RiryToolsMainMenu.cs` (Main Menu; `Tabs` sets tab order), `RiryGui.cs`.
- `Editor/VRChat/` (needs the VRChat SDK): `RiryVRChatMenus.cs` (VRChat menu entries and Main Menu hooks); Constraint Tool and Insertable prop windows; `AnimatorTools/` (Find and Replace for state behaviours); `MenuTool/` (Parameter budget and Menu manager: window, scanner, model, budget, plan, apply, preview, trees, dialog, entry state).
- `Editor/VRChat/MenuTool/GestureManager/` (needs Gesture Manager): the radial preview and Play Mode entry state replay, separate so the Menu Tool works without Gesture Manager.
