using RimWorld;
using HarmonyLib;
using UnityEngine;

namespace Psychology.Harmony;

// Scales the vanilla trait-clash opinion by the "trait opinion multiplier" setting.
[HarmonyPatch(typeof(Thought_TeetotalerVsChemicalInterest), nameof(Thought_TeetotalerVsChemicalInterest.OpinionOffset))]
public class Thought_TeetotalerVsChemicalInterest_OpinionOffset_Patch
{
    [HarmonyPostfix]
    public static void OpinionOffset(ref float __result)
    {
        __result = Mathf.CeilToInt(PsychologySettings.traitOpinionMultiplier * __result);
    }
}