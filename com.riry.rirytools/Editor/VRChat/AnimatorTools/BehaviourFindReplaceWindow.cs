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
    /// Animator Tools > Find and Replace: finds the strings of state behaviours (case-insensitive, FuzzyMatch "contains")
    /// in an avatar's controllers (its playable layers, VRCFury Full Controllers and Modular Avatar Merge Animators, as the
    /// Menu Tool's scan finds them) or one animator controller, and replaces whole strings on Apply, as one undo step.
    /// VRC Animator Play Audio paths can be repathed to a scene object (relative to the avatar, or to a VRCFury Full
    /// Controller's root object), and VRC Avatar Parameter Driver names mapped to one of the controller's parameters.
    /// </summary>
    internal sealed class BehaviourFindReplaceWindow : EditorWindow
    {
        private enum Kind
        {
            Other,
            PlayAudio,
            Driver
        }

        private sealed class Hit
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
            internal string Label = "";
        }

        private sealed class Change
        {
            internal Hit Hit;
            internal string Before = "";
            internal string After = "";
        }

        [SerializeField] private Object source;
        [SerializeField] private string search = "";
        [SerializeField] private string replace = "";
        [SerializeField] private GameObject repath;
        [SerializeField] private string map = "";
        [SerializeField] private Vector2 foundScroll;
        [SerializeField] private Vector2 changedScroll;
        [NonSerialized] private List<Hit> hits = new List<Hit>();
        [NonSerialized] private List<Change> changes = new List<Change>();
        [NonSerialized] private bool searched;
        [NonSerialized] private List<string> driverParameters = new List<string>();

        internal static EditorWindow CreateHosted()
        {
            return CreateInstance<BehaviourFindReplaceWindow>();
        }

        /// <summary>The popup, with this source (the right-clicked object or controller, or none).</summary>
        internal static void OpenPopup(Object with)
        {
            var window = CreateInstance<BehaviourFindReplaceWindow>();
            window.titleContent = new GUIContent("Find and Replace");
            window.source = Usable(with) ? with : null;
            window.minSize = new Vector2(460f, 420f);
            window.ShowUtility();
        }

        private static bool Usable(Object value)
        {
            return value is GameObject || value is AnimatorController;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            Object chosen = EditorGUILayout.ObjectField(new GUIContent("Source", "An avatar or an object in one (its playable layers, VRCFury Full Controllers and Modular Avatar Merge Animators), or an animator controller."),
                source, typeof(Object), true);
            if (chosen != source && (chosen == null || Usable(chosen)))
            {
                source = chosen;
                searched = false;
                hits.Clear();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("Search", search);
                if (GUILayout.Button("Search", GUILayout.Width(70f))) Search();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                replace = EditorGUILayout.TextField("Replace with", replace);
                using (new EditorGUI.DisabledScope(!searched || hits.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Apply", "Replaces the whole of each found string, as one undo step."), GUILayout.Width(70f))) Apply();
                }
            }

            if (searched && !(source is AnimatorController) && hits.Any(h => h.Kind == Kind.PlayAudio))
                repath = (GameObject)EditorGUILayout.ObjectField("Repath to scene object:", repath, typeof(GameObject), true);
            if (searched && hits.Any(h => h.Kind == Kind.Driver)) DrawMap();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Found components" + (searched ? " (" + hits.Count + ")" : ""), EditorStyles.boldLabel);
            foundScroll = EditorGUILayout.BeginScrollView(foundScroll, EditorStyles.helpBox, GUILayout.MinHeight(160f));
            if (!searched) EditorGUILayout.LabelField("Search to list behaviour strings.", EditorStyles.miniLabel);
            else if (hits.Count == 0) EditorGUILayout.LabelField("Nothing found.", EditorStyles.miniLabel);
            foreach (Hit hit in hits)
            {
                if (GUILayout.Button(hit.Label + "  =  " + hit.Value, EditorStyles.linkLabel)) Reveal(hit);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField("Changed components", EditorStyles.boldLabel);
            changedScroll = EditorGUILayout.BeginScrollView(changedScroll, EditorStyles.helpBox, GUILayout.MinHeight(160f));
            if (changes.Count == 0) EditorGUILayout.LabelField("The last Apply's changes are listed here.", EditorStyles.miniLabel);
            foreach (Change change in changes)
            {
                if (GUILayout.Button(change.Hit.Label + "  :  " + change.Before + "  →  " + change.After, EditorStyles.linkLabel)) Reveal(change.Hit);
            }
            EditorGUILayout.EndScrollView();
        }

        // Map to parameter: typed text, with the controllers' parameters that match it below while it names none.
        private void DrawMap()
        {
            map = EditorGUILayout.TextField("Map to parameter:", map);
            if (driverParameters.Contains(map)) return;
            foreach (string name in Ranked(map).Take(6))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth);
                    if (GUILayout.Button(name, EditorStyles.miniButtonLeft))
                    {
                        map = name;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private IEnumerable<string> Ranked(string typed)
        {
            return driverParameters.Select(name => new { name, score = FuzzyMatch.Score(name, typed ?? "") })
                .Where(p => p.score > 0).OrderByDescending(p => p.score).ThenBy(p => p.name.Length).ThenBy(p => p.name, StringComparer.Ordinal)
                .Select(p => p.name);
        }

        // ---- Search -------------------------------------------------------------------------------

        private void Search()
        {
            searched = true;
            hits = Find(source, search ?? "");
            driverParameters = hits.Where(h => h.Kind == Kind.Driver).Select(h => h.Controller).Distinct()
                .SelectMany(c => c.parameters.Select(p => p.name)).Distinct().ToList();
            if (driverParameters.Count > 0 && !driverParameters.Contains(map)) map = Ranked(search).FirstOrDefault() ?? "";
            GUI.FocusControl(null);
        }

        private static List<Hit> Find(Object from, string text)
        {
            var found = new List<Hit>();
            foreach (KeyValuePair<AnimatorController, KeyValuePair<string, Transform>> usage in Controllers(from))
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
                            if (FuzzyMatch.Score(value, text) < 2) continue;
                            Kind kind = behaviour is VRCAnimatorPlayAudio && property.propertyPath == "SourcePath" ? Kind.PlayAudio
                                : behaviour is VRCAvatarParameterDriver && property.propertyPath.StartsWith("parameters.Array.data[", StringComparison.Ordinal) ? Kind.Driver
                                : Kind.Other;
                            found.Add(new Hit
                            {
                                Controller = controller, Layer = i, Machines = machines, Owner = owner, Behaviour = behaviour,
                                Property = property.propertyPath, Value = value, Kind = kind, Root = usage.Value.Value,
                                Label = usage.Value.Key + " > " + layers[i].name + " > " + owner.name + " > " +
                                    ObjectNames.NicifyVariableName(behaviour.GetType().Name) + " > " + property.displayName
                            });
                        }
                    }, new HashSet<AnimatorStateMachine>());
                }
            }
            return found;
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

        // Each controller of the source once: its label and what Play Audio paths are relative to (null for an asset).
        private static Dictionary<AnimatorController, KeyValuePair<string, Transform>> Controllers(Object from)
        {
            var controllers = new Dictionary<AnimatorController, KeyValuePair<string, Transform>>();
            if (from is AnimatorController asset)
            {
                controllers[asset] = new KeyValuePair<string, Transform>(asset.name, null);
                return controllers;
            }
            var target = from as GameObject;
            if (target == null) return controllers;
            MenuScan scan = MenuToolScanner.Scan(target);
            if (scan == null) return controllers;
            foreach (ControllerUsage usage in scan.Controllers)
            {
                var controller = usage.Controller as AnimatorController;
                if (controller == null || controllers.ContainsKey(controller)) continue;
                controllers[controller] = new KeyValuePair<string, Transform>(usage.Name, RootOf(usage.Source, controller));
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

        // ---- Apply --------------------------------------------------------------------------------

        // Play Audio paths take the repath object's path when one is set, Parameter Driver names the mapped parameter when
        // one is typed; every other found string becomes Replace with, when it is not empty. One undo step.
        private void Apply()
        {
            var made = new List<Change>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Find and Replace");
            foreach (IGrouping<StateMachineBehaviour, Hit> behaviour in hits.Where(h => h.Behaviour != null).GroupBy(h => h.Behaviour))
            {
                var serialized = new SerializedObject(behaviour.Key);
                foreach (Hit hit in behaviour)
                {
                    string after = NewValue(hit);
                    if (after == null) continue;
                    SerializedProperty property = serialized.FindProperty(hit.Property);
                    if (property == null || property.propertyType != SerializedPropertyType.String || property.stringValue == after) continue;
                    made.Add(new Change { Hit = hit, Before = property.stringValue, After = after });
                    property.stringValue = after;
                }
                serialized.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            if (made.Count == 0) return;
            changes = made;
            foreach (AnimatorController controller in made.Select(c => c.Hit.Controller).Distinct()) EditorUtility.SetDirty(controller);
            Search();
        }

        private string NewValue(Hit hit)
        {
            if (hit.Kind == Kind.PlayAudio && repath != null && !(source is AnimatorController))
            {
                if (hit.Root != null && (repath.transform == hit.Root || repath.transform.IsChildOf(hit.Root)))
                    return AnimationUtility.CalculateTransformPath(repath.transform, hit.Root);
            }
            else if (hit.Kind == Kind.Driver && !string.IsNullOrEmpty(map)) return map;
            return string.IsNullOrEmpty(replace) ? null : replace;
        }

        // ---- Reveal -------------------------------------------------------------------------------

        // Opens the Animator window on the hit's controller, layer and state machine, selects its state (shown in the
        // Inspector) and frames it. The Animator window's members (UnityEditor.Graphs.AnimatorControllerTool) are internal.
        private static void Reveal(Hit hit)
        {
            if (hit.Controller == null || hit.Owner == null) return;
            EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
            Type toolType = RiryTypes.Named("UnityEditor.Graphs.AnimatorControllerTool");
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
                catch (Exception) { }
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
