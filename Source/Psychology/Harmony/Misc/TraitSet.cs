using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Psychology.Harmony;

// Gaining a trait that some PersonalityNodeDef lists in traitModifiers recalculates the pawn's adjusted ratings.
[HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
public static class TraitSet_GainTrait_Patch
{
    [HarmonyPostfix]
    public static void GainTrait_AdjustedRatingsPostfix(Trait trait, Pawn ___pawn)
    {
        if (!PsycheHelper.TraitDefNamesThatAffectPsyche.Contains(trait.def.defName))
        {
            return;
        }
        if (PsycheHelper.TryGetPawnSeed(___pawn) != true)
        {
            return;
        }
        if (PsycheHelper.PsychologyEnabled(___pawn) != true)
        {
            return;
        }
        PsycheHelper.Comp(___pawn).Psyche.CalculateAdjustedRatings();
    }
}

// Applied by ManualPatches only when the Kinsey system is enabled. Vanilla orientation traits are converted into
// Kinsey values instead of being added (Asexual/Bisexual/Gay are blocked), and Codependent / Lecher guarantee a
// romantic / sex drive of at least 0.5. The HasTrait postfixes below only log; they aren't registered anywhere.
public class TraitSet_ManualPatches
{
    public static bool GainTrait_KinseyEnabledPrefix(Trait trait, Pawn ___pawn)
    {
        int num;
        if (trait.def == TraitDefOfPsychology.Codependent)
        {
            num = 1;
        }
        else if (trait.def == TraitDefOfPsychology.Lecher)
        {
            num = 2;
        }
        // Other mods add orientation traits directly (e.g. Humanoid Alien Races' forced race traits), so converting
        // them is expected, not an error.
        else if (trait.def == TraitDefOf.Asexual)
        {
            PsychologyDebug.Log("Asexual trait converted to Kinsey values for " + ___pawn);
            num = 3;
        }
        else if (trait.def == TraitDefOf.Bisexual)
        {
            PsychologyDebug.Log("Bisexual trait converted to Kinsey values for " + ___pawn);
            num = 4;
        }
        else if (trait.def == TraitDefOf.Gay)
        {
            PsychologyDebug.Log("Gay trait converted to Kinsey values for " + ___pawn);
            num = 5;
        }
        else
        {
            return true;
        }
        if (PsycheHelper.TryGetPawnSeed(___pawn) != true)
        {
            Log.Error("GainTrait_KinseyEnabledPrefix, unable to get seed for pawn " + ___pawn + " and trait " + trait);
            return false;
        }
        if (PsycheHelper.PsychologyEnabled(___pawn) != true)
        {
            Log.Error("GainTrait_KinseyEnabledPrefix, PsychologyDisabled for pawn " + ___pawn + " and trait " + trait);
            return false;
        }
        switch (num)
        {
            case 1:
                PsycheHelper.Comp(___pawn).Sexuality.romanticDrive = Mathf.Max(0.5f, PsycheHelper.Comp(___pawn).Sexuality.romanticDrive);
                return true;
            case 2:
                PsycheHelper.Comp(___pawn).Sexuality.sexDrive = Mathf.Max(0.5f, PsycheHelper.Comp(___pawn).Sexuality.sexDrive);
                return true;
            case 3:
                PsycheHelper.Comp(___pawn).Sexuality.AsexualTraitReroll();
                return false;
            case 4:
                PsycheHelper.Comp(___pawn).Sexuality.BisexualTraitReroll();
                return false;
            case 5:
                PsycheHelper.Comp(___pawn).Sexuality.GayTraitReroll();
                return false;
            default:
                Log.Error("GainTrait_KinseyEnabledPrefix, impossible condition reached for pawn " + ___pawn + " and trait " + trait);
                return true;
        }
    }

    public static void HasTrait_KinseyEnabledPostfix(TraitDef tDef, Pawn ___pawn, ref bool __result)
    {
        if (tDef == TraitDefOf.Asexual)
        {
        }
        else if (tDef == TraitDefOf.Bisexual)
        {
        }
        else if (tDef == TraitDefOf.Gay)
        {
        }
        else
        {
            return;
        }
        if (__result)
        {
            Log.Error("HasTrait_KinseyEnabledPostfix, pawn " + ___pawn + " has trait " + tDef);
        }
    }

    public static void HasTrait_KinseyEnabledPostfix2(TraitDef tDef, int degree, Pawn ___pawn, ref bool __result)
    {
        HasTrait_KinseyEnabledPostfix(tDef, ___pawn, ref __result);
    }
}
