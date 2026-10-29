# Blendshape Sync

Target: Unity 2022.3 LTS, including 2022.3.22f1. This is an Editor tool.

## Use

1. Open **Tools > Rirytools > Blendshape Sync**.
2. Drag the master scene object (or its SkinnedMeshRenderer) into **Master**.
3. Drag child renderers into the child fields. Assigning the last field adds
   another empty drop row. Each child must be a different renderer.
4. Click **Sync now**, or enable **Run automatically** for a pass every 0.5 seconds.
5. Click **Set all to original** to restore all cached blendshape weights on the
   master and children, including unmatched shapes and children removed from
   the list. This also stops automatic syncing.

The tool synchronizes **weights**, using blendshapes already on each mesh. It
does not copy blendshape geometry, change mesh assets, or create animation keys.
Scene objects and Prefab Mode objects are supported; prefab/model assets dragged
directly from the Project window are rejected.

## Manual links and case matching

Expand **Manual link** underneath a child. Enter the master's blendshape name
in **Source**, and that child's blendshape name in **Destination**. Another blank
link row appears automatically. Links apply to that child only.

Manual links override automatic matching for their source and destination. You
can explicitly link one source to several destinations. Two manual links cannot
write the same destination. Incomplete links or missing names block the entire
pass and show a message; a fully blank row is ignored. Unmatched automatic names
are ignored, leaving those weights unchanged.

Names are literal, complete names. Case-insensitive matching uses an ordinal,
culture-independent comparison of the whole name. Regex punctuation in names
such as `Blink.Left` or `Smile[Wide]` has no special meaning. Spaces remain part
of the name. There is no substring, suffix, or arbitrary-pattern matching.

The **Debug** button disables case-insensitive filtering and requires exact names.
If either mesh contains several relevant names that match when case is ignored,
the tool blocks the pass and offers:

> Multiple matches found without case sensitivity. Would you like to disable?

Choose **Disable filtering** to enable Debug, or keep filtering and correct the
names/links. A blocked pass never partially updates other children.

## Original cache and Undo

Each renderer's entire weight array is captured when first assigned, before any
sync writes. A newly encountered mesh is captured before its first write. The
baseline is not overwritten by later sync passes, and survives script assembly
reloads while the window remains open. Removed children remain in the cache.

The cache belongs to this window session. Closing the window discards the cache
and keeps the current scene values. Reopen it to start a new baseline. There is
no disk cache or runtime component. Save the scene/prefab to retain synced values.

Restoring checks the renderer, mesh identity, shape count, and exact shape order.
Snapshots for deleted objects, different meshes, or reordered/renamed shapes are
skipped and counted in the result. In-place mesh reimports do not overwrite the
old baseline; mismatched snapshots are skipped to avoid restoring by wrong index.

A master weight that is NaN or Infinity is never copied; the result message
counts those skipped links.

Each pass with actual weight changes is one Undo operation. No-op ticks add no
Undo entries. Undo/Redo of a sync or restore pass stops automatic syncing so it
cannot immediately overwrite the Undo result. Undoing any other edit, such as a
master weight change, leaves automatic syncing on. Prefab instance modifications
are recorded normally.

## Editor guards and scheduling

Both sync and restore are blocked while:

- Entering or leaving Play Mode, or in Play Mode.
- The editor is compiling scripts or importing assets.
- Unity AnimationMode is active, including scene animation preview/recording.
- An open Animation window is previewing, playing, or recording.
- The Animator graph has focus or the pointer is over it, or an Animator state,
  state machine, or transition is selected for editing in the Inspector.

Preview/graph pauses lift when that activity ends. Entering Play Mode unchecks
**Run automatically**; enable it again after returning to Edit Mode. To clear a
graph-selection pause, select a normal scene object.

Scheduling uses `EditorApplication.update` and elapsed editor time. Unity does
not guarantee a fixed update rate: a busy editor can delay a pass, and missed
passes are skipped. No catch-up bursts or concurrent passes run. Each pass reads
one master-weight snapshot, validates every child's links before writing, and
applies all changes synchronously on the editor thread. A write failure attempts
to roll the entire pass back. Large mesh sets can still take editor time; atomic
updates cannot be split across frames without exposing partial results.

Name mappings are rebuilt when assignments/links/settings change, after asset
changes/Undo, or when a mesh reference, shape count, or shape names/order change.
Regular ticks reuse them after a quick check of the current names.
Closing/reloading the window removes its editor event subscriptions.

## Unity API references

- [AnimationMode.InAnimationMode](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AnimationMode.InAnimationMode.html)
- [AnimationWindow preview/playback/recording properties](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AnimationWindow.html)
- [EditorApplication.update scheduling](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorApplication-update.html)
