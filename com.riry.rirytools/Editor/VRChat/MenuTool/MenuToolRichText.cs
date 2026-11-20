using System.Text.RegularExpressions;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu control names can hold rich text tags (such as &lt;b&gt; or &lt;color=#DDDDDD&gt;), which VRChat draws as styling.
    /// Menu Tools shows names without them; only renaming (triple-click or Rename) shows and edits the name with its tags.
    /// </summary>
    internal static class MenuToolRichText
    {
        // An opening or closing tag: "<", an optional "/", a letter or "#", then anything up to ">" with no "<" or ">" inside.
        private static readonly Regex Tag = new Regex("</?[A-Za-z#][^<>]*>", RegexOptions.CultureInvariant);

        /// <summary>The text with its rich text tags removed; null stays null.</summary>
        internal static string Plain(string text)
        {
            return string.IsNullOrEmpty(text) ? text : Tag.Replace(text, "");
        }
    }
}

// ============================================================================
// GLOSSARY: MenuToolRichText
//   Tag                      The rich text tag pattern ("<b>", "</color>", "<color=#DDDDDD>", "<#FFF>").
//   Plain(text)              The text without tags, for showing names in the Menu Tools UI.
// ============================================================================
