# Tutorial: Blendshape Sync

Keep the blendshape weights of clothing, hair or accessories in step with a body
mesh while you work.

## 1. Open the window

Choose **Tools > Rirytools > Blendshape Sync**.

## 2. Assign the meshes

1. Drag the body's SkinnedMeshRenderer from the Hierarchy into **Master**.
2. Drag a clothing SkinnedMeshRenderer into **Child 1**. A new empty row appears
   each time you fill one, so keep adding children.
3. Use **Remove** on a row to take a child out.

## 3. Sync

- Click **Sync now** to copy every matching weight once.
- Turn on **Run automatically** to sync every half second while you adjust the
  master.

Names match ignoring case. If two blendshapes on a child differ only by case,
the pass stops and offers to switch to exact matching; **Debug** turns exact,
case-sensitive matching on and off by hand.

## 4. Link differently named blendshapes

When the child's blendshape has another name (for example `Breast_Big` on the
body and `Chest_Large` on a shirt):

1. Open the child's **Manual link** foldout.
2. Enter the master's name in **Source** and the child's name in **Destination**.
3. Sync again. Remove a link with **X**.

## 5. Undo or restore

- **Ctrl+Z** undoes a sync. Undoing the tool's own sync also stops automatic
  syncing, so it does not immediately sync again.
- **Set all to original** puts back every weight as it was when each renderer was
  first assigned, and stops automatic syncing. These originals are kept while the
  window stays open.

Syncing pauses on its own during Play Mode, animation preview or recording, and
Animator graph editing.

Reference: [design/blendshape-sync.md](../design/blendshape-sync.md)
