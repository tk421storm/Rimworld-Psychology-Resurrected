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

//[HarmonyPatch(typeof(ChildRelationUtility), nameof(ChildRelationUtility.ChanceOfBecomingChildOf))]
// (Kinsey only) Vanilla heavily discounts Gay pawns becoming parents. Replace the trait check with "always true" for
// psyche pawns and the discount with a factor that falls linearly from Kinsey 0 (1x) to Kinsey 6 (0x).
public static class ChildRelationUtility_ManualPatches
{
    public static IEnumerable<CodeInstruction> ChanceOfBecomingChildOf_Transpiler(IEnumerable<CodeInstruction> codes)
    {
        List<CodeInstruction> clist = codes.ToList();
        List<OpCode> opcodesBreakFalseList = new List<OpCode> { OpCodes.Brfalse, OpCodes.Brfalse_S };
        int max = codes.Count();
        int success = 0;
        int pawnLoadIndex = 0;
        bool searchingForBreak = false;
        bool searchingForMultiplier = false;

        for (int i = 0; i < max; i++)
        {
            yield return clist[i];
            if (searchingForBreak)
            {
                if (opcodesBreakFalseList.Contains(clist[i].opcode))
                {
                    searchingForBreak = false;
                    searchingForMultiplier = true;
                }
                continue;
            }
            if (searchingForMultiplier)
            {
                if (clist[i].opcode == OpCodes.Ldc_R4)
                {
                    searchingForMultiplier = false;
                    // Load pawn
                    yield return clist[pawnLoadIndex];
                    // Replace multiplier
                    yield return CodeInstruction.Call(typeof(ChildRelationUtility_ManualPatches), nameof(KinseyEnabledMultiplier));
                }
                continue;
            }
            if (i >= max - 4)
            {
                continue;
            }
            bool bool0 = clist[i + 3].LoadsField(AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Gay)));
            bool bool1 = clist[i + 4].Calls(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef) }));
            if (!bool0 || !bool1)
            {
                continue;
            }
            searchingForBreak = true;
            yield return CodeInstruction.Call(typeof(ChildRelationUtility_ManualPatches), nameof(HasTraitGayKinseyEnabled));
            pawnLoadIndex = i;
            success++;
            i += 4;   
        }

        if (success < 2 || searchingForBreak || searchingForMultiplier)
        {
            Log.Error("ChildRelationUtility.ChanceOfBecomingChildOf not patched properly, success < 2: " + (success < 2) + ", searchingForBreak: " + searchingForBreak + ", searchingForMultiplier: " + searchingForMultiplier);
        }
    }

    public static bool HasTraitGayKinseyEnabled(Pawn parent)
    {
        bool flag = true;
        if (PsycheHelper.TryGetPawnSeed(parent) != true)
        {
            flag = PsycheHelper.HasTraitDef(parent, TraitDefOf.Gay);
            if (flag)
            {
                Log.Error("ChanceOfBecomingChildOf.HasTraitGayKinseyEnabled, TryGetPawnSeed(parent) != true but pawn has TraitDefOf.Gay");
            }
            return flag;
        }
        if (PsycheHelper.PsychologyEnabled(parent) != true) // no psyche: vanilla, no error (expected for psyche-disabled species)
        {
            flag = PsycheHelper.HasTraitDef(parent, TraitDefOf.Gay);
            return flag;
        }
        // Move on to the KinseyEnabledMultiplier
        return true;
    }

    public static float KinseyEnabledMultiplier(float oldMultiplier, Pawn parent)
    {
        if (PsycheHelper.TryGetPawnSeed(parent) != true)
        {
            Log.Error("ChanceOfBecomingChildOf.KinseyEnabledMultiplier, using old multiplier because TryGetPawnSeed(parent) != true");
            return oldMultiplier;
        }
        if (PsycheHelper.PsychologyEnabled(parent) != true)
        {
            return oldMultiplier;
        }
        return Mathf.InverseLerp(6f, 0f, PsycheHelper.Comp(parent).Sexuality.kinseyRating);
    }

}

// Parent age window in species ages; pawns of species without a psyche/romance or in a non-reproductive life stage
// can't be generated as parents.
[HarmonyPatch(typeof(ChildRelationUtility), "GetParentAgeFactor")]
public static class ChildRelationUtility_GetParentAgeFactor_Patch
{
    [HarmonyPrefix]
    public static bool GetParentAgeFactor(ref float __result, Pawn parent, Pawn child, ref float minAgeToHaveChildren, ref float usualAgeToHaveChildren, ref float maxAgeToHaveChildren)
    {
        SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(parent.def);
        float minLovinAge = settings.minLovinAge;
        if (!settings.enablePsyche || !parent.ageTracker.CurLifeStage.reproductive || minLovinAge < 0f)
        {
            __result = 0f;
            return false;
        }
        if (minLovinAge == 0f || !settings.enableAgeGap)
        {
            float parentChrAge = parent.ageTracker.AgeChronologicalYearsFloat;
            float childChrAge = child.ageTracker.AgeChronologicalYearsFloat;
            __result = minLovinAge + childChrAge < parentChrAge ? 1f : 0f;
            return false;
        }
        minAgeToHaveChildren = PsycheHelper.LovinBioAgeToVanilla(minAgeToHaveChildren, minLovinAge);
        usualAgeToHaveChildren = PsycheHelper.LovinBioAgeToVanilla(usualAgeToHaveChildren, minLovinAge);
        maxAgeToHaveChildren = PsycheHelper.LovinBioAgeToVanilla(maxAgeToHaveChildren, minLovinAge);
        return true;
    }
}
