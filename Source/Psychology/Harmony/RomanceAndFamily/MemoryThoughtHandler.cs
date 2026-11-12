using HarmonyLib;
using RimWorld;
using Verse;

namespace Psychology.Harmony;

// Family traits scale the mood of losing a child (FamilyUtility.IsChildLossThought: my son/daughter died or was lost,
// stillbirth, miscarriage, partner's miscarriage): x1.5 for a matriarch/patriarch, x0.5 for a child-free pawn. Applied
// once, as the memory is gained, through its moodPowerFactor (saved with the memory).
[HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
public static class MemoryThoughtHandler_ChildLossPatch
{
    [HarmonyPrefix]
    public static void ScaleByFamilyTraits(MemoryThoughtHandler __instance, Thought_Memory newThought)
    {
        if (newThought?.def == null || !FamilyUtility.IsChildLossThought(newThought.def))
        {
            return;
        }
        float factor = FamilyUtility.ChildLossMoodFactor(__instance.pawn);
        if (factor != 1f)
        {
            newThought.moodPowerFactor *= factor;
            PsychologyDebug.Log($"{newThought.def.defName} for {__instance.pawn}: mood x{factor} (family trait)");
        }
    }
}
