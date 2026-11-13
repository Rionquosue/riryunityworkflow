# Bugcheck procedures

## Before every prep to push into a mainline release perform the following bugchecks:

    Ctrl-Z bugs (Z)

    1. All actions applied in one user button press or confirmation should be undone in a single CTRL-Z press.
    2. Actions should not grab or overwrite any other action's CTRL-Z, if it is impossible for that action to not break the CTRL-Z history, do not perform that action without a warning popup.
    3. Anything such as files that cannot be undone should be listed in the log to provide users the ability to remove them if desired.

    UI Bugs (U)

    1. Rects that collide or UI elements that draw over eachother. Example being an input field that draws over a control field below it.
    2. Fixed width boxes that will not re-order correctly when the user resizes a window. 
    3. Long text or handles that crowd neighbours
    4. Code comments that have made its way into the UI such as "No X is applied until Y process is done", they cause clutter and do not clarify to the user.
    5. Overlays against IMGUI, such as a radial overlapping an emulated menu. Give IMGUI elements such as the radial menu a fixed size box and do not draw into it.

    Destructive Bugs (D)
    1. Anything that can overwrite or delete something that is not part of the purposeful action of the tool.
    2. Edits to a shared asset that another avatar, prefab, or closed scene uses.
    3. Edits to a prefab or asset that Modular Avatar or VRCFury use.
    4. Wrong keys, such as identifying an avatar by name instead of GUID.
    5. Data loss on apply, anything irreversible.

    Log issues (L)
    1. Any action that does not generate a log
    2. Any error that does not generate a log that can be traced back to the exact script that errored.
    3. Excessive log entries from a single action, such as turning on blendshape sync generating a log entry every time it executes a sync.
    

## Perform the following resolution standards:

    (Z)
        For Z bugs, either combine the scripts actions into a single call or edit the data layers once it has been complete so that a single press can undo the whole action taken instead of one action requiring 4 CTRL-Z presses to fully undo.

    (U)
        For U bugs, adhere to standard flexbox and tab based UI design conventions. Give every UI element a little bit of buffer, use collapsing boxes where applicable, and ensure the UI will have all elements available to the user. If a tool requires fixed width and the Main menu cannot accommodate, it is recommended to move that tool into a popup and categorize it appropriately per menustructure.md

    (D)
        Any destructive bugs should be fixed. Prefabs should never be unpacked, a vrcfury prefab should not be directly edited but a new child prefab made from that prefab where our edits are applied, or apply edits as scene overrides instead. If a destructive edit is inevitable due to the nature of a tool, before the script executes a warning should be issued. If GIT is enabled, check that a previous version of that file is cached and notify the user they can use GIT to roll back to that version but CTRL-Z will not function as they expect.

    (L)
        Any tool executing should generate a single log entry for the script execution, and completion with a summary of what was changed. If an error can be generated without creating a log entry treat that as a critical bug and immediately rectify so that it can be traced back. Any tool should only generate execution and completion, repeating job scripts such as git sync and blendshape sync should only generate once they are turned on or when an exception has occurred. This avoids the log being overcrowded with non contextual information.

## If none of the above can be applied

Append to the end of this file below the break line with what the bug is, and recommended fixes.


---