using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Psychology.Harmony;

// Bleeding Heart: rescuing someone gives the rescuer a happy thought.
[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.Notify_RescuedBy))]
public static class Notify_RescuedBy_BleedingHeartPatch
{

    [HarmonyPostfix]
    public static void AddBleedingHeartThought(Pawn_RelationsTracker __instance, Pawn rescuer, Pawn ___pawn)
    {
        if (rescuer.needs.mood != null && __instance.canGetRescuedThought)
        {
            rescuer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RescuedBleedingHeart, ___pawn);
        }
    }
}

// Biotech: whenever anything reads a pair's pregnancy approach (vanilla's pregnancy chance, the social tab, other
// mods), the pawns' own approach is applied first (FamilyUtility.OnApproachRead): child-free pawns always avoid.
[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.GetPregnancyApproachForPartner))]
public static class Pawn_RelationsTracker_PregnancyApproachPatch
{
    [HarmonyPostfix]
    public static void PawnsChoose(Pawn partner, Pawn ___pawn, ref PregnancyApproach __result)
    {
        __result = FamilyUtility.OnApproachRead(___pawn, partner, __result);
    }
}

// Replaces vanilla's attraction (SecondaryLovinChanceFactor) entirely (other mods' postfixes on it still run).
// Vanilla's own formula if either pawn has no psyche. Otherwise the product of:
//   - orientation: Kinsey/3 toward the same gender, 2 - Kinsey/3 toward the other (clamped to 0..1), or the vanilla
//     Gay/Bisexual/Asexual rules without Kinsey
//   - age (CalculateAgeFactor below)
//   - looks and personality: the other pawn's beauty relative to 75% of one's own (people want to date out of their
//     league), discounted by poor sight and ignored by Open-Minded pawns, plus the other pawn's Cool rating. Men weigh
//     looks more, women personality more; mapped to a multiplier of roughly 0.27-2.85
//   - drive: romantic drive + 0.25 x sex drive (both age-adjusted)
[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.SecondaryLovinChanceFactor))]
public static class Pawn_RelationsTracker_LovinChancePatch
{
    [HarmonyPrefix]
    public static bool SecondaryLovinChanceFactor(Pawn otherPawn, Pawn ___pawn, ref float __result)
    {
        // #30: pawns without a psyche (either side) use vanilla's attraction, so their romance works as in vanilla.
        if (___pawn != otherPawn && (!PsycheHelper.PsychologyEnabled(___pawn) || !PsycheHelper.PsychologyEnabled(otherPawn)))
        {
            return true;
        }
        __result = Attraction(___pawn, otherPawn, false, out _);
        return false;
    }

    // The attraction above. With explain, breakdown lists its parts for the debug log ("orientation 1.00 x age 0.45 x
    // looks 0.95 x drive 0.91"); it's skipped otherwise, as this runs very often.
    public static float Attraction(Pawn pawn, Pawn otherPawn, bool explain, out string breakdown)
    {
        if (pawn == otherPawn)
        {
            breakdown = "self";
            return 0f;
        }

        // Disable psyche for a species means no personality or sexuality generated
        // However, should we use the vanilla formula or just diable all dating?
        if (!PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.PsychologyEnabled(otherPawn))
        {
            breakdown = "no psyche"; // Disable all dating for pawns with no psyche
            return 0f;
        }

        /* SEXUAL PREFERENCE FACTOR */
        float sexualityFactor = 1f;
        /* Psychology result */
        if (PsychologySettings.enableKinsey)
        {
            float kinseyFactor = PsycheHelper.Comp(pawn).Sexuality.kinseyRating / 3f;
            sexualityFactor = Mathf.Clamp01(pawn.gender == otherPawn.gender ? kinseyFactor : 2f - kinseyFactor);
        }
        // Vanilla Asexual, Bisexual, and Gay traits
        else if (pawn.story != null && pawn.story.traits != null)
        {
            if (pawn.story.traits.HasTrait(TraitDefOf.Asexual))
            {
                breakdown = "asexual";
                return 0f;
            }
            if (!pawn.story.traits.HasTrait(TraitDefOf.Bisexual))
            {
                if (pawn.story.traits.HasTrait(TraitDefOf.Gay))
                {
                    if (otherPawn.gender != pawn.gender)
                    {
                        breakdown = "orientation 0 (gay)";
                        return 0f;
                    }
                }
                else if (otherPawn.gender == pawn.gender)
                {
                    breakdown = "orientation 0 (straight)";
                    return 0f;
                }
            }
        }

        /* AGE FACTOR */
        float ageFactor = CalculateAgeFactor(pawn, otherPawn);
        if (ageFactor == 0f)
        {
            breakdown = !explain ? null : $"orientation {sexualityFactor:0.00} x age 0 (too young or under the age minimum)";
            return 0f;
        }

        /* BEAUTY FACTOR */
        float pawnBeauty = pawn.GetStatValue(StatDefOf.PawnBeauty);
        float otherPawnBeauty = otherPawn.GetStatValue(StatDefOf.PawnBeauty);
        float otherPawnCool = PsycheHelper.Comp(otherPawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Cool);
        float pawnOpenMinded = pawn.story.traits.HasTrait(TraitDefOfPsychology.OpenMinded) ? 1f : 0f;

        /* Beautiful pawns will have higher beauty standards. Everyone wants to date out of league */
        float physicalFactor = otherPawnBeauty - 0.75f * pawnBeauty;
        /* Open Minded pawns don't care about physical beauty */
        physicalFactor *= 1f - pawnOpenMinded;
        /* Pawns who can't see as well can't determine physical beauty as well. */
        physicalFactor *= 0.1f + 0.9f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);

