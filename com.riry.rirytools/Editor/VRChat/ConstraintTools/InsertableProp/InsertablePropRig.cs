using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Builds the scene side of one insertable prop pair under the avatar root: IP_&lt;id&gt; with the
    /// prop, its PhysBone grab rig, the hand frames, and the PC physics bodies or the Android raycast
    /// drop. The handles (Grip, Tip on the prop; Seat, Opening on the socket) keep their poses when
    /// Generate runs again; everything else is rebuilt. Fields that only exist from SDK 3.10.6
    /// (advanced contact parameters, constraint smoothing, VRCRaycast) are set by name, so this
    /// still compiles with older SDKs; the window refuses to generate without them.
    /// </summary>
    internal static class InsertablePropRig
    {
        internal const string RootPrefix = "IP_";
        internal const string PropName = "Prop";
        internal const string GripName = "IP Grip";
        internal const string TipName = "IP Tip";
        internal const string RigName = "Rig";
        internal const string WorldName = "World";
        internal const string DropName = "Drop";
        internal const float TipRadius = 0.01f;
        internal const float GripReceiverRadius = 0.15f;
        internal const float GrabRadius = 0.06f;
        internal const float BodyMass = 1f;
        internal const float FollowerMass = 0.01f;
        // Layer 13 is VRChat's Pickup layer: collides with the world, not with players.
        internal const int PickupLayer = 13;
        internal const string RaycastType = "VRC.SDK3.Avatars.Components.VRCRaycast, VRCSDK3A";

        // ContactReceiver.AdvancedParameter flags (3.10.6): SenderType 1, SenderLocalPosition 2,
        // PointSeparation 16, CollisionTags the high bit.
        private const int AdvancedFlags = 1 | 2 | 16 | int.MinValue;

        /// <summary>Everything Build made, for the animator and the report.</summary>
        internal sealed class Rig
        {
            public string Id;
            public Transform AvatarRoot, Root, Prop, Instance, Grip, Tip;
            public Transform Seat, SeatGrip, Entry, Opening;
            public Transform Anchor, End, GripSense, Marker;
            public Transform FrameRemote, FrameWearerL, FrameWearerR, FrameSheath, HandFrame, Hand;
            public Transform World, Body, BodyGrip, Follower;
            public Transform Drop, RayOrigin, Landed, DropYaw, Lying, DropGrip;
            public bool Android;
            public float Smoothing;
        }

        /// <summary>The pair's ID from the avatar and prop names, safe for object, tag and parameter names.</summary>
        internal static string PairId(GameObject avatar, GameObject prop)
        {
            string raw = (avatar != null ? avatar.name : "") + "_" + (prop != null ? prop.name : "");
            var id = new System.Text.StringBuilder();
            foreach (char c in raw) id.Append(char.IsLetterOrDigit(c) ? c : '_');
            return id.ToString();
        }

        /// <summary>Whether the installed SDK has what the rig needs (SDK 3.10.6 or newer).</summary>
        internal static string MissingSdkFeature()
        {
            if (typeof(ContactReceiver).GetField("advancedParameters") == null) return "advanced contact parameters";
            if (typeof(VRCConstraintBase).GetField("SmoothingFactor") == null) return "constraint smoothing";
            Type raycast = Type.GetType(RaycastType);
            if (raycast == null) return "VRCRaycast";
            // Every VRCRaycast field Build sets, so a renamed one is caught here instead of being skipped.
            foreach (string field in RaycastFields)
            {
                if (FieldOf(raycast, field) == null) return "VRCRaycast." + field;
            }
            return null;
        }

        private static readonly string[] RaycastFields = { "version", "rootTransform", "raycastDirection", "distance", "collisionMode", "resultTransform" };

        private static System.Reflection.FieldInfo FieldOf(Type type, string name)
        {
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            for (Type current = type; current != null; current = current.BaseType)
            {
                System.Reflection.FieldInfo field = current.GetField(name, Flags);
                if (field != null) return field;
            }
            return null;
        }

        internal static Transform FindRoot(Transform avatarRoot, string id)
        {
            return avatarRoot != null ? Child(avatarRoot, RootPrefix + id) : null;
        }

        internal static Transform FindOnSocket(Transform socket, string id, string what)
        {
            return socket != null ? Child(socket, RootPrefix + id + " " + what) : null;
        }

        private static Transform Child(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        /// <summary>
        /// Builds or rebuilds the pair. Handles that exist keep their poses; on the first run they
        /// start at the socket (Seat, Opening), the prop's root (Grip) and its top (Tip).
        /// </summary>
        internal static Rig Build(InsertablePropWindow.Inputs inputs, InsertablePropWindow.Settings settings, string undoName)
        {
            var rig = new Rig
            {
                Id = PairId(inputs.Avatar.gameObject, inputs.PropPrefab),
                AvatarRoot = inputs.Avatar.transform,
                Android = settings.platform == InsertablePropWindow.Platform.Android,
                Smoothing = settings.insertSmoothing
            };
            int layer = inputs.Socket.gameObject.layer;

            rig.Root = FindRoot(rig.AvatarRoot, rig.Id) ?? Create(RootPrefix + rig.Id, rig.AvatarRoot, layer, undoName);
            foreach (string generated in new[] { RigName, WorldName, DropName })
            {
                Transform old = Child(rig.Root, generated);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }

            // The prop: a container the constraints move, holding an instance of the prefab.
            rig.Prop = Child(rig.Root, PropName) ?? Create(PropName, rig.Root, layer, undoName);
            RemoveConstraints(rig.Prop);
            rig.Instance = FindInstance(rig.Prop, inputs.PropPrefab);
            if (rig.Instance == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(inputs.PropPrefab, rig.Prop);
                Undo.RegisterCreatedObjectUndo(instance, undoName);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                rig.Instance = instance.transform;
            }
            Bounds bounds = PropBounds(rig.Prop, rig.Instance);
            rig.Grip = Child(rig.Instance, GripName) ?? Create(GripName, rig.Instance, layer, undoName);
            rig.Tip = Child(rig.Instance, TipName);
            if (rig.Tip == null)
            {
                rig.Tip = Create(TipName, rig.Instance, layer, undoName);
                rig.Tip.position = rig.Prop.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            }

            // The socket's handles and targets.
            rig.Seat = FindOnSocket(inputs.Socket, rig.Id, "Seat") ?? Create(RootPrefix + rig.Id + " Seat", inputs.Socket, layer, undoName);
            rig.Opening = FindOnSocket(inputs.Socket, rig.Id, "Opening");
            if (rig.Opening == null)
            {
                // Made just outside the socket, against its forward (insertion) axis, so its handle is not on the Seat's.
                rig.Opening = Create(RootPrefix + rig.Id + " Opening", inputs.Socket, layer, undoName);
                rig.Opening.localPosition = new Vector3(0f, 0f, -2f * Mathf.Max(settings.captureRadius, 0.01f));
            }
            rig.Entry = FindOnSocket(inputs.Socket, rig.Id, "Entry") ?? Create(RootPrefix + rig.Id + " Entry", inputs.Socket, layer, undoName);

            // The grip and tip in the prop's own space; every target below uses them.
            Vector3 gripPosition = rig.Prop.InverseTransformPoint(rig.Grip.position);
            Quaternion gripRotation = Quaternion.Inverse(rig.Prop.rotation) * rig.Grip.rotation;
            Vector3 tipInProp = rig.Prop.InverseTransformPoint(rig.Tip.position);
            Vector3 offsetPosition, offsetEuler;
            InsertablePropMath.InverseOffset(gripPosition, gripRotation, out offsetPosition, out offsetEuler);

            rig.SeatGrip = Child(rig.Seat, "Grip") ?? Create("Grip", rig.Seat, layer, undoName);
            SetLocalPose(rig.SeatGrip, gripPosition, gripRotation);
            Vector3 seatedTip = rig.Seat.position + rig.Seat.rotation * tipInProp;
            Undo.RecordObject(rig.Entry, undoName);
            rig.Entry.SetPositionAndRotation(
                InsertablePropMath.EntryPosition(rig.Seat.position, seatedTip, rig.Opening.position, rig.Opening.forward), rig.Seat.rotation);

            // The tip sender and the sheath receiver, tagged for this pair only.
            string tag = RootPrefix + rig.Id + "_Tip";
            RemoveComponents<VRCContactSender>(rig.Tip);
            var sender = Undo.AddComponent<VRCContactSender>(rig.Tip.gameObject);
            sender.shapeType = ContactBase.ShapeType.Sphere;
            sender.radius = TipRadius;
            sender.collisionTags = new List<string> { tag };
            RemoveComponents<VRCContactReceiver>(rig.Opening);
            var sheath = Undo.AddComponent<VRCContactReceiver>(rig.Opening.gameObject);
            sheath.shapeType = ContactBase.ShapeType.Sphere;
            sheath.radius = settings.captureRadius;
            sheath.collisionTags = new List<string> { tag };
            sheath.allowSelf = true;
            sheath.allowOthers = false;
            sheath.receiverType = ContactReceiver.ReceiverType.Proximity;
            sheath.parameter = InsertablePropAnimator.Parameter(rig.Id, InsertablePropAnimator.Sheath);
            Dirty(sender);
            Dirty(sheath);

            // Where a dropped prop lies: the PC body, or Android's raycast result.
            Transform dropTarget, dropGrip;
            if (rig.Android) BuildAndroidDrop(rig, bounds, gripPosition, gripRotation, layer, undoName, out dropTarget, out dropGrip);
            else BuildPhysics(rig, inputs, settings, bounds, gripPosition, gripRotation, offsetPosition, offsetEuler, layer, undoName, out dropTarget, out dropGrip);

            BuildGrab(rig, inputs, settings, dropGrip, layer, undoName);
            SetFollowerHand(rig);
            SetAndroidHand(rig);

            // The prop follows the seat, the entry point, the hand (through the grip's inverse) or the drop.
            var prop = AddConstraint<VRCParentConstraint>(rig.Prop, new[] { rig.Seat, rig.Entry, rig.Hand, dropTarget }, new[] { 1f, 0f, 0f, 0f });
            SetSourceOffset(prop, 2, offsetPosition, offsetEuler);
            SetSmoothing(prop, 0f);
            Dirty(prop);
            return rig;
        }

        private static void BuildGrab(Rig rig, InsertablePropWindow.Inputs inputs, InsertablePropWindow.Settings settings,
            Transform dropGrip, int layer, string undoName)
        {
            Transform folder = Create(RigName, rig.Root, layer, undoName);

            // PhysBone: the root sits AnchorLength back from the grip and freezes in place on grab; the end
            // is pulled to the grip while not grabbed (Is Animated takes that as its rest).
            rig.Anchor = Create("Anchor", folder, layer, undoName);
            rig.Anchor.position = rig.SeatGrip.TransformPoint(new Vector3(0f, -InsertablePropMath.AnchorLength, 0f));
            rig.Anchor.rotation = rig.SeatGrip.rotation;
            var anchor = AddConstraint<VRCParentConstraint>(rig.Anchor, new[] { rig.SeatGrip, dropGrip }, new[] { 1f, 0f });
            var back = new Vector3(0f, -InsertablePropMath.AnchorLength, 0f);
            SetSourceOffset(anchor, 0, back, Vector3.zero);
            SetSourceOffset(anchor, 1, back, Vector3.zero);
            Dirty(anchor);
            rig.End = Create("End", rig.Anchor, layer, undoName);
            rig.End.position = rig.SeatGrip.position;
            Dirty(AddConstraint<VRCPositionConstraint>(rig.End, new[] { rig.SeatGrip, dropGrip }, new[] { 1f, 0f }));

            var bone = Undo.AddComponent<VRCPhysBone>(rig.Anchor.gameObject);
            bone.rootTransform = rig.Anchor;
            bone.pull = 1f;
            bone.spring = 0f;
            bone.stiffness = 1f;
            bone.gravity = 0f;
            bone.immobile = 1f;
            bone.allowCollision = VRCPhysBoneBase.AdvancedBool.False;
            bone.radius = GrabRadius;
            // Only the end is grabbable, not the invisible line back to the root.
            bone.radiusCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.9f, 0f), new Keyframe(1f, 1f));
            bone.allowGrabbing = VRCPhysBoneBase.AdvancedBool.Other;
            VRCPhysBoneBase.PermissionFilter filter = bone.grabFilter;
            filter.allowSelf = settings.allowSelf;
            filter.allowOthers = settings.allowOthers;
            bone.grabFilter = filter;
            bone.allowPosing = VRCPhysBoneBase.AdvancedBool.False;
            bone.snapToHand = true;
            bone.grabMovement = 1f;
            bone.maxStretch = InsertablePropMath.MaxStretch;
            bone.isAnimated = true;
            bone.parameter = InsertablePropAnimator.Parameter(rig.Id, "");
            Dirty(bone);

            // The grip receiver reads the holder's little finger in the end's frame.
            rig.GripSense = Create("GripSense", folder, layer, undoName);
            rig.GripSense.SetPositionAndRotation(rig.End.position, rig.End.rotation);
            Dirty(AddConstraint<VRCParentConstraint>(rig.GripSense, new[] { rig.End }, new[] { 1f }));
            var grip = Undo.AddComponent<VRCContactReceiver>(rig.GripSense.gameObject);
            grip.shapeType = ContactBase.ShapeType.Sphere;
            grip.radius = GripReceiverRadius;
            grip.collisionTags = new List<string> { "FingerLittleL", "FingerLittleR" };
            grip.allowSelf = true;
            grip.allowOthers = true;
            grip.receiverType = ContactReceiver.ReceiverType.Proximity;
            grip.parameter = InsertablePropAnimator.Parameter(rig.Id, InsertablePropAnimator.Grip);
            SetField(grip, "advancedParameters", AdvancedFlags);
            Dirty(grip);
            rig.Marker = Create("Marker", rig.GripSense, layer, undoName);

            // Hand frames: each aims the grip's -Y (toward the pommel) at a little finger, +Z up.
            var aimAxis = new Vector3(0f, -1f, 0f);
            var upAxis = new Vector3(0f, 0f, 1f);
            rig.FrameRemote = Frame("Frame Remote", folder, rig.End, rig.Marker, aimAxis, upAxis, null, Vector3.up, layer, undoName);
            Animator animator = inputs.Avatar.GetComponent<Animator>();
            rig.FrameWearerL = WearerFrame("Frame Wearer L", folder, rig.End, animator, true, aimAxis, upAxis, layer, undoName);
            rig.FrameWearerR = WearerFrame("Frame Wearer R", folder, rig.End, animator, false, aimAxis, upAxis, layer, undoName);
            // The fallback, and the approach: the grip's +Y (toward the tip) at the sheath opening.
            rig.FrameSheath = Frame("Frame Sheath", folder, rig.End, rig.Opening, Vector3.up, upAxis, null, Vector3.up, layer, undoName);

            rig.HandFrame = Create("HandFrame", folder, layer, undoName);
            Dirty(AddConstraint<VRCPositionConstraint>(rig.HandFrame, new[] { rig.End }, new[] { 1f }));
            var handFrame = AddConstraint<VRCRotationConstraint>(rig.HandFrame,
                new[] { rig.FrameRemote, rig.FrameWearerL, rig.FrameWearerR, rig.FrameSheath }, new[] { 0f, 0f, 0f, 1f });
            SetSmoothing(handFrame, settings.gripSmoothing);
            Dirty(handFrame);
            rig.Hand = Create("Hand", folder, layer, undoName);
            Dirty(AddConstraint<VRCPositionConstraint>(rig.Hand, new[] { rig.End }, new[] { 1f }));
            Dirty(AddConstraint<VRCRotationConstraint>(rig.Hand, new[] { rig.HandFrame, rig.FrameSheath }, new[] { 1f, 0f }));
        }

        private static Transform Frame(string name, Transform folder, Transform end, Transform aimAt, Vector3 aimAxis, Vector3 upAxis,
            Transform upObject, Vector3 upVector, int layer, string undoName)
        {
            Transform frame = Create(name, folder, layer, undoName);
            Dirty(AddConstraint<VRCPositionConstraint>(frame, new[] { end }, new[] { 1f }));
            var aim = AddConstraint<VRCAimConstraint>(frame, new[] { aimAt }, new[] { 1f });
            aim.AimAxis = aimAxis;
            aim.UpAxis = upAxis;
            aim.WorldUp = upObject != null ? VRCConstraintBase.WorldUpType.ObjectRotationUp : VRCConstraintBase.WorldUpType.Vector;
            aim.WorldUpTransform = upObject;
            aim.WorldUpVector = upVector;
            aim.AffectsRotationX = true;
            aim.AffectsRotationY = true;
            aim.AffectsRotationZ = true;
            Dirty(aim);
            return frame;
        }

        // The wearer's own hand: aim at their little finger, up along their hand's wrist-to-middle-finger direction.
        private static Transform WearerFrame(string name, Transform folder, Transform end, Animator animator, bool left,
            Vector3 aimAxis, Vector3 upAxis, int layer, string undoName)
        {
            Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            Transform middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Vector3 forward = Quaternion.Inverse(hand.rotation) * (middle.position - hand.position).normalized;
            return Frame(name, folder, end, little, aimAxis, upAxis, hand, forward, layer, undoName);
        }

        /// <summary>Why the avatar can't take a pair (no humanoid hands and fingers), or null.</summary>
        internal static string AvatarError(Animator animator)
        {
            if (animator == null || !animator.isHuman) return "The avatar needs a humanoid Animator.";
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLittleProximal,
                         HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal })
            {
                if (animator.GetBoneTransform(bone) == null) return "The avatar's humanoid rig has no " + ObjectNames.NicifyVariableName(bone.ToString()) + ".";
            }
            return null;
        }

        // PC: a dynamic body with the collider, tied by a ConfigurableJoint to a light follower on the hand.
        private static void BuildPhysics(Rig rig, InsertablePropWindow.Inputs inputs, InsertablePropWindow.Settings settings, Bounds bounds,
            Vector3 gripPosition, Quaternion gripRotation, Vector3 offsetPosition, Vector3 offsetEuler, int layer, string undoName,
            out Transform dropTarget, out Transform dropGrip)
        {
            rig.World = Create(WorldName, rig.Root, layer, undoName);
            Dirty(AddConstraint<VRCParentConstraint>(rig.World, new[] { rig.Root }, new[] { 1f }));

            rig.Body = Create("Body", rig.World, PickupLayer, undoName);
            rig.Body.SetPositionAndRotation(rig.Seat.position, rig.Seat.rotation);
            var box = Undo.AddComponent<BoxCollider>(rig.Body.gameObject);
            box.center = bounds.center;
            box.size = bounds.size;
            var body = Undo.AddComponent<Rigidbody>(rig.Body.gameObject);
            body.mass = BodyMass;
            body.isKinematic = true;
            Dirty(AddConstraint<VRCParentConstraint>(rig.Body, new[] { rig.Seat }, new[] { 1f }));
            rig.BodyGrip = Create("Grip", rig.Body, PickupLayer, undoName);
            SetLocalPose(rig.BodyGrip, gripPosition, gripRotation);

            rig.Follower = Create("Follower", rig.World, layer, undoName);
            rig.Follower.SetPositionAndRotation(rig.Seat.position, rig.Seat.rotation);
            var follower = Undo.AddComponent<Rigidbody>(rig.Follower.gameObject);
            follower.mass = FollowerMass;
            follower.isKinematic = true;
            // Its hand source is filled in once the hand exists (SetFollowerHand).
            var follow = AddConstraint<VRCParentConstraint>(rig.Follower, new[] { rig.Seat, rig.Seat }, new[] { 1f, 0f });
            SetSourceOffset(follow, 1, offsetPosition, offsetEuler);
            Dirty(follow);

            // Drives near critical damping, so the body tracks the hand without ringing.
            float stiffness = settings.stiffness;
            float damper = InsertablePropMath.CriticalDamping(stiffness, BodyMass);
            float inertia = InsertablePropMath.BoxInertia(BodyMass, bounds.size);
            float angular = stiffness * inertia / BodyMass;
            var joint = Undo.AddComponent<ConfigurableJoint>(rig.Body.gameObject);
            joint.connectedBody = follower;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = Vector3.zero;
            var drive = new JointDrive { positionSpring = stiffness, positionDamper = damper, maximumForce = float.MaxValue };
            joint.xDrive = drive;
            joint.yDrive = drive;
            joint.zDrive = drive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = angular,
                positionDamper = InsertablePropMath.CriticalDamping(angular, inertia),
                maximumForce = float.MaxValue
            };
            Dirty(joint);
            dropTarget = rig.Body;
            dropGrip = rig.BodyGrip;
        }

        /// <summary>Points the follower's second source at the hand (it's built before the hand).</summary>
        internal static void SetFollowerHand(Rig rig)
        {
            if (rig.Follower == null) return;
            var follow = rig.Follower.GetComponent<VRCParentConstraint>();
            var sources = follow.Sources;
            VRCConstraintSource hand = sources[1];
            hand.SourceTransform = rig.Hand;
            sources[1] = hand;
            follow.Sources = sources;
            Dirty(follow);
        }

        // Android: no physics. A raycast below the hand finds the floor; on release the prop lies there.
        private static void BuildAndroidDrop(Rig rig, Bounds bounds, Vector3 gripPosition, Quaternion gripRotation, int layer, string undoName,
            out Transform dropTarget, out Transform dropGrip)
        {
            rig.Drop = Create(DropName, rig.Root, layer, undoName);
            rig.RayOrigin = Create("RayOrigin", rig.Drop, layer, undoName);
            rig.RayOrigin.position = rig.Seat.position;
            rig.Landed = Create("Landed", rig.Drop, layer, undoName);
            rig.Landed.position = rig.Seat.position;
            rig.DropYaw = Create("DropYaw", rig.Drop, layer, undoName);
            rig.DropYaw.SetPositionAndRotation(rig.Seat.position, rig.Seat.rotation);
            rig.Lying = Create("Lying", rig.DropYaw, layer, undoName);
            Vector3 lyingPosition;
            Quaternion lyingRotation;
            InsertablePropMath.LyingPose(bounds, out lyingPosition, out lyingRotation);
            SetLocalPose(rig.Lying, lyingPosition, lyingRotation);
            rig.DropGrip = Create("Grip", rig.Lying, layer, undoName);
            SetLocalPose(rig.DropGrip, gripPosition, gripRotation);

            Type type = Type.GetType(RaycastType);
            Component ray = Undo.AddComponent(rig.RayOrigin.gameObject, type);
            var serialized = new SerializedObject(ray);
            SetProperty(serialized, "version", 1);
            serialized.FindProperty("rootTransform").objectReferenceValue = rig.RayOrigin;
            serialized.FindProperty("raycastDirection").vector3Value = Vector3.down;
            serialized.FindProperty("distance").floatValue = 50f;
            SetProperty(serialized, "collisionMode", 0);
            serialized.FindProperty("resultTransform").objectReferenceValue = rig.Landed;
            serialized.ApplyModifiedProperties();
            dropTarget = rig.Lying;
            dropGrip = rig.DropGrip;
        }

        /// <summary>The Android parts that need the hand: the ray origin and the yaw follow it until release.</summary>
        internal static void SetAndroidHand(Rig rig)
        {
            if (rig.Drop == null) return;
            Dirty(AddConstraint<VRCPositionConstraint>(rig.RayOrigin, new[] { rig.Hand }, new[] { 1f }));
            Dirty(AddConstraint<VRCPositionConstraint>(rig.DropYaw, new[] { rig.Landed }, new[] { 1f }));
            var yaw = AddConstraint<VRCRotationConstraint>(rig.DropYaw, new[] { rig.Hand }, new[] { 1f });
            yaw.AffectsRotationX = false;
            yaw.AffectsRotationZ = false;
            Dirty(yaw);
        }

        private static Transform FindInstance(Transform container, GameObject prefab)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab) return child;
            }
            return null;
        }

        // The prop's renderer bounds in the container's space; a 10 cm cube when it has none.
        private static Bounds PropBounds(Transform container, Transform instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            Bounds local = new Bounds(container.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? world.min.x : world.max.x,
                        (corner & 2) == 0 ? world.min.y : world.max.y,
                        (corner & 4) == 0 ? world.min.z : world.max.z);
                    local.Encapsulate(container.InverseTransformPoint(point));
                }
            }
            return local;
        }

        private static Transform Create(string name, Transform parent, int layer, string undoName)
        {
            return ConstraintToolObjects.CreateEmpty(name, parent, layer, undoName);
        }

        private static void SetLocalPose(Transform target, Vector3 position, Quaternion rotation)
        {
            Undo.RecordObject(target, "Insertable prop");
            target.localPosition = position;
            target.localRotation = rotation;
        }

        private static T AddConstraint<T>(Transform target, Transform[] sources, float[] weights) where T : VRCConstraintBase
        {
            var constraint = Undo.AddComponent<T>(target.gameObject);
            ConstraintToolSources.AddSources(constraint, sources, weights);
            ConstraintToolSources.ApplyDefaults(constraint);
            constraint.FreezeToWorld = false;
            return constraint;
        }

        private static void SetSourceOffset(VRCConstraintBase constraint, int index, Vector3 position, Vector3 euler)
        {
            var sources = constraint.Sources;
            VRCConstraintSource source = sources[index];
            source.ParentPositionOffset = position;
            source.ParentRotationOffset = euler;
            sources[index] = source;
            constraint.Sources = sources;
        }

        // SmoothingFactor only exists from SDK 3.10.6, so it's set by name.
        private static void SetSmoothing(VRCConstraintBase constraint, float value)
        {
            SetField(constraint, "SmoothingFactor", value);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                RiryLog.Warn(RiryLog.ConstraintTool, "Insertable prop: " + target.GetType().Name + "." + name +
                    " was not found in this SDK, so it was left at its default.", target as UnityEngine.Object);
                return;
            }
            field.SetValue(target, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, value) : value);
        }

        private static void SetProperty(SerializedObject serialized, string name, int value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                // MissingSdkFeature checks these; this only happens when the SDK serializes a field under another name.
                RiryLog.Warn(RiryLog.ConstraintTool, "Insertable prop: " + serialized.targetObject.GetType().Name + "." + name +
                    " was not found in this SDK, so it was left at its default.", serialized.targetObject);
                return;
            }
            if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = value;
            else property.intValue = value;
        }

        private static void RemoveConstraints(Transform target)
        {
            foreach (VRCConstraintBase constraint in target.GetComponents<VRCConstraintBase>()) Undo.DestroyObjectImmediate(constraint);
        }

        private static void RemoveComponents<T>(Transform target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Undo.DestroyObjectImmediate(component);
        }

        private static void Dirty(UnityEngine.Object target)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
        }

        /// <summary>Removes the pair's scene objects: IP_&lt;id&gt; and its Seat, Opening and Entry on the socket.</summary>
        internal static int Remove(Transform avatarRoot, Transform socket, string id)
        {
            int removed = 0;
            var targets = new List<Transform> { FindRoot(avatarRoot, id), FindOnSocket(socket, id, "Seat"), FindOnSocket(socket, id, "Opening"),
                FindOnSocket(socket, id, "Entry") };
            foreach (Transform target in targets)
            {
                if (target == null) continue;
                Undo.DestroyObjectImmediate(target.gameObject);
                removed++;
            }
            return removed;
        }

        /// <summary>Counts of what the pair adds to the avatar, for the window's report.</summary>
        internal static string Report(Transform avatarRoot, Transform socket, string id)
        {
            var roots = new List<Transform> { FindRoot(avatarRoot, id), FindOnSocket(socket, id, "Seat"), FindOnSocket(socket, id, "Opening"),
                FindOnSocket(socket, id, "Entry") };
            int constraints = 0, contacts = 0, bones = 0, bodies = 0, joints = 0, rays = 0;
            Type rayType = Type.GetType(RaycastType);
            foreach (Transform root in roots)
            {
                if (root == null) continue;
                constraints += root.GetComponentsInChildren<VRCConstraintBase>(true).Length;
                contacts += root.GetComponentsInChildren<ContactBase>(true).Length;
                bones += root.GetComponentsInChildren<VRCPhysBoneBase>(true).Length;
                bodies += root.GetComponentsInChildren<Rigidbody>(true).Length;
                joints += root.GetComponentsInChildren<Joint>(true).Length;
                if (rayType != null) rays += root.GetComponentsInChildren(rayType, true).Length;
            }
            if (roots[0] == null) return null;
            return constraints + " constraints, " + contacts + " contacts, " + bones + " PhysBone, " + bodies + " Rigidbodies, " +
                joints + " joint, " + rays + " raycast";
        }
    }
}

