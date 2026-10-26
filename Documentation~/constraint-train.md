# Constraint Train

Target: Unity 2022.3 LTS with the VRChat Avatars SDK 3.7.0 or newer (VRC
Constraints). The window only compiles when that SDK is installed.

Open **Tools > Rirytools > Constraint Train**, or right-click a bone in the
Hierarchy and choose **Rirytools > Constraint Train...**. From the Hierarchy, the
right-clicked bone becomes the chain start (RB1). Select a second bone below it
first (Ctrl/Cmd-click) to fill in the end as well. Switching to a new start
clears the chains and avatar root found for the previous one.

## What it builds

Terms used below:

- **Real bones** are the avatar's chain being animated, numbered from the root:
  RB1 (real bone 1, the start, closest to the root) to RBn (the tip). "RBk"
  means real bone number k.
- **PBC1-k** is bone k of physics chain 1 (the copy of real bone k in that
  chain); PBC2-k is the same bone in physics chain 2, and so on.
- **REST** is the `Rest_` chain, a still copy of the starting pose.

Each real bone gets one VRC Parent Constraint. A single float, driven through
**Motion Time**, moves the chain from its rest pose onto up to five physics
chains:

1. **Train (0 to Train end).** The range is split into n equal steps. The tip
   (RBn) moves from REST to physics chain 1's bone 1 in the first step, to its
   bone 2 in the next, and so on, reaching its bone n at Train end. Each real
   bone closer to the root starts one step later than the bone after it. At
   Train end, every real bone sits on its own copy in physics chain 1 (real
   bone 1 on PBC1-1, real bone 2 on PBC1-2, and so on).
2. **Chain blends (Train end to 1).** The rest of the range is split evenly
   between the physics chains. Every real bone moves from its copy in physics
   chain 1 to its copy in physics chain 2, then to chain 3, and so on.

Example with 5 bones, 2 chains, and Train end 0.5: each step is 0.1. At 0.2
the tip is on PBC1-2 while real bone 4 is on PBC1-1. At 0.5 every real bone is
on its own physics chain 1 bone, and at 0.75 every bone is halfway between its
physics chain 1 and physics chain 2 bones (with a linear blend curve).

Source order on real bone k (used by the animation, so do not reorder it):
REST, PBC1-1 .. PBC1-k, then PBC2-k .. PBC5-k. Offsets are zero, so each bone
snaps to the pose of its targets.

## Steps

1. Assign the real chain **Start (RB1)** and **End**. The end must be the start
   or one of its descendants. **Avatar root** fills in from the nearest parent
   Animator; animation paths are relative to it.
2. Choose 1 to 5 **Physics chains**. Assign each chain's start, or click
   **Generate** (or **Generate all missing chains**). A **Rest** chain is also
   needed: it holds the REST targets.
3. Adjust **Train end**. **Default** splits the range evenly: 1/chains.
4. Click **Set up constraints**, then **Build animation**.
5. Put the clip in an Animator state, enable **Motion Time**, and drive it with
   your float parameter.

Assigned target chains are followed downward from their start. A bone with one
child continues to that child; with several children, the child whose name ends
with the matching real bone's name is used.

## Generated chains

Generated empties are named `Phys1_` .. `Phys5_` (or `Rest_`) plus the real
bone's name. The first one is a sibling of RB1 (a child of RB1's parent) and the
rest are nested in chain order. Each matches its real bone's world position,
rotation, and scale. They are added objects, so prefab instances are never
unpacked and prefab assets are never edited. If a chain with the generated name
already exists, it is reused.

With **Add endpoints**, each physics chain also gets
`PhysN_<last bone>_End`, offset from the last bone by the same world offset as
the last real bone from the one before it. Rest chains never get an endpoint.
Put your PhysBones on the physics chains.

## Prefabs

Chains can be generated and constraints set up directly in Prefab Mode; they
save with the prefab like any other edit.

On a prefab instance in a scene, the chains and constraints are instance
overrides in that scene. The window then offers to save them:

- **Save as Prefab Overrides** applies the generated `Rest_`/`Phys1_`..`Phys5_`
  chains, new VRC Parent Constraints and changed constraints to the outermost
  prefab, so every instance gets them. Chains are applied first, so the
  constraints point at the prefab's own copies. It is one Undo step.
- When that prefab is an FBX model or a read-only package prefab, it cannot hold
  overrides. **Save to `<name>_ROOT.prefab`** instead saves the instance, with all
  its overrides, as a Prefab Variant of the model in the model's folder (or in
  `Assets` for a read-only package), and connects the scene object to it. Save
  the scene to keep that link. An existing `_ROOT` prefab is replaced (keeping its
  GUID) only after confirmation, or a new copy is saved beside it.

## Curves

- **Train step curve** shapes each move between two neighboring targets.
- **Chain blend curve** is the influence of the next physics chain across each
  blend. Both curves are rescaled to run from 0 to 1.
- **Next chain at halfway** with **Apply** builds a power curve. For example,
  75% gives 75% PBC2 at 0.75 in the 5-bone example. **Linear** resets it.

Curves are sampled adaptively: straight-line keys stay within 0.2% of the exact
weights, and keys on straight spans are removed.

## Existing constraints and the clip

- A bone with an empty VRC Parent Constraint reuses it. One with sources is
  replaced after confirmation, because sources are animated by index. A
  constraint that comes from the bone's prefab is never removed automatically.
- **Build animation** saves a new clip when the Clip field is empty. An assigned
  clip has all of its curves replaced after confirmation, keeping its GUID and
  Animator references.
- Every change supports Undo.

## Preview

The **Value** slider shows each bone's weights. **Preview in Scene** samples the
generated weights with Animation Mode on the set-up constraints, and stops before
setup or build. Nothing is saved while previewing.

## Names and other limits

- The float parameter's name is never used by this tool: you pick it in the
  Animator's Motion Time setting, so any characters Unity accepts there work.
- Animation paths are built from object names. A real bone, or any object
  between it and the avatar root, whose name contains `/` is rejected, because
  Unity splits paths on `/`. So is one whose name repeats an earlier sibling's
  name, because Unity would animate that sibling instead. Rename the object.
- Avatar root defaults to the outermost Animator above the chain, so an
  Animator on a prop or nested object is not picked by mistake.
- Every target must be inside the avatar root (VRChat drops outside sources on
  upload), outside the real chain, and used by only one target chain.
- Generating chains needs RB1 to have a parent; otherwise the new chain would
  become a second root, which a prefab cannot hold.
- A bone with two VRC Parent Constraints is rejected. Other constraints on the
  same bone are allowed but reported, since they also move it.
- A clip inside an installed (read-only) package, an imported clip, or one not
  open for editing is never replaced. Clear the Clip field to save a new one.
- A curve containing NaN or Infinity, or one that starts and ends at the same
  value, is treated as linear. Curves that overshoot are clamped to 0..1.
- With a one-bone chain, no endpoint is generated: there is no previous bone to
  measure an offset from.

## Tests

`ConstraintTrainTests` covers the example timing, chain blends, the power curve,
one to five chains, weight sums, curve accuracy, overflow source names, broken
curves, chain generation and lookup, `/` and duplicate names in paths, shared
targets, and avatar root detection. They do not need the VRChat SDK.
