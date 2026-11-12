using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

namespace Psychology.Harmony;

// How likely a pawn is to break up with their partner. Codependent pawns never do.
[HarmonyPatch(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.RandomSelectionWeight), new[] { typeof(Pawn), typeof(Pawn) })]
public static class InteractionWorker_RandomSelectionWeight_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(InteractionWorker_Breakup __instance, ref float __result, Pawn initiator, Pawn recipient)
    {
        if (initiator.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
        {
            __result = 0f;
            return false;
        }
        return true;
    }

    // 1.4 behaviour (scaled vanilla's weight by Romantic), replaced in the 1.5/1.6 builds by NewSelectionWeight below.
    //[HarmonyPostfix]
    //public static void Postfix(InteractionWorker_Breakup __instance, ref float __result, Pawn initiator, Pawn recipient)
    //{
    //    if (PsycheHelper.PsychologyEnabled(initiator))
    //    {
    //        __result *= Mathf.Lerp(2f, 0f, PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic));
    //    }
    //}

    // Replaces vanilla's weight: 0.15 (0.02 without a psyche) x how un-Romantic the initiator is x how much they dislike
    // the partner (opinion 100 -> 0, -100 -> 1) x 0.4 if married. NOTE: two [HarmonyPrefix] methods in one class; both run,
    // the Codependent check is just duplicated.
    [HarmonyPrefix]
    public static bool NewSelectionWeight(InteractionWorker_Breakup __instance, ref float __result, Pawn initiator, Pawn recipient)
    {
        /* Also this one. */
        if (!LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient))
        {
            __result = 0f;
            return false;
        }
        else if (initiator.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
        {
            __result = 0f;
            return false;
        }
        float chance = 0.02f;
        float romanticFactor = 1f;
        if (PsycheHelper.PsychologyEnabled(initiator))
        {
            chance = 0.15f;
            romanticFactor = Mathf.InverseLerp(1.05f, 0f, PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic));
        }
        float opinionFactor = Mathf.InverseLerp(100f, -100f, (float)initiator.relations.OpinionOf(recipient));
        float spouseFactor = 1f;
        if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
        {
            spouseFactor = 0.4f;
        }
        __result = chance * romanticFactor * opinionFactor * spouseFactor;
        return false;
    }

}

// Break-up thoughts go through RomanceHelperMethods (personality-scaled).
[HarmonyPatch(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.Interacted))]
public static class InteractionWorker_Breakup_Interacted_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
    }

}
