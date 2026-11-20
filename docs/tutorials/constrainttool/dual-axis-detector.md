# Tutorial: Constraint Tools' Dual axis detector

Read how far a bone tilts left/right and up/down relative to a flat plane, as two
contact parameters, with two Proximity receivers laid out in an L.

Requires the VRChat Avatars SDK 3.7.0 or newer. Without it, the menu entries
show **VRCSDK not found**.

## 1. Open the tab

Right-click the object that moves in the Hierarchy and choose **RiryTools > Constraint Tools > Dual axis detector**: it fills **Source**. To fill **Relative to** too, Ctrl/Cmd-click that object first so both are selected.

You can also choose **Tools > RiryTools > Constraint Tools** and open the **Dual axis detector**
tab, or open it from the Main Menu's Tools > Constraint Tools sub-tab.

## 2. Pick the objects

1. Drag the object that moves into **Source**, such as the end of the tilting
   bone. The sender goes in a new child of it.
2. Drag the object the plane is fixed to into **Relative to**, such as the
   tilting bone's parent. The receivers go in a new child of it.

Both must be in the same scene (or in Prefab Mode), and Relative to can't be
inside Source.

## 3. Set up the plane

- **Flat plane** is the axis the plane faces (X, Y or Z). The receivers lie
  across the other two axes.
- **Space**: **Local** takes the plane's axes from Relative to, so the plane
  turns with it. **World** uses the world's axes at the time of the build.
- **Offset** is how far each receiver sits from Source's rest position, in
  meters (0.05 by default). Each receiver's radius is twice this, so it reads 0.5
  at rest and 1 when Source has moved one Offset toward it.

The **Parameters** list shows the names the receivers will drive: `Tilt/Right`
and `Tilt/Up`.

## 4. Build it

Click **Build dual axis detector**. It adds:

- `TiltDetector_<source>` under Relative to, at Source's rest position, holding
  the `Right` and `Up` receivers.
- `TiltSender_<source>` under Source, holding the sender.

The sender and receivers share a collision tag of their own and only take
contacts from the same avatar. Nothing existing is changed, and the build is one
undo step. On a prefab instance both objects are added objects, so the prefab
stays packed; you can constrain them or move them in the hierarchy.

## 5. Use the parameters

Add Float parameters named `Tilt/Right` and `Tilt/Up` to your animator. To turn
them into a tilt from -1 to 1, with 0 at rest:

- X = 2 × `Tilt/Right` − 1
- Y = 2 × `Tilt/Up` − 1

## 6. Check it

Contacts don't run in Edit Mode. In Play Mode (with Gesture Manager, for
example), move Source and watch the two parameters change.
