# Import Presets

Target: Unity 2022.3 LTS. Applies saved import settings to new files by folder,
using presets stored outside the project so every project can share them.

## The library

Presets live in a project-independent folder:

- Windows: `%USERPROFILE%\Documents\RiryTools\ImportPresets` (your Documents
  folder, including a OneDrive-redirected one)
- Linux and macOS: `~/Documents/RiryTools/ImportPresets`

Each subfolder is one **configuration**, for example `Textures` or
`Avatar FBX`. It holds Unity `.preset` files and an optional `config.json`:

```json
{
  "rules": [
    { "extensions": ["png", "tga"], "preset": "Texture png.preset" },
    { "extensions": ["fbx"], "preset": "Model fbx.preset" }
  ]
}
```

- For a new file, rules that list its extension (or `"*"`) are tried in order,
  then every preset no rule mentions. The first preset that suits the file's
  importer is applied.
- A rule naming a missing preset is ignored. A rule whose preset is for another
  importer is skipped with a Console warning.
- Open the folder with **Tools > RiryTools > Import Presets > Open Import Preset Library**. It is
  created on first use.

### Adding presets

Right-click an asset whose import settings you want, then choose
**RiryTools > Save Import Preset**. Pick or name a
configuration and a preset name, and click **Save**; a missing or invalid name
is reported only then. The preset applies to every file of its type (every file
that importer handles). Saving again under the same name replaces the preset and
removes any `config.json` rule for it. Existing Unity `.preset` assets can also be copied into a configuration
folder by hand.

## Choosing a folder's presets

Presets are chosen per **file type**: everything one Unity importer reads, so one
preset fits all of it. **Image files** covers every texture, **Model files** every
model (including `.blend`), and likewise **Audio files**, **Video files** and
**Font files**; any other importer shows as `<Type> files`. Scripts and files Unity
does not import are not listed.

Decorators make file types of their own, each holding every file in the folder
whose name contains it (any case), whatever its importer:

- **Tags:** the words listed under **Tags** in the window, saved for every project
  in `Documents/RiryTools/ImportPresets/preferences.yml`. A tag is listed as
  **normal files**. The file starts with the PBR map names Substance Painter and
  similar programs export: `basecolor`, `basemap`, `albedotransparency`, `diffuse`,
  `metallic`, `roughness`, `specular`, `glossiness`, `metallicsmoothness`,
  `specularsmoothness`, `smoothness`, `maskmap`, `normal`, `height`, `mixed_ao`,
  `occlusion`, `emissive`, `emission`, `opacity`. Tags hold letters, digits and `_`.
- **Endings:** every `_word` ending of a file name in the folder (`Rock_custom.png`
  gives `_custom`; number-only endings such as `_01` are ignored), listed as
  **_custom files**. An ending whose word is a tag counts as the tag.

A name containing several uses the one that ends last, the longer when two end
together (`Body_MetallicSmoothness.png` is a **metallicsmoothness file**, not a
**smoothness file**). A decorated type set to **None** gives its files the plain
type's preset.

**Tools > RiryTools > Import Presets > Set Folder Import Presets** opens a window
for one folder:

- **Folder**: a fuzzy search field over every folder in `Assets` (up to 8
  suggestions, ranked by name or path). It starts on the folder shown in the
  Project window.
- One dropdown per file type directly in that folder, listing **None** and every
  library preset that fits that type, as `Configuration/Preset`.
- **Export .blend with Better FBX**, only when the folder holds `.blend` files,
  with the Blender settings while it is on.
- **Apply** and **Close**. The list is found again after a domain reload or an
  import, keeping the choices.

Right-clicking one or more folders, or empty space in a folder's contents, and
choosing **RiryTools > Set Folder Import Presets** opens a small popup instead.
**Apply to all** (on by default) gives one dropdown for every folder; turned off,
each folder gets its own. Each dropdown lists **None** and every configuration;
choosing one sets each file type to that configuration's preset for it. Its
**File types** foldout gives each file type in its folders its own dropdown.
**Abort** closes the popup. **RiryTools > Extract Animation** in the same menu is
enabled only for a model with embedded animations.

**Apply**, in both, writes the folder's `ImportPreset.md` and applies each changed
preset to the files of that type already in the folder, without Undo. A type set
to **None**, or left unchanged, keeps its files' settings and nothing is
reimported. Presets of file types not in the folder stay in `ImportPreset.md`.

## ImportPreset.md

The tool writes `ImportPreset.md`; you can also write it by hand:

```markdown
Image files: Textures/Texture png
Model files: Avatar/Humanoid
betterfbx: blend
```

- `<file type>: Configuration/Preset` gives that type one preset, for example
  `_normal files: Textures/Normal map` for a decorator.
  `Configuration` alone lets the configuration's rules pick the preset.
- `betterfbx: blend` exports `.blend` files with Better FBX (see below).
- It covers only the files directly in its folder, never its subfolders.
- Rule lines are `name: value`. Headings, list items (`- ...`), HTML comments and
  other text are ignored. Values may be wrapped in backticks, `*` or quotes, and
  file types and configuration names match without regard to case.
- Copy a folder that contains `ImportPreset.md` into the project, and its other
  new files are configured as they import, whatever the import order.
- The tool only rewrites or removes an `ImportPreset.md` whose text is exactly
  what it generates. One you edited by hand is never touched; the window says so
  and offers to open it.

## Extracting animations before Humanoid

