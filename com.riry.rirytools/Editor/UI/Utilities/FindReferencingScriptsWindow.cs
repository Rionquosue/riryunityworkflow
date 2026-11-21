using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Find scripts that reference this popup: for each selected object, the components in its avatar that reference it,
    /// each a link that selects that component's object.
    /// </summary>
    internal sealed class FindReferencingScriptsWindow : EditorWindow
    {
        private readonly List<KeyValuePair<GameObject, List<FindReferencingScripts.Result>>> found =
            new List<KeyValuePair<GameObject, List<FindReferencingScripts.Result>>>();
        private Vector2 scroll;

        internal static void Open()
        {
            var window = CreateInstance<FindReferencingScriptsWindow>();
            window.titleContent = new GUIContent(FindReferencingScripts.Title);
            foreach (GameObject target in Selection.gameObjects.Where(each => each != null && !EditorUtility.IsPersistent(each)))
                window.found.Add(new KeyValuePair<GameObject, List<FindReferencingScripts.Result>>(target, FindReferencingScripts.Find(target)));
            window.minSize = new Vector2(380f, 200f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (KeyValuePair<GameObject, List<FindReferencingScripts.Result>> entry in found)
                {
                    if (entry.Key == null) continue;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(entry.Key.name + " (" + entry.Value.Count + ")", EditorStyles.boldLabel);
                    if (entry.Value.Count == 0)
                    {
                        EditorGUILayout.LabelField("No component in its avatar references it.", EditorStyles.miniLabel);
                        continue;
                    }
                    foreach (FindReferencingScripts.Result result in entry.Value)
                    {
                        if (result.Source == null) continue;
                        string target = result.Target is GameObject ? "" : " (" + result.Target.GetType().Name + ")";
                        var link = new GUIContent(result.Source.GetType().Name + " on " + result.Source.gameObject.name + ": " + result.Field + target,
                            "Select " + result.Source.gameObject.name + " in the Hierarchy.");
                        if (EditorGUILayout.LinkButton(link))
                        {
                            Selection.activeObject = result.Source.gameObject;
                            EditorGUIUtility.PingObject(result.Source.gameObject);
                        }
                    }
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FindReferencingScriptsWindow
//   found                    Per selected scene object, FindReferencingScripts.Find's results, worked out when opened.
//   Open()                   The popup (ShowUtility) for Selection.gameObjects in scenes or Prefab Mode.
//   OnGUI()                  Per object: its name and count, then a link per result ("Type on Object: Field", and the
//                            component pointed at when it is not the object itself) that selects and pings the component's object.
// ============================================================================
