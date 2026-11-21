using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>Counts of one parameter set, against VRChat's limits.</summary>
    internal struct MenuToolBudgetCount
    {
        internal const int MaxBits = 256;
        internal const int MaxParameters = 8192;

        internal int Total;
        internal int Synced;
        internal int Local;
        internal int Bits;

        internal int BitsLeft
        {
            get { return MaxBits - Bits; }
        }

        internal int ParametersLeft
        {
            get { return MaxParameters - Total; }
        }

        internal static MenuToolBudgetCount Of(IEnumerable<MenuToolParamEntry> parameters)
        {
            var count = new MenuToolBudgetCount();
            foreach (MenuToolParamEntry parameter in parameters.Where(p => p.Declared))
            {
                count.Total++;
                if (parameter.Synced) count.Synced++;
                else count.Local++;
                count.Bits += parameter.Bits;
            }
            return count;
        }
    }

    /// <summary>
    /// Parameter budget with VRCFury's Parameter Compressor off and on. VRCFury compresses automatically, only when the
    /// avatar is over the limit, under its Compress / Ask / Fail setting; the estimate runs its solver (MenuToolCompressor)
    /// on the scan. For exact numbers, build a VRCFury test copy.
    /// </summary>
    internal static class MenuToolBudget
    {
        internal sealed class Result
        {
            internal MenuToolBudgetCount Off;
            internal MenuToolBudgetCount On;
            // What VRCFury would say about the compression (it cannot fit the avatar, or it is set to Fail), or "".
            internal string Note = "";
            internal readonly List<string> Compressed = new List<string>();
        }

        /// <summary>The estimate from a scan: VRCFury's compressor solver on its parameters and assembled menu.</summary>
        internal static Result Estimate(MenuToolScan scan)
        {
            var result = new Result { Off = MenuToolBudgetCount.Of(scan.Params.Values) };
            result.On = result.Off;
            int max = MenuToolCompressor.MaxCost();
            if (result.Off.Bits <= max) return result;
            MenuToolCompressor.Decision decision = MenuToolCompressor.Solve(scan, result.Off.Bits, out int bestCost);
            if (MenuToolCompressor.Setting() == MenuToolCompressor.Fail)
                result.Note = "VRCFury's Parameter Compressor is set to Fail (Tools > VRCFury), so an upload over " + max + " bits fails instead of compressing.";
            if (decision == null)
            {
                // VRCFury compresses nothing then, and the upload fails.
                result.Note = (result.Note.Length > 0 ? result.Note + " " : "") + "VRCFury cannot fit this avatar: its most compression reaches " +
                    bestCost + " of " + max + " bits, so it compresses nothing and the upload fails.";
                return result;
            }
            int added = decision.IndexBits + decision.NumberSlots + decision.BoolSlots;
            result.On.Bits = decision.FinalCost(result.Off.Bits);
            result.On.Synced = result.Off.Synced - decision.Compress.Count + added;
            // With more than one batch, every compressed parameter also gets a local latch copy.
            result.On.Total = result.Off.Total + added + (decision.BatchCount > 1 ? decision.Compress.Count : 0);
            result.On.Local = result.On.Total - result.On.Synced;
            result.Compressed.AddRange(decision.Compress.Select(p => p.Name));
            return result;
        }

        /// <summary>The synced bits under a menu entry: each parameter once.</summary>
        internal static int Bits(MenuToolScan scan, MenuToolEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Where(p => p != null && p.Declared).Sum(p => p.Bits);
        }

        /// <summary>Synced Floats under a menu entry (each once), for its shading.</summary>
        internal static int SyncedFloats(MenuToolScan scan, MenuToolEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Count(p => p != null && p.Declared && p.Synced && p.Type == ValueType.Float);
        }

        internal static HashSet<string> ParametersUnder(MenuToolEntry entry)
        {
            var names = new HashSet<string>(entry.Parameters());
            foreach (MenuToolEntry below in entry.Descendants()) names.UnionWith(below.Parameters());
            return names;
        }

        /// <summary>Synced parameters no menu control uses: the "Animator only" block.</summary>
        internal static List<MenuToolParamEntry> AnimatorOnly(MenuToolScan scan)
        {
            return scan.Params.Values.Where(p => p.Declared && p.Synced && p.UsedBy.Count == 0).OrderBy(p => p.Name).ToList();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tools budget
// VRCFury's compressor solver is in MenuToolCompressor.cs.
// ============================================================================
//
// MenuToolBudgetCount                Total custom parameters, Synced, Local, synced Bits; MaxBits 256 and MaxParameters 8192
//                            (VRChat's limits); BitsLeft / ParametersLeft. Of(scan params).
// MenuToolBudget (this file)
//   Result                   Off, On, Note (VRCFury cannot fit the avatar, or is set to Fail), Compressed.
//   Estimate(scan)           Off: the scan's counts. On: nothing changes at or under MenuToolCompressor.MaxCost; over it,
//                            VRCFury's decision: bits FinalCost, synced less the compressed plus index bits and slots, total
//                            plus index bits, slots and (more than one batch) a local latch per compressed parameter.
//   Bits / SyncedFloats      Per menu entry (its subtree), each parameter once.
