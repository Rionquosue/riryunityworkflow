# Constraint Train

Target: Unity 2022.3 LTS with the VRChat Avatars SDK 3.7.0 or newer (VRC
Constraints). The window only compiles when that SDK is installed.

Open **Tools > Rirytools > Constraint Train**.

## What it builds

A real bone chain RB1..RBn (RB1 is the start, closest to the root) gets one
VRC Parent Constraint per bone. A single float, driven through **Motion Time**,
moves the chain from its rest pose onto up to five physics chains:

1. **Train (0 to Train end).** Each step is `Train end / n`. RBn, the tip,
   moves from REST to PBC1-1 in the first step, then PBC1-2, and so on until it
   reaches PBC1-n at Train end. Every bone toward the root follows one step
   later, so RBn-1 is at PBC1-1 when RBn is at PBC1-2. At Train end every RBk is
   fully on PBC1-k.
2. **Chain blends (Train end to 1).** The remaining range is split evenly
   between the physics chains. Each bone moves from PBC1-k to PBC2-k, then to
   PBC3-k, and so on.

Example with 5 bones, 2 chains, and Train end 0.5: each step is 0.1, RB5 is at
PBC1-2 at 0.2 while RB4 is at PBC1-1, every bone is on its PBC1 bone at 0.5, and
at 0.75 every bone is 50% PBC1 and 50% PBC2 (with a linear blend curve).

Source order on bone RBk (used by the animation, so do not reorder it):
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

## Tests

`ConstraintTrainTests` covers the example timing, chain blends, the power curve,
one to five chains, weight sums, curve accuracy, overflow source names, and
chain generation and lookup. They do not need the VRChat SDK.
