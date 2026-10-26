# Import Presets

Target: Unity 2022.3 LTS. Applies saved import settings to new files by folder,
using presets stored outside the project so every project can share them.

## The library

Presets live in a project-independent folder:

- Windows: `%USERPROFILE%\Documents\UnityTools\ImportPresets` (your Documents
  folder, including a OneDrive-redirected one)
- Linux and macOS: `~/Documents/UnityTools/ImportPresets`

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
- Open the folder with **Tools > Rirytools > Open Import Preset Library**. It is
  created on first use, with a `README.txt`.

### Adding presets

Right-click an asset whose import settings you want, then choose
**Rirytools > Save Import Settings to Preset Library...**. Pick or name a
configuration and a preset name. With **Use only for .ext files** on, a rule
limits the preset to that extension; otherwise it applies to every file that
importer handles. Saving again under the same name replaces the preset and its
rule. Existing Unity `.preset` assets can also be copied into a configuration
folder by hand.

## Choosing a folder's preset

Right-click a folder, or empty space in a folder's contents, then choose
**Rirytools > Import Preset...**. Right-clicking a file uses the file's folder.

The dialog shows each folder's current state, lists the configurations, and
summarizes the chosen one's rules. **Apply** writes `ImportPreset.md` into the
folder; **None** removes it. **Also apply to files already in the folder now**
replaces the current settings of existing files (after confirmation, without
Undo); subfolders with their own `ImportPreset.md` are skipped.

## ImportPreset.md

The dialog writes `ImportPreset.md`; you can also write it by hand:

```markdown
preset: Textures
fbx, obj: Avatar
png, tga, jpg, jpeg: Textures/Texture png
blend: none
animations: fbx
betterfbx: blend
```

- `preset:` is the configuration for file types not listed on their own line.
- `ext, ext: Configuration` gives those types a configuration (its rules then pick
  the preset). `Configuration/Preset` names one preset in it. `none` turns
  presets off for those types.
- `animations:` lists model types whose clips are extracted before their preset
  applies (see below).
- `betterfbx: blend` exports `.blend` files with Better FBX (see below).
- It covers its folder and all subfolders. The closest `ImportPreset.md` wins.
- Rule lines are `name: value`. Headings, list items (`- ...`), HTML comments and
  other text are ignored. Names may be wrapped in backticks, `*` or quotes, and
  extensions and configuration names match without regard to case.
- Copy a folder that contains `ImportPreset.md` into the project, and its other
  new files are configured as they import, whatever the import order.
- The dialog only rewrites or removes an `ImportPreset.md` whose text is exactly
  what it generates. One you edited by hand is never touched; the dialog says so
  and offers to open it.

In the dialog, each **file type** row has extensions, a configuration (or
**Default** or **None**), an optional single preset, and the animation and
Better FBX options. **+ Textures**, **+ Models** and **+ Blender** add common rows.

## Extracting animations before Humanoid

With `animations: fbx`, a new FBX is first imported with Unity's default
(Generic) rig. Every clip is copied to `Animations/` beside the FBX as
`<model>_<clip>.anim`, keeping plain bone curves with no Humanoid retargeting.
Then the FBX's preset (for example a Humanoid avatar preset) is applied and the
FBX is reimported. There is one FBX, not a copy.

- The copies are ordinary clips; edit or move them freely.
- **Rirytools > Extract Animations (Generic)** on any model does the same later:
  it imports as Generic temporarily, copies the clips, and restores the model's
  own settings.
- A clip the tool wrote is refreshed automatically only while unchanged. One you
  edited, or one it did not write, is kept and listed in the Console; the menu
  command asks before replacing those.
- Pending extractions are stored in `Library/RiryImportPresets`, so they resume
  after a script reload.

## Better FBX for .blend files

With `betterfbx: blend`, importing or changing a `.blend` runs Blender in the
background with the Better FBX add-on (`bpy.ops.better_export.fbx`) and writes
`<name>.fbx` beside it. That FBX is a normal new file: the `fbx` line (or the
default), and `animations: fbx`, apply to it. When the `.blend` changes, the FBX is
exported again and keeps its import settings; with `animations: fbx`, its clips
are refreshed.

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

## Tests

`ImportPresetTests` covers marker generation and per-type rules, hand-edit
detection, parsing of handwritten files, nearest-marker lookup, unsafe names,
rule ordering and confinement, broken config files, case-insensitive names,
Blender argument quoting and option checks, clip file names, owned-file
tracking, and an end-to-end import: a named preset applies to a new texture in a
subfolder, and a manual change survives reimport. Animation extraction and
Better FBX exports need real model files and Blender, so check those by hand.
The end-to-end test creates and deletes a temporary folder under `Assets`.
