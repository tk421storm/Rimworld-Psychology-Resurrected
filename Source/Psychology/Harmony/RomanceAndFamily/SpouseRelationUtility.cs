using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Routes the marriage-related thought removal through RomanceHelperMethods, so the Psychology versions are removed too.
[HarmonyPatch(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.RemoveGotMarriedThoughts))]
public static class SpouseRelationUtility_RemoveGotMarriedThoughts_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
    }
}
