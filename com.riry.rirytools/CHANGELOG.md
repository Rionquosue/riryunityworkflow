# Changelog

## 0.7.0 - 2026-10-26

- Renamed the package to `com.riry.rirytools` (RiryTools). Assemblies and
  namespaces are now `Riry.RiryTools.Editor`, `Riry.RiryTools.Editor.VRChat` and
  `Riry.RiryTools.Editor.Tests`. Saved settings keep their old keys, so nothing
  is lost.
- Added `Assets/RiryTools`: files in the package's `ProjectFolder~` are unpacked
  there on load without overwriting existing ones. Constraint Train clips and
  `_ROOT` variants of read-only prefabs are saved there by default.
- Without the VRChat Avatars SDK 3.7.0+, the Constraint Train menu entries now
  throw `VrcSdkNotFoundException` and show a **VRCSDK not found** popup instead
  of being missing.
- Moved the design documents from `Documentation~` to `Docs~/design`, and added a
  tutorial for every tool in `Docs~/tutorials`. `Docs~` is not part of the
  package in a project.
- Added VPM publishing: Rionquosue/rirytools_vpm checks `main` hourly, builds
  and releases new versions, and lists them for the VRChat Creator Companion
  after the owner approves the deployment.
- Added `TestProject~`, a minimal 2022.3 project that loads the package from the
  repository and runs its tests.

## 0.6.0 - 2026-10-25

- Moved all UI into `Editor/UI/`: every menu entry is in `RiryMenus.cs` (VRChat
  entries in `Editor/VRChat/RiryVRChatMenus.cs`), and each window's drawing code
  is in its own file with a glossary of its fields and methods. No behaviour change.
- Added the **GIT** menu: **AutoSync Scene∕Prefab** (on by default in a Git
  repository), **Sync Now**, **Check for Broken References...**, **Configure
  Sync...**, **AutoSync Settings...** and **Help**.
- AutoSync commits scenes, prefabs and `.meta` files to the `AutoSync` branch (or
  a chosen one) shortly after they change, through a separate Git index, and
  pushes it. The checked-out branch and staged changes are never touched.
- **Configure Sync** creates the repository from named `.gitignore` and
  `.gitattributes` templates kept with the import presets (GitHub's
  Unity.gitignore, the gitattributes project's Unity.gitattributes and a built-in
  LFS template when there are none), enables Git
  LFS, lists large files LFS does not store, tests an LFS upload, and warns that
  HTTPS remotes behind Cloudflare must use SSH instead.
- The repository is always the whole project folder; a project inside another
  repository is refused until Configure Sync creates one in the project folder.
- Git actions throw `GitNotInstalledException` when Git is missing.
- Added a GitHub Actions workflow that keeps AutoSync branches rebased on the
  base branch; AutoSync versions win conflicts.
- Regenerate GUID records every change in `ProjectSettings/RiryGuidRemap.tsv`.
- Added the Broken References window: redirects for missing GUIDs from the
  ledger, logs and Git history, chosen manually where none is found. AutoSync
  offers it when a scene or prefab opens with broken references.
- Added **SCRIPT CAUSING FAILED LOAD**: a scene or prefab that loads with
  far fewer objects than its file holds cannot be saved, and a modal window names
  the suspected script until the load is resolved.
- Added AutoSync, LFS, GUID repair and load-guard tests.

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
  FBX extension (checked against 6.3.3): every action is exported, Edit or Pose
  Mode files are switched to Object Mode, and failures are reported; the FBX
  then follows the folder's FBX rules.
- Exported FBX files and extracted clips are replaced only while unchanged since
  the tool wrote them.
- Added import preset tests, including an end-to-end texture import.
- Added **Tools > Rirytools > Play Mode**: entering Play Mode keeps the tabs that
  were in front, and a Game view opened only for Play Mode can be closed.

## 0.4.0 - 2026-10-24

- Added **Tools > Rirytools > Constraint Train** (VRChat Avatars SDK 3.7.0+):
  staggered VRC Parent Constraint trains onto 1 to 5 physics chains, built as a
  Motion Time clip. Also in the Hierarchy's right-click menu under **Rirytools**,
  where the selected bones fill in the chain start and end.
- Chains and constraints on a prefab instance can be saved as overrides to the
  source prefab, or, for an FBX or read-only prefab, to a new `<name>_ROOT`
  Prefab Variant beside it that the scene object is connected to.
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

