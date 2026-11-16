namespace Riry.RiryTools.Editor
{
    /// <summary>Byte-level matching shared by GUID regeneration (GuidToolRegenerate) and reference repair (GuidToolReferenceRepair).</summary>
    internal static class GuidToolBytes
    {
        /// <summary>True when <paramref name="token"/> appears in <paramref name="buffer"/> at <paramref name="offset"/>; the caller keeps it in range.</summary>
        internal static bool Matches(byte[] buffer, int offset, byte[] token)
        {
            for (int i = 0; i < token.Length; i++)
            {
                if (buffer[offset + i] != token[i]) return false;
            }
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: GUID Tools byte matching
// ============================================================================
//
// GuidToolBytes (this file)
//   Matches(buffer, offset, token)  The token's bytes at that offset. Used by GuidToolRegenerate's scan, patch and
//                                   rollback checks and by GuidToolReferenceRepair's .meta and reference readers.
