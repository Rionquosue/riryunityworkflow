# Riry Unity Workflow

Editor-only workflow tools targeting Unity 2022.3 LTS.

## Install

In Unity Package Manager, choose **Add package from git URL** and enter:

```text
https://github.com/Rionquosue/riryunityworkflow.git
```

The private repository must already be accessible through your Git credentials.

## Clean Animation Keys

Run the cleaner in any of these ways:

- Main menu: **Tools > Rirytools > Clean Animation Keys**
- Windows menu navigation: **Alt**, **T**, **R**, **C**
- Project window: right-click a native `.anim` asset, then choose **Tools > Remove blank shapekeys**
- Animation window: click **Clean keys**

The main-menu command uses the active clip from the focused Animation window. If
another window is focused, it uses the selected AnimationClip in the Project
window.

The cleaner removes inactive blendshape curves and static two-endpoint bone
transform curves. It rounds bone translation, rotation, and scale values to
three decimal places for endpoint comparison. Active or ambiguous curves are
preserved in a review window unless **Remove keys anyways** is explicitly
enabled.

Imported and read-only clips are never edited. Changes support Unity Undo.

