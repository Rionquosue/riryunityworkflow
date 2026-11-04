using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The animator side of one insertable prop pair: its parameters and four layers, appended to a
    /// controller, its synced state appended to an Expression Parameters asset, and its clips saved
    /// in Assets/RiryTools/InsertableProps/IP_&lt;id&gt;_&lt;controller&gt; (one folder per pair and controller, so
    /// avatars of the same name with their own controllers never share clips). Everything is named
    /// IP_&lt;id&gt;, so Generate replaces this pair's layers, parameters and clips and touches nothing else;
    /// the window asks first when the controller has the pair's layers but the avatar has no rig for them.
    ///   Decide (wearer only, IsLocal): Seated, Held, Inserting, Dropped, written to the synced State.
    ///   Pose (everyone): which source the prop, the PhysBone and the bodies follow, from IsGrabbed and State.
    ///   Hand (everyone): which hand frame is used, from the grip receiver.
    ///   Blend (everyone): places the little-finger marker, and blends in the aim at the sheath by proximity.
    /// </summary>
    internal static class InsertablePropAnimator
    {
        internal const string Grip = "Grip";
        internal const string Sheath = "Sheath";
        internal const string StateName = "State";
        internal const string OneName = "One";
        internal const string IsLocal = "IsLocal";
        internal const string ClipFolder = "InsertableProps";
        // Below this separation (meters) the grip reading is too weak; PointSeparation is -1 with no finger.
        internal const float WeakSeparation = 0.01f;
        // Above this sheath proximity the tip is inside the capture radius.
        internal const float InsideSheath = 0.001f;

        internal const int Seated = 0, Held = 1, Inserting = 2, Dropped = 3;
        private static readonly string[] Layers = { "Decide", "Pose", "Hand", "Blend" };

        internal static string Parameter(string id, string suffix)
        {
            return InsertablePropRig.RootPrefix + id + (string.IsNullOrEmpty(suffix) ? "" : "_" + suffix);
        }

        private static string LayerName(string id, string layer)
        {
            return InsertablePropRig.RootPrefix + id + " " + layer;
        }

        /// <summary>Every animator parameter of the pair, with its type.</summary>
        internal static Dictionary<string, AnimatorControllerParameterType> Parameters(string id)
        {
            string grip = Parameter(id, Grip);
            return new Dictionary<string, AnimatorControllerParameterType>
            {
                { Parameter(id, "IsGrabbed"), AnimatorControllerParameterType.Bool },
                { grip + "_SenderType", AnimatorControllerParameterType.Int },
                { grip + "_SenderLocalPositionX", AnimatorControllerParameterType.Float },
                { grip + "_SenderLocalPositionY", AnimatorControllerParameterType.Float },
                { grip + "_SenderLocalPositionZ", AnimatorControllerParameterType.Float },
                { grip + "_PointSeparation", AnimatorControllerParameterType.Float },
                { grip + "_CollisionTag_FingerLittleL", AnimatorControllerParameterType.Bool },
                { grip + "_CollisionTag_FingerLittleR", AnimatorControllerParameterType.Bool },
                { Parameter(id, Sheath), AnimatorControllerParameterType.Float },
                { Parameter(id, StateName), AnimatorControllerParameterType.Int },
                { Parameter(id, OneName), AnimatorControllerParameterType.Float }
            };
        }

        /// <summary>The controller the pair goes into: the one chosen, else the avatar's FX controller.</summary>
        internal static AnimatorController TargetController(AnimatorController chosen, VRCAvatarDescriptor avatar)
        {
            if (chosen != null) return chosen;
            if (avatar == null || avatar.baseAnimationLayers == null) return null;
            foreach (VRCAvatarDescriptor.CustomAnimLayer layer in avatar.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                    return layer.animatorController as AnimatorController;
            }
            return null;
        }

        internal static VRCExpressionParameters TargetParameters(VRCExpressionParameters chosen, VRCAvatarDescriptor avatar)
        {
            return chosen != null ? chosen : avatar != null ? avatar.expressionParameters : null;
        }

        /// <summary>Appends the pair to the controller and parameters, replacing an earlier run of it.</summary>
        internal static void Apply(InsertablePropRig.Rig rig, AnimatorController controller, VRCExpressionParameters expressions, string undoName)
        {
            string id = rig.Id;
            Undo.RegisterCompleteObjectUndo(controller, undoName);
            RemoveLayers(controller, id);
            foreach (KeyValuePair<string, AnimatorControllerParameterType> parameter in Parameters(id)) EnsureParameter(controller, parameter.Key, parameter.Value);
            EnsureParameter(controller, IsLocal, AnimatorControllerParameterType.Bool);
            SetDefault(controller, Parameter(id, OneName), 1f);
            bool writeDefaults = WritesDefaults(controller);
            string folder = ClipFolderFor(id, controller);

            var clips = new Clips(rig, folder);
            BuildDecide(controller, rig, clips, writeDefaults);
            BuildPose(controller, rig, clips, writeDefaults);
            BuildHand(controller, rig, clips, writeDefaults);
            BuildBlend(controller, rig, clips, writeDefaults);
            EditorUtility.SetDirty(controller);

            Undo.RecordObject(expressions, undoName);
            var list = (expressions.parameters ?? new VRCExpressionParameters.Parameter[0]).Where(p => p == null || p.name != Parameter(id, StateName)).ToList();
            list.Add(new VRCExpressionParameters.Parameter
            {
                name = Parameter(id, StateName),
                valueType = VRCExpressionParameters.ValueType.Int,
                saved = false,
                defaultValue = Seated,
                networkSynced = true
            });
            expressions.parameters = list.ToArray();
            EditorUtility.SetDirty(expressions);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Removes the pair's layers, parameters and synced State. Clips stay unless the window deletes the folder.</summary>
        internal static void Remove(string id, AnimatorController controller, VRCExpressionParameters expressions, string undoName)
        {
            if (controller != null)
            {
                Undo.RegisterCompleteObjectUndo(controller, undoName);
                RemoveLayers(controller, id);
                foreach (string name in Parameters(id).Keys)
                {
                    int index = Array.FindIndex(controller.parameters, p => p.name == name);
                    if (index >= 0) controller.RemoveParameter(index);
                }
                EditorUtility.SetDirty(controller);
            }
            if (expressions != null && expressions.parameters != null)
            {
                Undo.RecordObject(expressions, undoName);
                expressions.parameters = expressions.parameters.Where(p => p == null || p.name != Parameter(id, StateName)).ToArray();
                EditorUtility.SetDirty(expressions);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Synced bits the parameters asset uses, through the SDK's own count when it has one.</summary>
        internal static int SyncedCost(VRCExpressionParameters expressions)
        {
            if (expressions == null) return 0;
            var method = typeof(VRCExpressionParameters).GetMethod("CalcTotalCost");
            if (method != null) return (int)method.Invoke(expressions, null);
            int cost = 0;
            foreach (VRCExpressionParameters.Parameter p in expressions.parameters ?? new VRCExpressionParameters.Parameter[0])
            {
                if (p == null || !p.networkSynced) continue;
                cost += p.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
            return cost;
        }

        /// <summary>
        /// The pair's clip folder for that controller, not created: IP_&lt;id&gt;_ and the first 8 characters of the
        /// controller's GUID, so two avatars of the same name with their own controllers never share clips.
        /// </summary>
        internal static string ClipFolderPath(string id, AnimatorController controller)
        {
            string guid = controller != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller)) : "";
            return RiryToolsProjectFolder.AssetPath + "/" + ClipFolder + "/" + FolderName(id, guid);
        }

        private static string FolderName(string id, string controllerGuid)
        {
            return InsertablePropRig.RootPrefix + id + "_" + (controllerGuid.Length >= 8 ? controllerGuid.Substring(0, 8) : "unsaved");
        }

        internal static string ClipFolderFor(string id, AnimatorController controller)
        {
            string root = RiryToolsProjectFolder.Ensure();
            string props = root + "/" + ClipFolder;
            if (!AssetDatabase.IsValidFolder(props)) AssetDatabase.CreateFolder(root, ClipFolder);
            string folder = ClipFolderPath(id, controller);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(props, folder.Substring(props.Length + 1));
            return folder;
        }

        /// <summary>Whether the controller already has any of the pair's layers.</summary>
        internal static bool HasLayers(AnimatorController controller, string id)
        {
            if (controller == null) return false;
            var names = new HashSet<string>(Layers.Select(layer => LayerName(id, layer)));
            return controller.layers.Any(layer => names.Contains(layer.name));
        }

        private static void RemoveLayers(AnimatorController controller, string id)
        {
            var names = new HashSet<string>(Layers.Select(layer => LayerName(id, layer)));
            for (int i = controller.layers.Length - 1; i >= 0; i--)
            {
                if (names.Contains(controller.layers[i].name)) controller.RemoveLayer(i);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(name, type);
        }

        private static void SetDefault(AnimatorController controller, string name, float value)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (parameter.name == name) parameter.defaultFloat = value;
            }
            controller.parameters = parameters;
        }

        // Matches the avatar's existing Write Defaults setting: what most of its states use.
        private static bool WritesDefaults(AnimatorController controller)
        {
            int on = 0, off = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.writeDefaultValues) on++;
                    else off++;
                }
            }
            return on > off;
        }

        private static AnimatorStateMachine AddLayer(AnimatorController controller, string name)
        {
            var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = name, defaultWeight = 1f, stateMachine = machine });
            return machine;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, bool writeDefaults, int column, int row)
        {
            AnimatorState state = machine.AddState(name, new Vector3(260f * column, 70f * row, 0f));
            state.motion = motion;
            state.writeDefaultValues = writeDefaults;
            return state;
        }

        private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, params Action<AnimatorStateTransition>[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            foreach (Action<AnimatorStateTransition> condition in conditions) condition(transition);
            return transition;
        }

        private static AnimatorStateTransition AfterClip(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
            return transition;
        }

        private static Action<AnimatorStateTransition> If(string name, bool value)
        {
            return t => t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);
        }

        private static Action<AnimatorStateTransition> Is(string name, AnimatorConditionMode mode, float threshold)
        {
            return t => t.AddCondition(mode, threshold, name);
        }

        // Decide: only the wearer's client writes State; everyone else follows it.
        private static void BuildDecide(AnimatorController controller, InsertablePropRig.Rig rig, Clips clips, bool writeDefaults)
        {
            string id = rig.Id;
            string grabbed = Parameter(id, "IsGrabbed");
            AnimatorStateMachine machine = AddLayer(controller, LayerName(id, "Decide"));
            AnimatorState start = State(machine, "Start", null, writeDefaults, 0, 0);
            AnimatorState seated = Driven(machine, "Seated", id, Seated, null, writeDefaults, 1, 0);
            AnimatorState held = Driven(machine, "Held", id, Held, null, writeDefaults, 2, 0);
            AnimatorState inserting = Driven(machine, "Inserting", id, Inserting, clips.Wait, writeDefaults, 3, 0);
            AnimatorState dropped = Driven(machine, "Dropped", id, Dropped, null, writeDefaults, 3, 1);
            machine.defaultState = start;
            Go(start, seated, If(IsLocal, true));
            Go(seated, held, If(grabbed, true));
            Go(dropped, held, If(grabbed, true));
            Go(inserting, held, If(grabbed, true));
            // Released with the tip inside the radius, or while the grip reading was too weak: insert.
            Go(held, inserting, If(grabbed, false), Is(Parameter(id, Sheath), AnimatorConditionMode.Greater, InsideSheath));
            Go(held, inserting, If(grabbed, false), Is(Parameter(id, Grip) + "_PointSeparation", AnimatorConditionMode.Less, WeakSeparation));
            Go(held, dropped, If(grabbed, false));
            AfterClip(inserting, seated);
        }

        private static AnimatorState Driven(AnimatorStateMachine machine, string name, string id, int value, Motion motion, bool writeDefaults, int column, int row)
        {
            AnimatorState state = State(machine, name, motion, writeDefaults, column, row);
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new VRC_AvatarParameterDriver.Parameter { type = VRC_AvatarParameterDriver.ChangeType.Set, name = Parameter(id, StateName), value = value }
            };
            return state;
        }

        // Pose: everyone. Held follows the local IsGrabbed at once; after release it waits for the wearer's State.
        private static void BuildPose(AnimatorController controller, InsertablePropRig.Rig rig, Clips clips, bool writeDefaults)
        {
            string id = rig.Id;
            string grabbed = Parameter(id, "IsGrabbed");
            string state = Parameter(id, StateName);
            AnimatorStateMachine machine = AddLayer(controller, LayerName(id, "Pose"));
            AnimatorState seated = State(machine, "Seated", clips.Seated, writeDefaults, 0, 0);
            AnimatorState held = State(machine, "Held", clips.Held, writeDefaults, 1, 0);
            AnimatorState waiting = State(machine, "Waiting", clips.Held, writeDefaults, 2, 0);
            AnimatorState entry = State(machine, "Insert entry", clips.InsertEntry, writeDefaults, 3, 0);
            AnimatorState seat = State(machine, "Insert seat", clips.InsertSeat, writeDefaults, 3, 1);
            AnimatorState dropped = State(machine, "Dropped", clips.Dropped, writeDefaults, 2, 1);
            machine.defaultState = seated;
            foreach (AnimatorState from in new[] { seated, waiting, entry, seat, dropped }) Go(from, held, If(grabbed, true));
            Go(held, waiting, If(grabbed, false));
            Go(waiting, entry, Is(state, AnimatorConditionMode.Equals, Inserting));
            Go(waiting, dropped, Is(state, AnimatorConditionMode.Equals, Dropped));
            Go(waiting, seated, Is(state, AnimatorConditionMode.Equals, Seated));
            AfterClip(entry, seat);
            AfterClip(seat, seated);
            // Someone joining after a drop sees it dropped.
            Go(seated, dropped, If(grabbed, false), Is(state, AnimatorConditionMode.Equals, Dropped));
        }

        // Hand: which hand frame the prop's rotation comes from.
        private static void BuildHand(AnimatorController controller, InsertablePropRig.Rig rig, Clips clips, bool writeDefaults)
        {
            string grip = Parameter(rig.Id, Grip);
            AnimatorStateMachine machine = AddLayer(controller, LayerName(rig.Id, "Hand"));
            AnimatorState weak = State(machine, "Weak (aim at sheath)", clips.HandWeak, writeDefaults, 0, 0);
            AnimatorState remote = State(machine, "Remote", clips.HandRemote, writeDefaults, 1, 0);
            AnimatorState left = State(machine, "Wearer L", clips.HandWearerL, writeDefaults, 1, 1);
            AnimatorState right = State(machine, "Wearer R", clips.HandWearerR, writeDefaults, 1, 2);
            machine.defaultState = weak;
            Any(machine, weak, Is(grip + "_PointSeparation", AnimatorConditionMode.Less, WeakSeparation));
            Any(machine, left, Is(grip + "_SenderType", AnimatorConditionMode.Equals, 1), If(grip + "_CollisionTag_FingerLittleL", true),
                Is(grip + "_PointSeparation", AnimatorConditionMode.Greater, WeakSeparation));
            Any(machine, right, Is(grip + "_SenderType", AnimatorConditionMode.Equals, 1), If(grip + "_CollisionTag_FingerLittleR", true),
                Is(grip + "_PointSeparation", AnimatorConditionMode.Greater, WeakSeparation));
            Any(machine, remote, Is(grip + "_SenderType", AnimatorConditionMode.Greater, 1),
                Is(grip + "_PointSeparation", AnimatorConditionMode.Greater, WeakSeparation));
        }

        private static void Any(AnimatorStateMachine machine, AnimatorState to, params Action<AnimatorStateTransition>[] conditions)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            foreach (Action<AnimatorStateTransition> condition in conditions) condition(transition);
        }

        // Blend: the marker follows the little finger's reported position; the hand blends to the sheath by proximity.
        private static void BuildBlend(AnimatorController controller, InsertablePropRig.Rig rig, Clips clips, bool writeDefaults)
        {
            string id = rig.Id;
            string grip = Parameter(id, Grip);
            string one = Parameter(id, OneName);
            AnimatorStateMachine machine = AddLayer(controller, LayerName(id, "Blend"));
            var direct = new BlendTree { name = LayerName(id, "Blend"), blendType = BlendTreeType.Direct, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(direct, controller);
            float r = InsertablePropRig.GripReceiverRadius;
            var children = new List<ChildMotion>();
            for (int axis = 0; axis < 3; axis++)
            {
                string parameter = grip + "_SenderLocalPosition" + "XYZ"[axis];
                children.Add(new ChildMotion { motion = Linear(controller, parameter, -r, clips.MarkerLow[axis], r, clips.MarkerHigh[axis]), directBlendParameter = one, timeScale = 1f });
            }
            children.Add(new ChildMotion { motion = Linear(controller, Parameter(id, Sheath), 0f, clips.HandAimHand, 1f, clips.HandAimSheath), directBlendParameter = one, timeScale = 1f });
            direct.children = children.ToArray();
            AnimatorState state = State(machine, "Blend", direct, writeDefaults, 0, 0);
            machine.defaultState = state;
        }

        private static BlendTree Linear(AnimatorController controller, string parameter, float low, Motion lowMotion, float high, Motion highMotion)
        {
            var tree = new BlendTree { name = parameter, blendType = BlendTreeType.Simple1D, blendParameter = parameter, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                new ChildMotion { motion = lowMotion, threshold = low, timeScale = 1f },
                new ChildMotion { motion = highMotion, threshold = high, timeScale = 1f }
            };
            return tree;
        }

        /// <summary>The pair's clips, each setting every property its layer controls, saved in the pair's folder.</summary>
        private sealed class Clips
        {
            public AnimationClip Seated, Held, InsertEntry, InsertSeat, Dropped, Wait;
            public AnimationClip HandWeak, HandRemote, HandWearerL, HandWearerR, HandAimHand, HandAimSheath;
            public readonly AnimationClip[] MarkerLow = new AnimationClip[3];
            public readonly AnimationClip[] MarkerHigh = new AnimationClip[3];
            private readonly InsertablePropRig.Rig rig;
            private readonly string folder;

            public Clips(InsertablePropRig.Rig rig, string folder)
            {
                this.rig = rig;
                this.folder = folder;
                float step = Mathf.Max(1f / 60f, InsertablePropMath.SmoothedDuration(rig.Smoothing, InsertablePropMath.SettledFraction));
                Seated = Pose("Seated", Seat: 1, smoothing: 0f, length: 1f / 60f, onSeat: true, frozen: false, held: false, dynamicBody: false);
                Held = Pose("Held", Hand: 1, smoothing: 0f, length: 1f / 60f, onSeat: true, frozen: true, held: true, dynamicBody: true);
                InsertEntry = Pose("Insert entry", Entry: 1, smoothing: rig.Smoothing, length: step, onSeat: true, frozen: false, held: false, dynamicBody: false);
                InsertSeat = Pose("Insert seat", Seat: 1, smoothing: rig.Smoothing, length: step, onSeat: true, frozen: false, held: false, dynamicBody: false);
                Dropped = Pose("Dropped", Drop: 1, smoothing: rig.Android ? rig.Smoothing : 0f, length: 1f / 60f, onSeat: false, frozen: true, held: false, dynamicBody: true);

                // Decide's Inserting lasts both slide steps; it needs a curve to have a length.
                Wait = new AnimationClip();
                Set(Wait, rig.Root, typeof(GameObject), "m_IsActive", 1f, step * 2f);
                Wait = Save("Wait", Wait);

                HandWeak = HandFrame("Hand weak", 3);
                HandRemote = HandFrame("Hand remote", 0);
                HandWearerL = HandFrame("Hand wearer L", 1);
                HandWearerR = HandFrame("Hand wearer R", 2);
                HandAimHand = HandAim("Aim hand", 0f);
                HandAimSheath = HandAim("Aim sheath", 1f);
                float r = InsertablePropRig.GripReceiverRadius;
                for (int axis = 0; axis < 3; axis++)
                {
                    string property = "m_LocalPosition." + "xyz"[axis];
                    MarkerLow[axis] = Single("Marker " + "XYZ"[axis] + " low", rig.Marker, typeof(Transform), property, -r);
                    MarkerHigh[axis] = Single("Marker " + "XYZ"[axis] + " high", rig.Marker, typeof(Transform), property, r);
                }
            }

            // One Pose state's clip: the prop's source weights and smoothing, the PhysBone's targets and freeze,
            // and the PC bodies or Android drop parts.
            private AnimationClip Pose(string name, float Seat = 0f, float Entry = 0f, float Hand = 0f, float Drop = 0f, float smoothing = 0f,
                float length = 1f / 60f, bool onSeat = true, bool frozen = false, bool held = false, bool dynamicBody = false)
            {
                var clip = new AnimationClip();
                Type parent = typeof(VRCParentConstraint);
                Type position = typeof(VRCPositionConstraint);
                Type rotation = typeof(VRCRotationConstraint);
                float[] prop = { Seat, Entry, Hand, Drop };
                for (int s = 0; s < 4; s++) Set(clip, rig.Prop, parent, ConstraintTrainPlanner.SourceWeightProperty(s), prop[s], length);
                Set(clip, rig.Prop, parent, "SmoothingFactor", smoothing, length);
                // The PhysBone's root and end: at the seat's grip, or the dropped prop's.
                float seat = onSeat ? 1f : 0f;
                Set(clip, rig.Anchor, parent, ConstraintTrainPlanner.SourceWeightProperty(0), seat, length);
                Set(clip, rig.Anchor, parent, ConstraintTrainPlanner.SourceWeightProperty(1), 1f - seat, length);
                Set(clip, rig.Anchor, parent, "FreezeToWorld", held ? 1f : 0f, length);
                Set(clip, rig.End, position, ConstraintTrainPlanner.SourceWeightProperty(0), seat, length);
                Set(clip, rig.End, position, ConstraintTrainPlanner.SourceWeightProperty(1), 1f - seat, length);
                Set(clip, rig.End, position, "IsActive", held ? 0f : 1f, length);
                if (rig.World != null)
                {
                    Set(clip, rig.World, parent, "FreezeToWorld", frozen ? 1f : 0f, length);
                    Set(clip, rig.Body, typeof(Rigidbody), "m_IsKinematic", dynamicBody ? 0f : 1f, length);
                    Set(clip, rig.Body, parent, "IsActive", dynamicBody ? 0f : 1f, length);
                    // The follower stays on the hand while held, and is set free with the body on a drop.
                    bool followerFree = dynamicBody && !held;
                    Set(clip, rig.Follower, typeof(Rigidbody), "m_IsKinematic", followerFree ? 0f : 1f, length);
                    Set(clip, rig.Follower, parent, "IsActive", followerFree ? 0f : 1f, length);
                    Set(clip, rig.Follower, parent, ConstraintTrainPlanner.SourceWeightProperty(0), held ? 0f : 1f, length);
                    Set(clip, rig.Follower, parent, ConstraintTrainPlanner.SourceWeightProperty(1), held ? 1f : 0f, length);
                }
                if (rig.Drop != null)
                {
                    // Android: the ray origin and the yaw stop following the hand on a drop.
                    bool drop = Drop > 0f;
                    Set(clip, rig.RayOrigin, position, "FreezeToWorld", drop ? 1f : 0f, length);
                    Set(clip, rig.DropYaw, rotation, "FreezeToWorld", drop ? 1f : 0f, length);
                }
                return Save(name, clip);
            }

            private AnimationClip HandFrame(string name, int source)
            {
                var clip = new AnimationClip();
                for (int s = 0; s < 4; s++) Set(clip, rig.HandFrame, typeof(VRCRotationConstraint), ConstraintTrainPlanner.SourceWeightProperty(s), s == source ? 1f : 0f, 1f / 60f);
                return Save(name, clip);
            }

            private AnimationClip HandAim(string name, float sheath)
            {
                var clip = new AnimationClip();
                Set(clip, rig.Hand, typeof(VRCRotationConstraint), ConstraintTrainPlanner.SourceWeightProperty(0), 1f - sheath, 1f / 60f);
                Set(clip, rig.Hand, typeof(VRCRotationConstraint), ConstraintTrainPlanner.SourceWeightProperty(1), sheath, 1f / 60f);
                return Save(name, clip);
            }

            private AnimationClip Single(string name, Transform target, Type type, string property, float value)
            {
                var clip = new AnimationClip();
                Set(clip, target, type, property, value, 1f / 60f);
                return Save(name, clip);
            }

            private void Set(AnimationClip clip, Transform target, Type type, string property, float value, float length)
            {
                string path = AnimationUtility.CalculateTransformPath(target, rig.AvatarRoot);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), AnimationCurve.Constant(0f, length, value));
            }

            // Replaces the clip's contents in place when it exists, so its GUID and references stay.
            private AnimationClip Save(string name, AnimationClip clip)
            {
                clip.name = InsertablePropRig.RootPrefix + rig.Id + " " + name;
                string path = folder + "/" + clip.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clip, path);
                    return clip;
                }
                Undo.RegisterCompleteObjectUndo(existing, "Insertable prop");
                EditorUtility.CopySerialized(clip, existing);
                existing.name = clip.name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Insertable prop animator
