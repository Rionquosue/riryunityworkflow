using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The popup shown when Avatar Migration Assistant is opened without the VRChat SDK:
    /// "VRC SDK not detected! Only some tools will be available.", a Do not show again
    /// checkbox and OK. Do not show again is kept per project (EditorUserSettings).
    /// </summary>
    internal sealed class AvatarMigratorSdkNoticeWindow : EditorWindow
    {
        internal const string Message = "VRC SDK not detected! Only some tools will be available.";
        internal const string OffKey = "RiryTools.AvatarMigrator.SdkNoticeOff";

        private bool doNotShowAgain;

        /// <summary>Shows the popup when the SDK is missing, unless Do not show again was ticked.</summary>
        internal static void ShowIfNeeded()
        {
            if (AvatarMigratorWindow.SdkPresent || EditorUserSettings.GetConfigValue(OffKey) == "1") return;
            var notice = CreateInstance<AvatarMigratorSdkNoticeWindow>();
            notice.titleContent = new GUIContent(AvatarMigratorWindow.Title);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            notice.position = new Rect(main.center.x - 170f, main.center.y - 45f, 340f, 90f);
            notice.minSize = notice.maxSize = new Vector2(340f, 90f);
            notice.ShowModalUtility();
        }

        private void OnGUI()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField(Message, EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                doNotShowAgain = EditorGUILayout.ToggleLeft("Do not show again", doNotShowAgain);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK", GUILayout.Width(80f)))
                {
                    if (doNotShowAgain) EditorUserSettings.SetConfigValue(OffKey, "1");
                    Close();
                }
            }
            GUILayout.Space(4f);
        }
    }
}

// ============================================================================
// GLOSSARY: VRC SDK notice (AvatarMigratorSdkNoticeWindow.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorSdkNoticeWindow (this file)
//   ShowIfNeeded()   When the tool is opened (menu entry or Main Menu tab) without the VRChat SDK: Message, a
//                    Do not show again checkbox and OK. Ticked and OK: OffKey (EditorUserSettings) is set to 1.
