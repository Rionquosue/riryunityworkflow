# Tutorial: Constraint Train

Animate a bone chain (a tail, ears, hair) so it slides onto physics-driven
copies of itself, one bone after another, driven by a single float.

Requires the VRChat Avatars SDK 3.7.0 or newer. Without it, the menu entries
show **VRCSDK not found**.

## 1. Open the window on the chain

Right-click the first bone of the chain in the Hierarchy (the one closest to the
root) and choose **RiryTools > Constraint Train...**. To fill in the last bone
too, Ctrl/Cmd-click it first so both are selected.

You can also choose **Tools > RiryTools > Constraint Train** and drag the bones
into **Start (RB1)** and **End**. **Avatar root** fills itself in with the
avatar's Animator.

## 2. Make the target chains

1. Set **Physics chains** to how many chains the bones should travel through (1
   to 5).
2. Leave **Add endpoints** on so each physics chain gets an end bone for the
   physics component.
3. Click **Generate all missing chains**. This creates `Rest_` (the still pose)
   and `Phys1_`, `Phys2_` ... copies of the chain beside it. You can also use
   **Generate** on a single row, or drag in chains you made yourself.
4. Add your physics components (for example VRC PhysBones) to the `Phys` chains.

## 3. Tune the timing

- **Train end** is the value where the train ends: every bone has arrived on
  physics chain 1. **Default** resets it.
- **Chain 2 end**, **Chain 3 end** and so on are the values where every bone
  has arrived on that physics chain. They appear with two or more chains.
- **Train step curve** shapes each bone's move from one target to the next.
- **Chain blend curve** shapes the move from one physics chain to the next.

## 4. Build it

1. Click **Set up constraints**. Each bone gets a VRC Parent Constraint.
2. Click **Build animation** and save the clip. It is offered in
   `Assets/RiryTools` by default.
3. In your Animator, put the clip in a state, turn on **Motion Time**, and drive
   it with your float parameter.

## 5. Check it

Open the clip in the Animation window and scrub it to see the bones move. Physics
do not run in Edit Mode, so check the final motion in Play Mode.

## 6. Keep it in the prefab

On a prefab instance in a scene, the new chains and constraints are scene
overrides until saved:

- **Save as Prefab Overrides** writes them into the prefab.
- If the avatar is an FBX (which cannot hold overrides), the button reads
  **Save to `<name>_ROOT.prefab`**: it saves a prefab variant beside the FBX and
  connects the scene object to it. Save the scene afterwards.

You can also do all of this directly in Prefab Mode.

Reference: [design/constraint-train.md](../design/constraint-train.md)
