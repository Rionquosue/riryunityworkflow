# Clean Animation Keys

## Invocation context

The command intentionally chooses its source by invocation context:

- The Animation-window button always uses that window's active clip.
- The Project-window context menu always uses `Selection.activeObject`.
- The main-menu command uses the focused Animation window when possible;
  otherwise it uses the selected Project-window AnimationClip.

## Cleanup rules

- A blendshape group is removable when every key value is exactly zero and no
  tangent moves the curve away from zero between keys. Negative values are
  active. Stepped (infinite) tangents hold the key value.
- A bone translation, rotation, or scale group is removable when it contains no
  more than two keyed times, its rounded start and finish values match, and no
  tangent bends the curve between keys. A static hold is removed even when it
  holds a non-default pose; review the result window if a clip relies on that.
- Bone values are rounded to three decimal places before comparison.
- NaN and Infinity values or key times, and curves Unity cannot read, are
  preserved for safety.
- Other float curves, object-reference curves, and animation events are parsed
  for the result summary and left untouched.
- Whitelisted groups remain intact unless the user enables
  **Remove keys anyways** and confirms removal.

`CleanAnimationKeysTests` covers these rules with in-memory clips.

Only editable native `.anim` files are changed. Every removal is registered with
Unity Undo, and a failed batch write attempts to restore its original curves.