// ============================================================================
//
// InsertablePropAnimator           Appends one pair to a controller and an Expression Parameters asset.
//   Parameter(id, suffix)          "IP_<id>_<suffix>"; the PhysBone's is "IP_<id>" (IP_<id>_IsGrabbed), the grip receiver's
//                                  "IP_<id>_Grip" (_SenderType, _SenderLocalPositionX/Y/Z, _PointSeparation,
//                                  _CollisionTag_FingerLittleL/R), the sheath's "IP_<id>_Sheath", the synced "IP_<id>_State"
//                                  (Seated 0, Held 1, Inserting 2, Dropped 3) and "IP_<id>_One" (1, Direct blend weights).
//   TargetController / TargetParameters  The asset chosen in the window, else the avatar's FX controller / parameters.
//   Layers "IP_<id> Decide|Pose|Hand|Blend"
//     Decide                       Wearer only (IsLocal). Release inserts when the sheath proximity is above InsideSheath
//                                  (0.001) or the grip separation is under WeakSeparation (0.01 m); otherwise drops.
//                                  VRC Avatar Parameter Drivers (local only) write State.
//     Pose                         Everyone: Seated, Held (IsGrabbed), Waiting (released, waiting for State), Insert entry and
//                                  Insert seat (Smoothing Factor = Insert smoothing, each SmoothedDuration to 99%), Dropped.
//     Hand                         Everyone: Weak (aim at sheath), Remote, Wearer L, Wearer R, from the grip receiver.
//     Blend                        Everyone: Direct tree (weights IP_<id>_One) placing the marker from SenderLocalPosition
//                                  (±GripReceiverRadius) and blending the hand to the sheath aim by sheath proximity.
//   WritesDefaults                 Generated states copy what most of the controller's states use.
//   Clips                          Saved in ClipFolderPath: Assets/RiryTools/InsertableProps/IP_<id>_<first 8 of the controller
//                                  GUID>/ ("IP_<id> <name>.anim"); replaced in place. One folder per pair and controller.
//   HasLayers(controller, id)      The controller has some of the pair's layers (the window asks before replacing them when
//                                  this avatar has no rig for the pair: another avatar of the same name uses them).
//   Remove(...)                    Removes the layers, parameters and synced State; the clips folder is the window's call.
//   SyncedCost(expressions)        The SDK's CalcTotalCost when present, else 8 per synced Int/Float and 1 per Bool.
