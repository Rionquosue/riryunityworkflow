# Changelog

## 0.5.0 - 2026-10-24

- Added folder import presets: `ImportPreset.md` names a configuration from the
  shared `Documents/UnityTools/ImportPresets` library, and new files in that
  folder and its subfolders get its import settings on first import.
- Added **Rirytools > Import Preset...** for folders and empty Project window
  space, and **Rirytools > Save Import Settings to Preset Library...** for assets.
- Hand-edited `ImportPreset.md` files and existing import settings are never
  changed; applying to existing files is opt-in.
- `ImportPreset.md` sets a configuration, or one named preset, per file type,
  with `none` to opt a type out.
- `animations:` imports models as Generic first, copies every clip to
  `Animations/`, then applies the model's preset (for example Humanoid).
  **Rirytools > Extract Animations (Generic)** does this for existing models.
- `betterfbx: blend` exports `.blend` files to FBX through Blender and the Better
  FBX add-on; the FBX then follows the folder's FBX rules.
- Exported FBX files and extracted clips are replaced only while unchanged since
  the tool wrote them.
- Added import preset tests, including an end-to-end texture import.

## 0.4.0 - 2026-10-24

- Added **Tools > Rirytools > Constraint Train** (VRChat Avatars SDK 3.7.0+):
  staggered VRC Parent Constraint trains onto 1 to 5 physics chains, built as a
  Motion Time clip.
- Added generated `Phys1_`..`Phys5_` and `Rest_` empty chains with automatic
  endpoints, added without unpacking prefabs.
- Added editable train step and chain blend curves, a halfway-influence power
  curve preset, adaptive curve sampling, and an Animation Mode preview.
- Guarded names that break animation paths (`/`, shadowed sibling names),
  shared targets, targets outside the avatar, nested Animators, read-only clips,
  and NaN curves.
- Added Constraint Train planner and chain tests.

## 0.3.1 - 2026-10-24

- GUID Tools: the reference cache no longer skips uncached files with old
  timestamps, which could leave moved or copied references broken.
- GUID Tools: require saved scenes and a closed Prefab Mode, and reload open
  scenes from disk after a run or rollback.
- GUID Tools: write logs to `Logs/RiryGuidTools`, log one line per matching file,
  and rotate the active log during long runs.
- GUID Tools: binary files that cannot be inspected now report the file instead
  of naming an unrelated selected asset.
- Clean Animation Keys: negative blendshape values and tangents that move a
  curve between keys now keep the curve.
- Blendshape Sync: plain ordinal name matching replaces per-call regexes; only
  Undo/Redo of the tool's own passes stops automatic syncing.
- Added `.meta` files for `package.json`, `README.md`, and `CHANGELOG.md`.
- Added Edit Mode tests for Clean Animation Keys and GUID patching; Blendshape
  Sync tests now run in a temporary scene.
- Removed forced garbage collection after GUID runs.
- GUID Tools: stop symbolic link loops, stop on paths differing only by case, compare paths in NFC Unicode, and
  keep cache file names short for long asset names.
- Blendshape Sync: NaN or Infinity master weights are no longer copied.
- Clean Animation Keys: unreadable curves and non-finite key times are preserved.

## 0.3.0 - 2026-10-06

- Added **Tools > Rirytools > Blendshape Sync** with expanding drag/drop child rows.
- Added per-child manual name links and case-insensitive literal matching.
- Added collision alerts and a Debug button for exact case-sensitive matching.
- Added automatic 0.5-second editor passes, Sync now, and original-weight restore.
- Added window-session baselines, transactional writes, Undo, and prefab overrides.
- Added Play Mode, animation-preview, and Animator graph editing guards.
- Added optional Edit Mode regression tests and usage documentation.

## 0.2.0 - 2026-09-27

- Added batch GUID regeneration with serialized-reference preservation.
- Added recursive Project-folder selection without changing folder GUIDs.
- Added the **Tools > Rirytools > GUID Tools** window and Project context command.
- Added reusable binary reverse-reference indexes under `Library`.
- Added a separate cache-deletion utility.
- Added rotating diagnostic logs with 10 MiB active and 100 MiB archive limits.

## 0.1.0 - 2026-09-27

- Added the Clean Animation Keys editor utility.
- Added context-aware Project and Animation window clip selection.
- Added **Tools > Rirytools > Clean Animation Keys**.
- Added Project-window and Animation-window entry points.