        /* Cool pawns are more attractive */
        float personalityFactor = 2f * otherPawnCool - 1f;

        // Men will care more about physical beauty, women will care more about personality
        physicalFactor *= pawn.gender == Gender.Male ? 1.6f : 0.6f;
        personalityFactor *= pawn.gender == Gender.Female ? 1.6f : 0.6f;

        /* Turn into multiplicative factor. This ranges between 0.27 and 2.85 */
        float beautyFactor = 0.1f + Mathf.Pow(0.5f + 1f / (1f + Mathf.Pow(4f, -physicalFactor - personalityFactor + 0.12f)), 2.5f);

        /* PAWN SEX AND ROMANCE DRIVE FACTORS */
        float pawnSexDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive;
        float pawnRomanceDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedRomanticDrive;
        float pawnDriveFactor = pawnRomanceDrive + 0.25f * pawnSexDrive;

        /*  MULTIPLY TO GET RESULT */
        breakdown = !explain ? null : $"orientation {sexualityFactor:0.00} x age {ageFactor:0.00} x looks {beautyFactor:0.00} x drive {pawnDriveFactor:0.00}";
        return sexualityFactor * ageFactor * beautyFactor * pawnDriveFactor;
    }

    // Age part of attraction: species minimum dating ages (Lechers ignore some limits), a soft minimum of half your age
    // plus 7 (in human-equivalent years), and an optional age-gap preference curve. That curve is a flat hill around the
    // pawn's own age; how far it drops off depends on Experimental (wider), Pure (narrower) and Open-Minded (wider), and it
    // is shifted older for straight women and younger for straight men, scaled by how straight they are.
    public static float CalculateAgeFactor(Pawn pawn, Pawn otherPawn)
    {
        if (!SpeciesHelper.RomanceEnabled(pawn, true) || !SpeciesHelper.RomanceEnabled(otherPawn, true))
        {
            // No romance factor for children, no exceptions
            return 0f;
        }

        float age1 = pawn.ageTracker.AgeBiologicalYearsFloat;
        float age2 = otherPawn.ageTracker.AgeBiologicalYearsFloat;
        SpeciesSettings settings1 = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
        SpeciesSettings settings2 = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(otherPawn.def);
        float minAge1 = settings1.minDatingAge;
        float minAge2 = settings2.minDatingAge;
        bool pawnLecher = pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher);
        if (minAge1 < 0f || age1 < minAge1)
        {
            // No underage initiators
            return 0f;
        }
        if (minAge2 == 0f)
        {
            // Attractiveness of ageless pawns does not depend on age
            return 1f;
        }
        if (minAge2 < 0f)
        {
            // Lechers are gross and will hit on aromantic species
            return pawnLecher ? 1f : 0f;
        }
        if (age2 < minAge2 && !pawnLecher)
        {
            // Lechers are gross and will hit on underage pawns
            return 0f;
        }
        float scaledAge2 = PsycheHelper.DatingBioAgeToVanilla(age2, minAge2);
        if (minAge1 == 0f)
        {
            return pawnLecher ? 1f : Mathf.InverseLerp(14f, 18f, scaledAge2);
        }
        float scaledAge1 = PsycheHelper.DatingBioAgeToVanilla(age1, minAge1);
        float ageFactor = pawnLecher ? 1f : Mathf.InverseLerp(14f, Mathf.Clamp(0.5f * scaledAge1 + 7f, 14f, 18f), scaledAge2);
        if (settings1.enableAgeGap && settings2.enableAgeGap)
        {
            float pawnOpenMinded = pawn.story.traits.HasTrait(TraitDefOfPsychology.OpenMinded) ? 1f : 0f;
            float pawnExperimental = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
            float pawnPure = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Pure);
            float minY = Mathf.Clamp01(Mathf.Pow(0.5f * (pawnExperimental + 1f - pawnPure), 2.3f) + 0.5f * pawnOpenMinded);

            float pawnKinseyFactor = Mathf.InverseLerp(6f, 0f, PsycheHelper.Comp(pawn).Sexuality.kinseyRating);

            // Maybe one day other genders will come to the Rim...
            float pawnGenderFactor = pawn.gender == Gender.Female ? 1f : pawn.gender == Gender.Male ? -1f : 0f;

            float smallShift = 3.5f * pawnKinseyFactor * pawnGenderFactor;
            float largeShift = 10f * pawnKinseyFactor * pawnGenderFactor;

            List<float> offsets = new List<float>() { -20f + largeShift, -6.5f + smallShift, 6.5f + smallShift, 20f + largeShift };
            ageFactor *= AgeGapFactor(scaledAge1, scaledAge2, minY, pawnLecher, offsets);
        }
        return ageFactor;
    }

    public static float AgeGapFactor(float age1, float age2, float minY, bool lecher, List<float> offsets)
    {
        if (lecher)
        {
            if (age1 > age2)
            {
                // Gross
                return 1f;
            }
            minY = 0.5f * (minY + 1f);
        }
        float olderAgeGapFactor = Mathf.Max(1f, age1 / 30f);
        float min = age1 + offsets[0] * olderAgeGapFactor;
        float lower = age1 + offsets[1] * olderAgeGapFactor;
        float upper = age1 + offsets[2] * olderAgeGapFactor;
        float max = age1 + offsets[3] * olderAgeGapFactor;
        float result = GenMath.FlatHill(minY, min, lower, upper, max, minY, age2);
        return result;
    }

}

