using System;

namespace Riry.RiryTools.Editor
{
    /// <summary>Ranks how well a name matches typed text; used by Clean Animation Keys and GUID Tools.</summary>
    internal static class FuzzyMatch
    {
        /// <summary>3 when the name starts with the text, 2 when it contains it, 1 when it holds its letters in order, else 0.</summary>
        internal static int Score(string name, string typed)
        {
            int index = name.IndexOf(typed, StringComparison.OrdinalIgnoreCase);
            if (index == 0) return 3;
            if (index > 0) return 2;
            int position = 0;
            foreach (char letter in typed)
            {
                position = name.IndexOf(letter.ToString(), position, StringComparison.OrdinalIgnoreCase);
                if (position < 0) return 0;
                position++;
            }
            return 1;
        }
    }
}
