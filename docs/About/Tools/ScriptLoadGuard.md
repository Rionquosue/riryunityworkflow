# Script Load Guard

## 1. Where to find it

| Access | Location | Item |
| --- | --- | --- |
| Menu bar | RiryTools > Script Load Guard (toggle) | |
| Main Menu | Tools > General | Script Load Guard (toggle) |

## 2. Brief description

Stops a scene or prefab that loaded with far fewer objects than its file holds from being saved.

## 3. What the tool can be used for

- Protecting a scene or prefab from being overwritten when a broken package script made it load empty.

## 4. How the tool behaves

- Detects a scene or prefab that loaded with far fewer objects than its file holds.
- Names the script that logged errors during the load.
- Blocks saving it, so the good file is not overwritten, and holds the editor in a window until it is resolved.
- Can be turned off from the menu or the Main Menu's Tools > General sub-tab.

## 5. Flow chart

```mermaid
flowchart TD
    A[Scene or prefab loaded] --> B{Far fewer objects than the file holds?}
    B -- No --> Z[Nothing]
    B -- Yes --> C[Find the script that logged errors during the load]
    C --> D[Block saving this file]
    D --> E[Hold the editor in the guard window until resolved]
```

## 6. References

- Unity's scene loading and save callbacks.
