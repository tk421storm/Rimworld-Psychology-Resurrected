using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace Psychology.Harmony
{
    // (Ideology) Personality shapes conversion (#15). Both factors are 2^x with x in -2..2, so 0.25x-4x, and 1x for an
    // average pair; they skip pawns without a psyche (either side). They stack with the personality-vs-ideoligion factor
    // in ConversionUtility_ConversionPowerFactor_MemesVsTraits_Patch, uncapped.
    //   - How often (random conversion attempts only; wardens and the Convert ability choose to convert directly):
    //     initiator Outspoken + Judgmental + Moralistic, recipient Trusting.
    //   - How much certainty each attempt removes (random attempts, warden prisoner conversion and the Convert ability,
    //     which all use CertaintyReduction): initiator Cool, recipient Naive + Experimental - Nostalgic.
    public static class ConversionPersonality
    {
        public static float AttemptWeightFactor(Pawn initiator, Pawn recipient)
        {
            if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
            {
                return 1f;
            }
            Pawn_PsycheTracker init = PsycheHelper.Comp(initiator).Psyche;
            Pawn_PsycheTracker reci = PsycheHelper.Comp(recipient).Psyche;
            float x = init.GetPersonalityRating(PersonalityNodeDefOf.Outspoken)
                + init.GetPersonalityRating(PersonalityNodeDefOf.Judgmental)
                + init.GetPersonalityRating(PersonalityNodeDefOf.Moralistic)
                + reci.GetPersonalityRating(PersonalityNodeDefOf.Trusting) - 2f;
            return Mathf.Pow(2f, x);
        }

        public static float CertaintyFactor(Pawn initiator, Pawn recipient)
        {
            if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
            {
                return 1f;
            }
            return Mathf.Pow(2f, InitiatorPersuasion(initiator) + RecipientOpenness(recipient));
        }

        // Initiator's share of the certainty exponent: Cool - 0.5 (-0.5..0.5).
        public static float InitiatorPersuasion(Pawn initiator)
        {
            return PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Cool) - 0.5f;
        }

        // Recipient's share: Naive + Experimental - Nostalgic - 0.5 (-1.5..1.5).
        public static float RecipientOpenness(Pawn recipient)
        {
            Pawn_PsycheTracker reci = PsycheHelper.Comp(recipient).Psyche;
            return reci.GetPersonalityRating(PersonalityNodeDefOf.Naive)
                + reci.GetPersonalityRating(PersonalityNodeDefOf.Experimental)
                - reci.GetPersonalityRating(PersonalityNodeDefOf.Nostalgic) - 0.5f;
        }

        // Convert ability breakdown line (added by the MemesVsTraits patch, which only runs it for two psyche pawns).
        public static string BreakdownLine(Pawn initiator, Pawn recipient)
        {
            return " -  " + "AbilityIdeoConvertBreakdownPersonalityTraits".Translate() + ": " + CertaintyFactor(initiator, recipient).ToStringPercent()
                + "\n   -  " + "AbilityIdeoConvertBreakdownPersuasion".Translate(initiator.Named("PAWN")) + ": " + Mathf.Pow(2f, InitiatorPersuasion(initiator)).ToStringPercent()
                + "\n   -  " + "AbilityIdeoConvertBreakdownOpenness".Translate(recipient.Named("PAWN")) + ": " + Mathf.Pow(2f, RecipientOpenness(recipient)).ToStringPercent();
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.RandomSelectionWeight))]
    public static class InteractionWorker_ConvertIdeoAttempt_RandomSelectionWeight_Patch
    {
        [HarmonyPostfix]
        public static void RandomSelectionWeight(ref float __result, Pawn initiator, Pawn recipient)
        {
            if (__result > 0f)
            {
                __result *= ConversionPersonality.AttemptWeightFactor(initiator, recipient);
            }
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.CertaintyReduction))]
    public static class InteractionWorker_ConvertIdeoAttempt_CertaintyReduction_Patch
    {
        [HarmonyPostfix]
        public static void CertaintyReduction(ref float __result, Pawn initiator, Pawn recipient)
        {
            __result *= ConversionPersonality.CertaintyFactor(initiator, recipient);
        }
    }

    // Debug line per actual attempt (random or warden; not the ability tooltip, which calls CertaintyReduction every frame).
    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.Interacted))]
    public static class InteractionWorker_ConvertIdeoAttempt_Interacted_Patch
    {
        [HarmonyPrefix]
        public static void LogFactors(Pawn initiator, Pawn recipient)
        {
            if (PsychologyDebug.Enabled && PsycheHelper.PsychologyEnabled(initiator) && PsycheHelper.PsychologyEnabled(recipient))
            {
                PsychologyDebug.Log($"{initiator} tried to convert {recipient}: personality attempt weight x{ConversionPersonality.AttemptWeightFactor(initiator, recipient):0.00}, "
                    + $"certainty loss x{ConversionPersonality.CertaintyFactor(initiator, recipient):0.00}, total certainty loss {InteractionWorker_ConvertIdeoAttempt.CertaintyReduction(initiator, recipient):P1}");
            }
        }
    }
}
