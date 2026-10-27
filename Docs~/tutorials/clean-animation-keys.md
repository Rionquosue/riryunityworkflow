# Tutorial: Clean Animation Keys

Remove blendshape curves that never move off zero and bone curves that never
move, so clips stay small and do not lock poses by accident.

## 1. Pick the clip

Use whichever is handiest:

- In the **Animation** window, with the clip open, click **Clean keys** at the
  top right.
- Choose **Tools > Rirytools > Clean Animation Keys**. It cleans the clip in the
  focused Animation window, or else the clip selected in the Project window.
- Right-click a clip in the Project window and choose **Tools > Remove blank
  shapekeys**.

Only editable `.anim` files are changed; clips inside FBX files and read-only
packages are refused.

## 2. Read the result

The **Animation cleanup** window shows how many curves were removed and what was
kept. Groups the tool was unsure about are listed with the reason they were
kept, for example a bone that holds a non-default pose.

## 3. Remove kept groups (optional)

If a kept group should go too:

1. Turn on **Remove keys anyways**.
2. Click **Remove whitelisted keys** and confirm.

## 4. Undo

Every removal is one Undo step; press **Ctrl+Z** to bring the curves back.

Reference: [design/clean-animation-keys.md](../design/clean-animation-keys.md)