//            /* BEAUTY FACTOR */
//            float pawnBeauty = pawn.GetStatValue(StatDefOf.PawnBeauty);
//            float otherPawnBeauty = otherPawn.GetStatValue(StatDefOf.PawnBeauty);
//            otherPawnBeauty += 2f * otherPawnCool - 1f;
//            /* Beautiful pawns will have higher beauty standards. Everyone wants to date out of league */
//            float beautyFactor = otherPawnBeauty - 0.75f * pawnBeauty;
//            //if (beautyFactor > 0)
//            //{
//            //    /* Cool Pawns enhance their beauty */
//            //    beautyFactor *= 0.67f * (1f + otherPawnCool);
//            //}
//            //else
//            //{
//            //    /* Empathetic pawns don't care as much about negative beauty */
//            //    beautyFactor *= 1.5f - pawnEmpathetic;
//            //}
//            /* Cool pawns are more attractive */
//            beautyFactor += 2f * otherPawnCool - 1f;
//            /* Open Minded pawns don't care about beauty */
//            beautyFactor *= 1f - pawnOpenMinded;
//            /* Pawns who can't see as well can't determine beauty as well. */
//            beautyFactor *= 0.1f + 0.9f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
//            /* Turn into multiplicative factor */
//            beautyFactor = Mathf.Pow(0.5f + 1f / (1f + Mathf.Pow(6f, -beautyFactor)), 2f);

//            /* PAWN SEX AND ROMANCE DRIVE FACTORS */
//            // Should this go into MtbLovingHours instead?
//            float pawnDriveFactor = 0.5f * Mathf.Pow(pawnSexDrive, 2) + 0.5f * pawnRomanceDrive;

//            /* DISABILITY FACTOR */
//            // Should this go into MtbLovingHours instead?
//            //float disabilityFactor = 0f;
//            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking));
//            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
//            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
//            ///* More experimental pawns won't care as much about disabilities */
//            //disabilityFactor = Mathf.Lerp(disabilityFactor, 0f, PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental));
//            ///* Open minded pawns won't care as much about disabilities */
//            //disabilityFactor *= 1f - 0.9f * pawnOpenMinded;
//            ///* Turn into multiplicative factor */
//            //disabilityFactor = Mathf.Pow(2, disabilityFactor);
