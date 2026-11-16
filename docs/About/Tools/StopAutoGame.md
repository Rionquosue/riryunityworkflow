# Stop Auto Game

## 1. Brief description

Keeps entering Play Mode from switching focus to the Game view.

## 2. What the tool can be used for

- Staying in the Scene view or another window when entering Play Mode.

## 3. How the tool behaves

- On by default, per user, for every project on the computer.
- About a tenth of a second after Play Mode starts, the windows that had focus get it back from the Game view.
- If Play Mode opened a Game view because the layout had none, it is closed when Play Mode ends, unless it was one of the windows that had focus.

## 4. Flow chart

```mermaid
flowchart TD
    A[Play Mode starts] --> B{On?}
    B -- No --> Z[Nothing]
    B -- Yes --> C[Note focused windows]
    C --> D[After about 0.1 s give them focus back]
    D --> E[Play Mode ends]
    E --> F{Game view opened by Play Mode?}
    F -- Yes --> G[Close it]
```

## 5. References

- Unity's `EditorApplication.playModeStateChanged`.
