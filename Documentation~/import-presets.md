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

`ImportPreset.md` names the configuration on a `preset:` line:

```markdown
preset: Textures
```

- It covers its folder and all subfolders. The closest `ImportPreset.md` wins.
- You can write one by hand. The line may wrap the name in backticks, `*` or
  quotes. Configuration names match without regard to case.
- Copy a folder that contains `ImportPreset.md` into the project, and its other
  new files are configured as they import, whatever the import order.
- The dialog only rewrites or removes an `ImportPreset.md` whose text is exactly
  what it generated. One you edited by hand is never touched; the dialog says so
  and offers to open it.

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

`ImportPresetTests` covers marker generation, hand-edit detection and parsing,
nearest-marker lookup, unsafe names, rule ordering and confinement, broken
config files, case-insensitive names, and an end-to-end import: a saved preset
applies to a new texture in a subfolder, and a manual change survives reimport.
The end-to-end test creates and deletes a temporary folder under `Assets`.
