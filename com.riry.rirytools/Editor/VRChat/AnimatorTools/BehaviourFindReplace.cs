using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Animator Tools > Find and Replace, without its window (Editor/VRChat/UI/AnimatorTools/BehaviourFindReplaceWindow.cs): finds the
    /// strings of state behaviours (case-insensitive, FuzzyMatch "contains", or a regular expression) in an avatar's controllers (its playable
    /// layers, VRCFury Full Controllers and Modular Avatar Merge Animators, as the Menu Tool's scan finds them) or one
    /// animator controller, and shows a found state in the Animator window. Apply is BehaviourFindReplaceApply.
    /// </summary>
    internal static class BehaviourFindReplace
    {
        internal enum Kind
        {
            Other,
            PlayAudio,
            Driver
        }

        /// <summary>How Search finds strings and Apply replaces them.</summary>
        internal enum Match
        {
            // Finds strings containing the search (FuzzyMatch "contains"); Apply replaces the whole string.
            Whole,
            // Finds the same strings; Apply replaces only each occurrence of the search (case-insensitive).
            Partial,
            // Finds strings the regular expression matches (case-insensitive); Apply replaces each match ($1 and so on work).
            Regex
        }

        // Longest a regular expression may run on one string.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        internal sealed class Hit
        {
            internal AnimatorController Controller;
            internal int Layer;
            // The layer's state machine down to the one holding the owner.
            internal List<AnimatorStateMachine> Machines = new List<AnimatorStateMachine>();
            // The AnimatorState or AnimatorStateMachine the behaviour is on.
            internal Object Owner;
            internal StateMachineBehaviour Behaviour;
            internal string Property = "";
            internal string Value = "";
            internal Kind Kind;
            // What Play Audio paths are relative to; null for a controller asset.
            internal Transform Root;
            // The component that uses the controller (descriptor, VRCFury, Modular Avatar); null for a controller asset.
            internal Component Source;
            internal string Label = "";
            // The search and match mode that found it, which Apply's Partial and Regex replace use.
            internal string Search = "";
            internal Match Match;
        }

        internal sealed class Change
        {
            internal Hit Hit;
            internal string Before = "";
            internal string After = "";
        }

        /// <summary>Where one behaviour sits in its controller.</summary>
        internal sealed class Place
        {
            internal int Layer;
            internal Object Owner;
            internal List<AnimatorStateMachine> Machines;
            internal StateMachineBehaviour Behaviour;
        }

        // A controller used by the source: its label, what Play Audio paths are relative to, and the component using it.
        private sealed class Usage
        {
            internal string Label = "";
            internal Transform Root;
            internal Component Source;
        }

        // ---- Search -------------------------------------------------------------------------------

        internal static List<Hit> Find(Object from, string text, Match match = Match.Whole)
        {
            var found = new List<Hit>();
            foreach (KeyValuePair<AnimatorController, Usage> usage in Controllers(from))
            {
                AnimatorController controller = usage.Key;
                AnimatorControllerLayer[] layers = controller.layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].stateMachine == null) continue;
                    Walk(layers[i].stateMachine, new List<AnimatorStateMachine> { layers[i].stateMachine }, (owner, machines, behaviour) =>
                    {
                        if (behaviour == null) return;
                        var serialized = new SerializedObject(behaviour);
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.String || property.name == "m_Name" || property.name == "m_EditorClassIdentifier") continue;
                            string value = property.stringValue ?? "";
                            if (!Matches(value, text, match)) continue;
                            Kind kind = behaviour is VRCAnimatorPlayAudio && property.propertyPath == "SourcePath" ? Kind.PlayAudio
                                : behaviour is VRCAvatarParameterDriver && property.propertyPath.StartsWith("parameters.Array.data[", StringComparison.Ordinal) ? Kind.Driver
                                : Kind.Other;
                            found.Add(new Hit
                            {
                                Controller = controller, Layer = i, Machines = machines, Owner = owner, Behaviour = behaviour,
                                Property = property.propertyPath, Value = value, Kind = kind, Root = usage.Value.Root, Source = usage.Value.Source,
                                Label = usage.Value.Label + " > " + layers[i].name + " > " + owner.name + " > " +
                                    ObjectNames.NicifyVariableName(behaviour.GetType().Name) + " > " + property.displayName,
                                Search = text, Match = match
                            });
                        }
                    }, new HashSet<AnimatorStateMachine>());
                }
            }
            return found;
        }

        /// <summary>Null when the pattern is a valid regular expression, otherwise why it is not.</summary>
        internal static string PatternError(string pattern)
        {
            try
            {
                new System.Text.RegularExpressions.Regex(pattern ?? "", System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexTimeout);
                return null;
            }
            catch (ArgumentException exception)
            {
                return exception.Message;
            }
        }

        private static bool Matches(string value, string text, Match match)
        {
            switch (match)
            {
                case Match.Regex:
                    if (string.IsNullOrEmpty(text)) return false;
                    try { return System.Text.RegularExpressions.Regex.IsMatch(value, text, System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexTimeout); }
                    catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
                default:
                    return FuzzyMatch.Score(value, text) >= 2;
            }
        }

        /// <summary>The string after Apply's replace: the whole string, each occurrence of the search, or each regex match.</summary>
        internal static string Replaced(Hit hit, string value, string replace)
        {
            switch (hit.Match)
            {
                case Match.Partial:
                    if (string.IsNullOrEmpty(hit.Search)) return value;
                    return System.Text.RegularExpressions.Regex.Replace(value, System.Text.RegularExpressions.Regex.Escape(hit.Search),
                        replace.Replace("$", "$$"), System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexTimeout);
                case Match.Regex:
                    return System.Text.RegularExpressions.Regex.Replace(value, hit.Search, replace, System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexTimeout);
                default:
                    return replace;
            }
        }

        private static void Walk(AnimatorStateMachine machine, List<AnimatorStateMachine> machines, Action<Object, List<AnimatorStateMachine>, StateMachineBehaviour> visit,
            HashSet<AnimatorStateMachine> seen)
        {
            if (machine == null || !seen.Add(machine)) return;
            foreach (StateMachineBehaviour behaviour in machine.behaviours) visit(machine, machines, behaviour);
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state == null) continue;
                foreach (StateMachineBehaviour behaviour in child.state.behaviours) visit(child.state, machines, behaviour);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                Walk(child.stateMachine, new List<AnimatorStateMachine>(machines) { child.stateMachine }, visit, seen);
        }

        /// <summary>Every behaviour of a controller, in a fixed order (layers, then each state machine's own, its states', its children).</summary>
        internal static List<Place> Places(AnimatorController controller)
        {
            var places = new List<Place>();
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].stateMachine == null) continue;
                int layer = i;
                Walk(layers[i].stateMachine, new List<AnimatorStateMachine> { layers[i].stateMachine },
                    (owner, machines, behaviour) => places.Add(new Place { Layer = layer, Owner = owner, Machines = machines, Behaviour = behaviour }),
                    new HashSet<AnimatorStateMachine>());
            }
            return places;
        }

        // Each controller of the source once: its label and what Play Audio paths are relative to (null for an asset).
        private static Dictionary<AnimatorController, Usage> Controllers(Object from)
        {
            var controllers = new Dictionary<AnimatorController, Usage>();
            if (from is AnimatorController asset)
            {
                controllers[asset] = new Usage { Label = asset.name };
                return controllers;
            }
            var target = from as GameObject;
            if (target == null) return controllers;
            MenuToolScan scan = MenuToolScanner.Scan(target);
            if (scan == null) return controllers;
            foreach (MenuToolControllerUsage usage in scan.Controllers)
            {
                var controller = usage.Controller as AnimatorController;
                if (controller == null || controllers.ContainsKey(controller)) continue;
                controllers[controller] = new Usage { Label = usage.Name, Root = RootOf(usage.Source, controller), Source = usage.Source as Component };
            }
            return controllers;
        }

        // The avatar for its own layers and Modular Avatar; for VRCFury, the Full Controller's root object (its deprecated
        // root object override when set, else the VRCFury component's object), as VRCFury rewrites paths.
        private static Transform RootOf(Object usageSource, AnimatorController controller)
        {
            var component = usageSource as Component;
            if (component == null) return null;
            if (component is VRCAvatarDescriptor) return component.transform;
            if (component.GetType().FullName == "VF.Model.VRCFury")
            {
                foreach (SerializedProperty feature in MenuToolScanner.Features(new SerializedObject(component)))
                {
                    if (MenuToolScanner.FeatureName(feature) != "FullController") continue;
                    SerializedProperty list = feature.FindPropertyRelative("controllers");
                    bool holds = false;
                    for (int i = 0; list != null && i < list.arraySize; i++)
                    {
                        SerializedProperty slot = list.GetArrayElementAtIndex(i).FindPropertyRelative("controller");
                        SerializedProperty reference = slot != null ? slot.FindPropertyRelative("objRef") ?? slot : null;
                        if (reference != null && reference.propertyType == SerializedPropertyType.ObjectReference && reference.objectReferenceValue == controller) holds = true;
                    }
                    if (!holds) continue;
                    SerializedProperty overrideRoot = feature.FindPropertyRelative("rootObjOverride");
                    var root = overrideRoot != null ? overrideRoot.objectReferenceValue as GameObject : null;
                    return root != null ? root.transform : component.transform;
                }
                return component.transform;
            }
            VRCAvatarDescriptor avatar = MenuToolScanner.DescriptorAbove(component.transform);
            return avatar != null ? avatar.transform : null;
        }

        // ---- Reveal -------------------------------------------------------------------------------

        // Opens the Animator window on the hit's controller, layer and state machine, selects its state (shown in the
        // Inspector) and frames it. The Animator window's members (UnityEditor.Graphs.AnimatorControllerTool) are internal.
        internal static void Reveal(Hit hit)
        {
            if (hit.Controller == null || hit.Owner == null) return;
            EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
            Type toolType = RiryToolsTypes.Named("UnityEditor.Graphs.AnimatorControllerTool");
            var tool = toolType != null ? Resources.FindObjectsOfTypeAll(toolType).FirstOrDefault() as EditorWindow : null;
            if (tool != null)
            {
                const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo controller = toolType.GetProperty("animatorController", members);
                PropertyInfo layer = toolType.GetProperty("selectedLayerIndex", members);
                MethodInfo crumb = toolType.GetMethod("AddBreadCrumb", members, null, new[] { typeof(Object) }, null);
                try
                {
                    if (controller != null && controller.CanWrite) controller.SetValue(tool, hit.Controller);
                    if (layer != null && layer.CanWrite) layer.SetValue(tool, hit.Layer);
                    if (crumb != null) foreach (AnimatorStateMachine machine in hit.Machines.Skip(1)) crumb.Invoke(tool, new object[] { machine });
                }
                catch (Exception exception)
                {
                    RiryLog.Exception(RiryLog.FindReplace, "Could not show " + hit.Owner.name + " in the Animator window", exception, hit.Controller);
                }
            }
            Selection.activeObject = hit.Owner;
            if (tool == null) return;
            EditorApplication.delayCall += () =>
            {
                if (tool == null) return;
                tool.Focus();
                tool.SendEvent(EditorGUIUtility.CommandEvent("FrameSelected"));
            };
        }
    }
}
