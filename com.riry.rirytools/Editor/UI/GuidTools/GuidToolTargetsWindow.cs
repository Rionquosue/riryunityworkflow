using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The GUID Regenerator and GUID Repair popups (the Main Menu's General Tools > PopUp Tools): a list of assets and
    /// folders under Assets, filled from the Project window's selection when opened, then Regenerate GUID
    /// (GuidToolRegenerate.RegenerateGuids) or Repair (GuidToolReferenceRepair.Repair) on them.
    /// </summary>
    internal sealed class GuidToolTargetsWindow : EditorWindow
    {
        internal const string RegeneratorTitle = "GUID Regenerator";
        internal const string RepairTitle = "GUID Repair";

        [SerializeField] private bool repair;
        [SerializeField] private List<Object> targets = new List<Object>();
        [SerializeField] private Vector2 scroll;

        internal static void OpenRegenerator() { Open(false); }

        internal static void OpenRepair() { Open(true); }

        private static void Open(bool repair)
        {
            var window = CreateInstance<GuidToolTargetsWindow>();
            window.repair = repair;
            window.titleContent = new GUIContent(repair ? RepairTitle : RegeneratorTitle);
            window.targets = Selection.objects.Where(UnderAssets).Distinct().ToList();
            window.minSize = new Vector2(380f, 200f);
            window.ShowUtility();
        }

        private static bool UnderAssets(Object item)
        {
            if (item == null) return false;
            string path = AssetDatabase.GetAssetPath(item);
            return path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(repair
                    ? "Finds and repairs missing GUID references in these assets and the assets in these folders."
                    : "Gives these assets, and the assets in these folders, new GUIDs, keeping every reference to them. Folder GUIDs never change.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Assets and folders", EditorStyles.boldLabel);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.ExpandHeight(true)))
            {
                scroll = view.scrollPosition;
                int remove = -1;
                for (int i = 0; i < targets.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Object picked = EditorGUILayout.ObjectField(targets[i], typeof(Object), false);
                        if (picked != targets[i] && (picked == null || UnderAssets(picked))) targets[i] = picked;
                        if (GUILayout.Button("Remove", GUILayout.Width(70f))) remove = i;
                    }
                }
                if (remove >= 0) targets.RemoveAt(remove);
                // An empty field at the end: an asset or folder dropped or picked here is added.
                Object added = EditorGUILayout.ObjectField("Add", null, typeof(Object), false);
                if (added != null && UnderAssets(added) && !targets.Contains(added)) targets.Add(added);
            }
            Object[] valid = targets.Where(UnderAssets).ToArray();
            using (new EditorGUI.DisabledScope(valid.Length == 0))
            {
                if (GUILayout.Button(repair ? "Repair" : "Regenerate GUID", GUILayout.Height(28f)))
                {
                    // Next editor update, outside this GUI pass: both show dialogs, and Repair opens its window.
                    if (repair) EditorApplication.delayCall += () => GuidToolReferenceRepair.Repair(valid);
                    else EditorApplication.delayCall += () => GuidToolRegenerate.RegenerateGuids(valid);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: GuidToolTargetsWindow
//   RegeneratorTitle / RepairTitle  The popups' titles, also the Main Menu's PopUp Tools buttons.
//   repair / targets         [SerializeField] Which popup; the assets and folders under Assets it runs on.
//   OpenRegenerator() / OpenRepair()  The popup (ShowUtility), its list filled from the Project selection.
//   UnderAssets(item)        The item is Assets or inside it.
//   OnGUI()                  Description, the list (Remove per row, Add field), Regenerate GUID / Repair (closes the popup
//                            and runs on the next editor update).
// ============================================================================
