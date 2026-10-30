# Changelog

## 0.1.0

First release of RiryTools (`com.riry.rirytools`), editor-only tools for Unity 2022.3 LTS:

- **AutoSync**: commits every scene and prefab save to the checked-out branch and pushes it, sets up the repository and Git LFS, and blocks saving a scene or prefab that a script stopped from loading.
- **Blendshape Sync**: copies blendshape weights from a master renderer to child renderers.
- **Clean Animation Keys**: removes inactive blendshape curves and static bone curves from a clip.
- **Constraint Train** (VRChat Avatars SDK 3.7.0 or newer): drives a bone chain onto physics chains with VRC Parent Constraints and a Motion Time clip.
- **GUID Tools**: regenerates asset GUIDs while keeping references, and repairs missing GUID references.
- **Import Presets**: per-folder import presets, animation extraction and Better FBX export.
- **Play Mode Windows**: keeps the tabs that were in front when Play Mode starts.
