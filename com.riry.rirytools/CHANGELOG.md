# Changelog

## 0.1.0

First release of RiryTools (`com.riry.rirytools`), editor-only tools for Unity 2022.3 LTS:

- **Attach To**: snaps the selected objects to an avatar's bone, then offers to reparent them, unpacking prefabs only after a warning.
- **AutoSync**: commits every scene and prefab save to the checked-out branch and pushes it, sets up the repository and Git LFS, and blocks saving a scene or prefab that a script stopped from loading.
- **Avatar Migration Assistant**: copies components and settings from an old avatar to a new one and retargets its animations.
- **Blendshape Sync**: copies blendshape weights from a master renderer to child renderers.
- **Clean Animation Keys**: removes blank shapekey curves and static bone curves from up to 64 animations, with a whitelist and keep rules.
- **Constraint Train** (VRChat Avatars SDK 3.7.0 or newer): drives a bone chain onto physics chains with VRC Parent Constraints and a Motion Time clip.
- **GUID Tools**: regenerates asset GUIDs while keeping references, and repairs missing GUID references.
- **IconCap**: captures square PNG icons from the Scene View through an effect stack, with jobs, presets and Icon Generator assets.
- **Import Presets**: per-folder import presets, animation extraction (with an Extract Animation window that can clean keys and extract a model automatically) and Better FBX export.
- **Main Menu**: one dockable window with a tab per tool.
- **Menu Mockup**: lays out an example editor window from blocks and saves it as `.yml`.
- **Stop Auto Game**: keeps focus off the Game view when Play Mode starts.
