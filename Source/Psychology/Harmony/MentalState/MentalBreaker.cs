using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Psychology.Harmony;

// After a mood-caused mental break, the pawn may develop Anxiety (the mod's PTSD-like chronic condition). The chance
// scales with break intensity (minor 0.5x, major 1x, extreme 2x the "mental break anxiety chance" setting), is halved
// for Desensitized pawns and scaled by 1.5 - LaidBack. If the pawn already has Anxiety, its severity grows instead.
[HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak))]
public static class MentalBreaker_AnxietyPatch
{

    public static Pair<float, float> AnxietyChanceAndSeverity(MentalBreakIntensity intensity)
    {
        switch (intensity)
        {
            case MentalBreakIntensity.Minor:
                return new Pair<float, float>(0.5f * PsychologySettings.mentalBreakAnxietyChance, 0.3f);
            case MentalBreakIntensity.Major:
                return new Pair<float, float>(1.0f * PsychologySettings.mentalBreakAnxietyChance, 0.7f);
            case MentalBreakIntensity.Extreme:
                return new Pair<float, float>(2.0f * PsychologySettings.mentalBreakAnxietyChance, 1.1f);
            default:
                return new Pair<float, float>(0f, 0f);
        }
    }

    [HarmonyPostfix]
    public static void AddAnxiety(MentalBreaker __instance, ref bool __result, Pawn ___pawn)
    {
        // World pawns (off every map, not in a caravan) still tick and can have mood breaks in vanilla; they don't
        // get anxiety from them.
        if (!___pawn.Spawned && !___pawn.IsCaravanMember())
        {
            return;
        }
        if (__result != true || PsychologySettings.mentalBreakAnxietyChance == 0f)
        {
            return;
        }
        MentalBreakIntensity intensity = Traverse.Create(__instance).Property("CurrentDesiredMoodBreakIntensity").GetValue<MentalBreakIntensity>();
        Pair<float, float> pair = AnxietyChanceAndSeverity(intensity);
        float PTSDChance = pair.First;
        float severity = pair.Second;

        if (PTSDChance == 0.0f)
        {
            return;
        }
        Hediff hediff = ___pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety);
        if (hediff != null)
        {
            hediff.Severity += PTSDChance * severity;
            return;
        }
        if (___pawn.story.traits.HasTrait(TraitDefOfPsychology.Desensitized))
        {
            PTSDChance *= 0.5f;
        }
        if (PsycheHelper.PsychologyEnabled(___pawn))
        {
            //Laid-back pawns are less likely to get anxiety from mental breaks.
            //PTSDChance -= PsycheHelper.Comp(___pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.LaidBack) / 10f;
            PTSDChance *= 1.5f - PsycheHelper.Comp(___pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.LaidBack);
        }
        if (Rand.Chance(PTSDChance))
        {
            Hediff newHediff = HediffMaker.MakeHediff(HediffDefOfPsychology.Anxiety, ___pawn, ___pawn.health.hediffSet.GetBrain());
            newHediff.Severity = severity;
            Letter newAnxiety = LetterMaker.MakeLetter("LetterLabelPTSD".Translate(), "LetterPTSD".Translate(___pawn), LetterDefOf.NegativeEvent, ___pawn);
            if (PawnUtility.ShouldSendNotificationAbout(___pawn))
            {
                Find.LetterStack.ReceiveLetter(newAnxiety);
            }
            ___pawn.health.AddHediff(newHediff, null, null);
        }
    }
}