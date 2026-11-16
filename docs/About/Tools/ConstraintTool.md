# Constraint Tool

## 1. Brief description

One window with a tab per subtool, all built on VRChat constraints: Train, Bezier and Insertable prop.

## 2. What the tool can be used for

- **Train:** animating a bone chain (a tail, ears, hair) so it slides onto physics-driven copies of itself, one bone after another, driven by a single float.
- **Bezier:** bending a bone chain along a curve by moving two handles.
- **Insertable prop:** a prop that can be drawn from, dropped from and put back into a socket (a dagger and sheath, for example).

## 3. How the tool behaves

**Train**
- Opened from the Hierarchy's right-click **RiryTools > Constraint Tool > Train** on the first bone (Ctrl/Cmd-click the end bone too), or the Main Menu's Constraint Tool tab.
- The real chain runs from the start bone to the end bone. The end bone is where the train stops; the bones before it are constrained.
- One **Start position** object is where every bone sits before the train reaches it.
- 1 to 5 physics chains (`Phys1_`, `Phys2_`...) can be generated beside the chain, each matching the real chain bone for bone.
- **Set up constraints** adds a VRC Parent Constraint to each moving bone, switched off, with every source at 0, at-rest values from the bone's pose, and the bones left at rest. One undo step.
- **Build animation** writes a Motion Time clip: each bone is switched on, moves from the Start position along physics chain 1 one bone per step (the tip leads, each bone toward the root one step later) and stops one chain bone past its own, so the last stops at the end bone's chain bone; then every bone blends to chain 2, chain 3 and so on. Train end, chain ends and two curves shape the timing.
- On a prefab instance, the changes can be saved as overrides, or to a `<name>_ROOT.prefab` variant for an FBX.

**Bezier**
- Rigs a bone chain to one cubic Bezier curve (two anchors, two handles) so moving the handles bends the chain, with optional twist and stretch.

**Insertable prop**
- Generates a draw, drop and reinsert system for a prop and its socket: grabbed by a PhysBone, slides back in when released near the socket, otherwise drops. Needs VRChat SDK 3.10.6 or newer.

## 4. Flow chart

```mermaid
flowchart TD
    A[Train: pick start and end bones] --> B[Assign Start position]
    B --> C[Generate or assign physics chains]
    C --> D[Set up constraints]
    D --> E[VRC Parent Constraint per moving bone: off, sources at 0, bones at rest]
    E --> F[Build animation]
    F --> G[Per bone: switch on, Start position, along chain 1 to the next chain bone]
    G --> H[Blend chain 1 to 2 to ... at each chain end]
    H --> I[Motion Time clip saved]
```

## 5. References

- VRChat Avatars SDK constraints (`VRCParentConstraint` and the other VRC constraint types), VRChat PhysBones.