With **Automatically extract animations** on (Extract Animation window), a new
FBX is first imported with Unity's default (Generic) rig. Every clip is copied to
`Animations/` beside the FBX as
`<model>_<clip>.anim`, keeping plain bone curves with no Humanoid retargeting.
Then the FBX's preset (for example a Humanoid avatar preset) is applied and the
FBX is reimported. There is one FBX, not a copy.

- The copies are ordinary clips; edit or move them freely.
- **RiryTools > Extract Animation** opens a window for one model. **Extract**
  does the same later: it imports as Generic temporarily, copies the clips, and
  restores the model's own settings.
  - **Target**: the model, filled from the Project window selection.
  - **Destination**: the folder for the clips, `Animations/` beside the model by default.
  - **Automatically clean keys**: cleans the copies with Clean Animation Keys.
    **Configure** opens Clean Animation Keys for these settings, listing the
    clips already in Destination.
  - **Save animation extract settings** (only while cleaning): keeps the
    cleaning settings, so automatic extraction cleans too.
  - **Automatically extract animations**: the model is always extracted before
    its preset is applied, on its first import and by **Apply** in Set Folder
    Import Presets.
- The window's settings are saved to `.<model>_ani-extract.json~` beside the
  model, only while Automatically extract animations or Save animation extract
  settings is on, and the file is removed otherwise. Unity skips names that start
  with `.` or end with `~`, so it gets no `.meta`; it is hidden on Windows. In a
  Git repository it is staged with `git add --force`, and Sync commits it when on.
- A clip the tool wrote is refreshed automatically only while unchanged. One you
  edited, or one it did not write, is kept and listed in the Console; **Extract**
  asks before replacing those.
- Pending extractions are stored in `Library/RiryTools/ImportPresets`, so they resume
  after a script reload.

## Better FBX for .blend files

With `betterfbx: blend`, importing or changing a `.blend` runs Blender in the
background with the Better FBX add-on (`bpy.ops.better_export.fbx`) and writes
`<name>.fbx` beside it. That FBX is a normal new file: the `Model files` line
applies to it, while the `.blend` itself gets no preset. When the `.blend`
changes, the FBX is exported again and keeps its import settings; with
Automatically extract animations on for the FBX, its clips are refreshed. Turning
Better FBX on with **Apply** exports the folder's `.blend` files at once.

- Better FBX is a paid Blender extension (Blender 4.2 or newer). Install and
  enable it in Blender and save Blender's preferences; background exports load
  saved preferences.
- The call matches Better FBX 6.3.3: `bpy.ops.better_export.fbx(filepath=...)`
  with the add-on's defaults, except `my_animation_type='Actions'`, so every
  action becomes its own take (the add-on's default exports only the active
  one). The default axis, `MayaZUp`, is kept because the add-on warns that its
  `Unity` axis is not compatible with Humanoid.
- The exporter only runs in Object Mode, so a `.blend` saved in Edit or Pose
  Mode is switched to Object Mode in the background copy first; the `.blend`
  itself is not saved.
- **Blender** is detected (`Program Files\Blender Foundation` on Windows, the
  Applications folder on macOS, `PATH`, `/snap/bin` and `/usr/bin` on Linux) or set
  in the dialog's Better FBX section. **Exporter options** passes extra
  `name=value` arguments that override those defaults; they are checked as
  plain values and never run as code. Both are saved per computer. Useful ones:

  | Option | Values (default first) |
  | --- | --- |
  | `my_animation_type` | `'Actions'` here, `'Active'`, `'Tracks'` (NLA tracks) |
  | `my_fbx_unit` | `'cm'`, `'m'`, `'mm'`, ... |
  | `my_fbx_axis` | `'MayaZUp'`, `'OpenGL'` (`'Unity'` breaks Humanoid) |
  | `use_selection`, `use_visible` | `False` |
  | `use_only_deform_bones` | `False` |
  | `use_triangulate` | `False` |
  | `use_apply_modifiers` | `True` |
  | `use_embed_media` | `False` |
  | `my_max_bone_influences` | `'Unlimited'`, `'4'`, ... |

  An unknown option or value stops the export and names the problem.
- An existing FBX is replaced only when the tool exported it and it is unchanged
  since. A failed export restores the previous FBX, and Unity does not import a
  half-written file. Exports can be cancelled from the progress bar and time out
  after 10 minutes.
- Presets are not applied to the `.blend` itself. Unity still imports the
  `.blend` with its own Blender conversion, so Blender must be installed.
- Only `.blend` files are exported; other types listed in `betterfbx` are ignored.

## What is never changed

- Presets apply only on a file's **first import**, when it has no import
  settings yet. Reimports, including after you change settings by hand, keep
  your settings.
- Files that arrive with their own `.meta` (for example from a `.unitypackage`)
  keep the settings in that `.meta`.
- Unity's Preset Manager defaults are applied first; a folder preset then
  overrides them.

## Names and limits

- Configuration and preset names cannot contain `/ \ :` or other characters that
  are invalid in file names, cannot be `.` or `..`, start or end with a space, end
  with a period, or be a reserved Windows device name such as `CON` or `COM1`.
  Configuration folders with such names are not listed.
- A rule cannot point outside its configuration folder.
- An invalid `config.json` is reported once; presets then apply by importer type
  only.
- A preset that cannot be loaded (for example, made by a newer Unity) is reported
  once and skipped.
- Model presets can reference materials or avatars by asset GUID. Those
  references are empty in projects that do not contain the same assets.
- `ImportPreset.md` folders in installed (read-only) packages cannot be set.

