using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Linq;using System.Reflection.Emit;

namespace Psychology.Harmony;

// When generating parents for siblings, convert vanilla's human parent-age window into the species' ages (or, for
// species with no age gap, just require the parent to be at least the minimum lovin age older).
[HarmonyPatch(typeof(PawnRelationWorker_Sibling), "GenerateParentParams")]
public static class PawnRelationWorker_Sibling_GenerateParentParams_Patch
{
    [HarmonyPrefix]
    public static bool GenerateParentParams(ref float minChronologicalAge, ref float maxChronologicalAge, ref float midChronologicalAge, ref float minBioAgeToHaveChildren, Pawn generatedChild, Pawn existingChild)
    {
        float chrAge1 = generatedChild.ageTracker.AgeChronologicalYearsFloat;
        float chrAge2 = existingChild.ageTracker.AgeChronologicalYearsFloat;

        SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(existingChild.def);
        float minLovingAge = settings.minLovinAge;
        if (minLovingAge < 0f)
        {
            minLovingAge = 0f;
        }
        if (minLovingAge == 0f || !settings.enableAgeGap)
        {
            if (existingChild.RaceProps?.lifeExpectancy is float maxBioAge != true)
            {
                maxBioAge = 80f;
            }
            minChronologicalAge = chrAge1 + minLovingAge;
            midChronologicalAge = chrAge1 + 0.5f * (maxBioAge + minLovingAge);
            maxChronologicalAge = chrAge1 + maxBioAge;
            minBioAgeToHaveChildren = minLovingAge;
            return true;
        }
        float chrAgeMax = Mathf.Max(chrAge1, chrAge2);
        float num = minChronologicalAge - chrAgeMax;
        float num2 = maxChronologicalAge - chrAgeMax;
        float num3 = midChronologicalAge - chrAgeMax;

        // Convert from hardcoded vanilla values
        num = PsycheHelper.LovinBioAgeFromVanilla(num, minLovingAge);
        num2 = PsycheHelper.LovinBioAgeFromVanilla(num2, minLovingAge);
        num3 = PsycheHelper.LovinBioAgeFromVanilla(num3, minLovingAge);

        // Recompute
        minChronologicalAge = chrAgeMax + num;
        maxChronologicalAge = chrAgeMax + num2;
        midChronologicalAge = chrAgeMax + num3;
        minBioAgeToHaveChildren = num;
        return true;

    }
}

// (Kinsey only) Vanilla divorces generated parents if one is Gay; use "Kinsey 6" instead of the trait.
public static class PawnRelationWorker_Sibling_ManualPatches
{
    public static IEnumerable<CodeInstruction> CreateRelation_Transpiler(IEnumerable<CodeInstruction> codes)
    {
        List<CodeInstruction> clist = codes.ToList();
        int max = clist.Count();
        bool bool1;
        bool bool2;

        for (int i = 0; i < max; i++)
        {
            yield return clist[i];
            if (i >= max - 4)
            {
                continue;
            }
            bool1 = clist[i + 3].LoadsField(AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Gay)));
            bool2 = clist[i + 4].Calls(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef) }));
            if (!bool1 || !bool2)
            {
                continue;
            }
            yield return CodeInstruction.Call(typeof(PawnRelationWorker_Sibling_ManualPatches), nameof(DivorceBasedOnSexuality), new Type[] { typeof(Pawn) });
            i += 4;
        }
    }

    public static bool DivorceBasedOnSexuality(Pawn parent)
    {
        bool flag = true;
        if (PsycheHelper.TryGetPawnSeed(parent) != true)
        {
            flag = PsycheHelper.HasTraitDef(parent, TraitDefOf.Gay);
            if (flag)
            {
                Log.Error("CreateRelations.DivorceBasedOnSexuality, TryGetPawnSeed(parent) != true but pawn has TraitDefOf.Gay");
            }
            return flag;
        }
        if (PsycheHelper.PsychologyEnabled(parent) != true)
        {
            flag = PsycheHelper.HasTraitDef(parent, TraitDefOf.Gay);
            if (flag)
            {
                Log.Error("CreateRelations.DivorceBasedOnSexuality, PsychologyEnabled(parent) != true but pawn has TraitDefOf.Gay");
            }
            return flag;
        }
        int kinsey = PsycheHelper.Comp(parent).Sexuality.kinseyRating;
        flag = kinsey > 5;
        return flag;
    }
}
