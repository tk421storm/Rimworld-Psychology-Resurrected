using RimWorld;
using Verse;
using System;
using HarmonyLib;
using System.Runtime.CompilerServices;
using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using System.Linq;

namespace Psychology.Harmony;

// (Kinsey only, registered in HarmonyInitialize) Word of Love uses Kinsey ratings instead of orientation traits to
// decide whether the target can fall for the chosen pawn, and respects species dating rules.
public static class CompAbilityEffect_WordOfLove_KinseyEnabledPatches
{
    public static IEnumerable<CodeInstruction> ValidateTarget_Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return ChangeLogicForSexualityTraits(codes, "ValidateTarget_Transpiler patch unsuccessful.");
    }

    public static void ValidateTarget_Postfix(CompAbilityEffect_WordOfLove __instance, LocalTargetInfo target, LocalTargetInfo ___selectedTarget, ref bool __result)
    {
        if (__result != true)
        {
            return;
        }
        Pawn pawn = ___selectedTarget.Pawn;
        Pawn pawn2 = target.Pawn;
        if (!PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.PsychologyEnabled(pawn2))
        {
            return;
        }
        if (!SpeciesHelper.RomanceEnabled(pawn, true) || !SpeciesHelper.RomanceEnabled(pawn2, true))
        {
            // No underage dating
            __result = false;
            return;
        }

        Gender gender = pawn.gender;
        Gender gender2 = pawn2.gender;
        int kinsey2 = PsycheHelper.Comp(pawn2).Sexuality.kinseyRating;
        bool compat2 = gender == gender2 ? kinsey2 > 0 : kinsey2 < 6;
        if (!compat2)
        {
            // ToDo: maybe change this to a different message that says that the ability can be cast, but the loved pawn cannot recipropate feelings of the pawn in love
            Messages.Message("AbilityCantApplyWrongAttractionGender".Translate(pawn2, pawn), pawn2, MessageTypeDefOf.CautionInput, historical: false);
        }
    }

    public static IEnumerable<CodeInstruction> Valid_Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return ChangeLogicForSexualityTraits(codes, "Valid_Transpiler patch unsuccessful.");
    }

    public static IEnumerable<CodeInstruction> ChangeLogicForSexualityTraits(IEnumerable<CodeInstruction> codes, string failureMessage)
    {
        FieldInfo storyFieldInfo = AccessTools.Field(typeof(Pawn), nameof(Pawn.story));
        FieldInfo traitsFieldInfo = AccessTools.Field(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.traits));
        MethodInfo hasTraitMethodInfo1 = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef) });
        MethodInfo hasTraitMethodInfo2 = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef), typeof(int) });

        FieldInfo asexualFieldInfo = AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Asexual));
        FieldInfo bisexualFieldInfo = AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Bisexual));
        FieldInfo gayFieldInfo = AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Gay));

        int numMatches = 0;
        
        List<CodeInstruction> clist = codes.ToList();

        for (int i = 0; i < clist.Count(); i++)
        {
            yield return clist[i];
            if (i >= clist.Count() - 4)
            {
                continue;
            }
            if (clist[i + 1].LoadsField(storyFieldInfo) != true)
            {
                continue;
            }
            if (clist[i + 2].LoadsField(traitsFieldInfo) != true)
            {
                continue;
            }
            int index = Math.Min(i + 5, clist.Count() - 1);
            int hasTraitIndex = clist[i + 4].Calls(hasTraitMethodInfo1) ? i + 4 : clist[index].Calls(hasTraitMethodInfo2) ? i + 5 : -1;
            if (hasTraitIndex == -1)
            {
                continue;
            }
            if (clist[i + 3].LoadsField(asexualFieldInfo))
            {
                yield return CodeInstruction.Call(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(AsexualCheckKinseyEnabled));
            }
            else if (clist[i + 3].LoadsField(bisexualFieldInfo))
            {
                yield return CodeInstruction.Call(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(BisexualCheckKinseyEnabled));
            }
            else if (clist[i + 3].LoadsField(gayFieldInfo))
            {
                yield return CodeInstruction.Call(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(GayCheckKinseyEnabled));
            }
            else
            {
                continue;
            }
            numMatches++;
            i = hasTraitIndex;
        }
        if (numMatches == 0)
        {
            Log.Error(failureMessage);
            yield break;
        }
    }

    public static bool AsexualCheckKinseyEnabled(Pawn pawn)
    {
        if (PsycheHelper.TryGetPawnSeed(pawn) != true)
        {
            return false;
        }
        if (PsycheHelper.PsychologyEnabled(pawn) != true)
        {
            return false;
        }
        return PsycheHelper.Comp(pawn).Sexuality.IsAsexual;
    }

    public static bool BisexualCheckKinseyEnabled(Pawn pawn)
    {
        if (PsycheHelper.TryGetPawnSeed(pawn) != true)
        {
            return false;
        }
        if (PsycheHelper.PsychologyEnabled(pawn) != true)
        {
            return false;
        }
        int kinsey = PsycheHelper.Comp(pawn).Sexuality.kinseyRating;
        return kinsey != 0 && kinsey != 6;
    }

    public static bool GayCheckKinseyEnabled(Pawn pawn)
    {
        if (PsycheHelper.TryGetPawnSeed(pawn) != true)
        {
            return false;
        }
        if (PsycheHelper.PsychologyEnabled(pawn) != true)
        {
            return false;
        }
        return PsycheHelper.Comp(pawn).Sexuality.kinseyRating == 6;
    }

}
