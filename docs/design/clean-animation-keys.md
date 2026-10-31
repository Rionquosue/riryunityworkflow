# Clean Animation Keys

Removes blank shapekey curves and static bone curves from animations that key
every bone and shapekey.

## Opening the window

- **Tools > RiryTools > Clean Animation Keys** opens it with an empty list.
- **Assets > RiryTools > Clean Animation Keys** (Project window right-click)
  adds every selected AnimationClip.
- **Clean Keys** in the Animation window adds that window's active clip.

When the window is already open, new clips are added to its list.

## Window

- **Animations**: one row per clip, plus an empty row for the next, up to 64.
  The list scrolls instead of growing the window. Clearing a row removes it.
  **Whitelist** on a row opens the whitelist popup for that clip.
- **Whitelist**: shapekey and bone names that are never removed. A name matches
  in every listed animation, on any mesh or path; a bone name keeps its
  translation, rotation and scale. Typing shows up to 8 fuzzy suggestions from
  the names keyed in the listed animations (names starting with the text, then
  containing it, then containing its letters in order). **Add** adds the typed
  text; **x** removes an entry. The whitelist lasts only while the window is open.
- **Keep** checkboxes, all on by default:
  - **Keep non-zero shapekeys**: a shapekey with any non-zero key (negative
    values deform too).
  - **Keep shapekey tangent motion**: a shapekey whose keys are 0 but whose
    tangents move the value between keys.
  - **Keep moving bones**: a bone translation, rotation or scale whose start
    and finish differ after rounding to three decimal places.
  - **Keep bone mid-keys/tangents**: a bone translation, rotation or scale with
    more than two keyed times, or whose tangents move between matching endpoints.
- **Clean**: removes every group that is not kept from every listed animation,
  as one Undo step, and shows how many curves were removed.

Curves that cannot be read or hold NaN or Infinity are always kept. Other float
curves, object-reference curves and animation events are never touched. Stepped
(infinite) tangents hold the key value.

## Whitelist popup

Lists the shapekeys and bones Clean would remove from that clip with the current
checkboxes and whitelist, under "Only keys that will be removed shown", each with
a checkbox. **APPLY** adds the checked names to the whitelist.

Only editable native `.anim` files are changed; imported and read-only clips are
skipped and counted in the summary. A failed write restores the original curves.
