# File structure

## Existing structure

`~` folders are hidden from Unity and the VPM build. `.meta` files are not listed.

- `.claude~/`, `.instructions~/`: instructions for LLMs.
- `.github/workflows/notify-vpm.yml`: starts the VPM build in `rirytools_vpm` on pushes to `main` and `beta`.
- `docs/`: not shipped. `variables.md` (every key, path, GUID, symbol, menu path), `design/` and `tutorials/` (one per tool).
- `com.riry.rirytools/`: the package. `README.md`, `CHANGELOG.md`; `ProjectFolder~/` is copied into the user's `Assets/RiryTools`.

Code is split into three assemblies by folder, so tools needing an optional SDK only compile with it. A tool's logic is in its own folder (or one file when small); every file that draws UI is in `Editor/UI/` (VRChat tools' in `Editor/UI/VRChat/`, which an `.asmref` keeps in the VRChat assembly).

- `Editor/` (Unity only):
  - `Components/`: code shared by several tools: `RiryLog`, `RiryToolsPaths`, `RiryToolsProjectFolder`, `FuzzyMatch`, `VrcSdkNotFoundException`, `RiryTypes` (type by name), `HierarchyPath` (transform path).
  - `AssemblyInfo` opens internals to the VRChat and Gesture Manager assemblies.
  - Single files: `AttachTo`, `BlendshapeSyncJob(s)`, `CleanAnimationKeys`, `FindNullReferences`, `MenuMockup`, `StopAutoGame`.
  - Folders: `AutoSync/` (Git sync), `AvatarMigrationAssistant/` (one file per step, copy types, `types.yml`), `GuidTools/`, `IconCap/` (with `Shaders/`), `ImportPresets/`; `ConstraintBezier/`, `ConstraintTrain/`, `InsertableProp/` hold Constraint Tool math, here because it needs no SDK.
- `Editor/UI/`: every core window (Avatar Migration Assistant and its dialogs, Blendshape Sync, Broken references, Find Null references, Git, Import Presets and others), `RiryMenus.cs` (all core menu entries and the "VRCSDK not found" stand-ins), `RiryToolsMainMenu.cs` (Main Menu; `Tabs` sets tab order), `RiryGui.cs`.
- `Editor/UI/VRChat/`: `RiryVRChatMenus.cs` (VRChat menu entries and Main Menu hooks); VRChat tool windows: Constraint Tool, Train, Bezier, Insertable prop, Menu Tool (window, trees, Add dialog), Find and Replace.
- `Editor/VRChat/` (needs the VRChat SDK): Insertable prop rig and animator; `AnimatorTools/` (Find and Replace logic); `MenuTool/` (scanner, model, budget, plan, apply, preview, entry state).
- `Editor/VRChat/MenuTool/GestureManager/` (needs Gesture Manager): the radial preview and Play Mode entry state replay, separate so the Menu Tool works without Gesture Manager.