// ============================================================================
// GLOSSARY: Insertable prop scene rig
// ============================================================================
//
// InsertablePropRig                Builds one pair under the avatar root; called by InsertablePropWindow.Generate.
//   IP_<id> (RootPrefix + PairId)  The pair root. PairId: avatar name + "_" + prop name, non-alphanumerics as "_".
//     Prop                         Container with a VRC Parent Constraint: 0 Seat, 1 Entry, 2 Hand (grip-inverse offset),
//                                  3 Body (PC) or Lying (Android). Holds the prefab instance (never unpacked).
//       <instance>/IP Grip, IP Tip Handles. Tip has the VRC Contact Sender (TipRadius 0.01, tag IP_<id>_Tip).
//     Rig/Anchor                   PhysBone root, AnchorLength from the grip; Parent Constraint to SeatGrip or the drop grip;
//                                  frozen in world space while grabbed. VRCPhysBone: Is Animated, Grab Movement 1,
//                                  Max Stretch 5, Snap To Hand, radius only at the end (GrabRadius), grab filter self/others.
//     Rig/Anchor/End               Pulled to the grip by a Position Constraint while not grabbed.
//     Rig/GripSense, Marker        Grip receiver (GripReceiverRadius, FingerLittleL/R, advanced parameters SenderType,
//                                  SenderLocalPosition, PointSeparation, CollisionTags); Marker is placed by the animator.
//     Rig/Frame Remote|Wearer L|Wearer R|Sheath  At the end, each aiming the grip at a little finger (-Y), or the tip
//                                  at the opening (+Y, Sheath); up +Z from world up or the wearer's hand.
//     Rig/HandFrame                Rotation Constraint over the four frames (weights by the Hand layer), Grip smoothing.
//     Rig/Hand                     Rotation Constraint: HandFrame or Frame Sheath (by sheath proximity).
//     World (PC)                   Follows IP_<id>; frozen while held or dropped. Body: Rigidbody (BodyMass), BoxCollider from
//                                  the prop's bounds, layer 13 (PickupLayer), ConfigurableJoint (stiffness, critical damping)
//                                  to Follower (FollowerMass, no collider; follows Seat or Hand).
//     Drop (Android)               RayOrigin (VRCRaycast down, Hit Worlds, result Landed), DropYaw (Landed + hand yaw),
//                                  Lying (InsertablePropMath.LyingPose), Grip.
//   Socket children                IP_<id> Seat (+Grip), IP_<id> Opening (sheath receiver, captureRadius; forward = axis),
//                                  IP_<id> Entry (InsertablePropMath.EntryPosition).
//   MissingSdkFeature()            Null with SDK 3.10.6 or newer, else what's missing (VRCRaycast fields included,
//                                  RaycastFields). SetField / SetProperty warn when a field is still not found.
//   AvatarError(animator)          Needs a humanoid with hands and little and middle proximal fingers.
//   Remove / Report                The pair's scene objects; counts for the window.
