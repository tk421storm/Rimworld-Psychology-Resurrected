using RimWorld;
using HarmonyLib;
using UnityEngine;

namespace Psychology.Harmony;

// Scales the vanilla trait-clash opinion by the "trait opinion multiplier" setting (also see
// Common/Patches/OpinionMultiplierPatches.xml for the XML-defined ones).
[HarmonyPatch(typeof(Thought_ChemicalInterestVsTeetotaler), nameof(Thought_ChemicalInterestVsTeetotaler.OpinionOffset))]
public class Thought_ChemicalInterestVsTeetotaler_Patch
{
    [HarmonyPostfix]
    public static void OpinionOffset(ref float __result)
    {
        __result = Mathf.CeilToInt(PsychologySettings.traitOpinionMultiplier * __result);
    }
}