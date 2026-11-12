using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Replaces vanilla's chance that two pawns are generated as (ex-)lovers/spouses: species dating rules, Kinsey-based
// orientation compatibility (or vanilla Gay trait rules), 0.2^n for existing exes, age factors using the species'
// age scaling, an optional age-gap factor, and 0.01 for blood relatives.
[HarmonyPatch(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.LovePartnerRelationGenerationChance))]
public static class LovePartnerRelationUtility_GenerationChancePatch
{
  [HarmonyPrefix]
  public static bool LovePartnerRelationGenerationChance(ref float __result, Pawn generated, Pawn other, PawnGenerationRequest request, bool ex)
  {
    if (!SpeciesHelper.RomanceEnabled(generated, true) || !SpeciesHelper.RomanceEnabled(other, true))
    {
      __result = 0f;
      return false;
    }

    bool sameGender = generated.gender == other.gender;

    float sexualityFactor = 1f;
    if (PsychologySettings.enableKinsey && PsycheHelper.PsychologyEnabled(generated) && PsycheHelper.PsychologyEnabled(other))
    {
      int kinsey1 = PsycheHelper.Comp(generated).Sexuality.kinseyRating;
      int kinsey2 = PsycheHelper.Comp(other).Sexuality.kinseyRating;
      sexualityFactor *= Mathf.Clamp01(sameGender ? kinsey1 / 3f : 2f - kinsey1 / 3f);
      sexualityFactor *= Mathf.Clamp01(sameGender ? kinsey2 / 3f : 2f - kinsey2 / 3f);
    }
    else
    {
      if (sameGender && (!other.story.traits.HasTrait(TraitDefOf.Gay) || !request.AllowGay))
      {
        __result = 0f;
        return false;
      }
      if (!sameGender && other.story.traits.HasTrait(TraitDefOf.Gay))
      {
        __result = 0f;
        return false;
      }
      sexualityFactor = sameGender ? 0.01f : 1f;
    }

    float existingExLoverFactor = 1f;
    if (ex)
    {
      int exLovers = 0;
      List<DirectPawnRelation> directRelations = other.relations.DirectRelations;
      for (int i = 0; i < directRelations.Count; i++)
      {
        if (LovePartnerRelationUtility.IsExLovePartnerRelation(directRelations[i].def))
        {
          exLovers++;
        }
      }
      existingExLoverFactor = Mathf.Pow(0.2f, exLovers);
    }
    else if (LovePartnerRelationUtility.HasAnyLovePartner(other))
    {
      __result = 0f;
      return false;
    }
    SpeciesSettings generatedSettings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(generated.def);
    SpeciesSettings otherSettings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(other.def);

    float bioAge1 = generated.ageTracker.AgeBiologicalYearsFloat;
    float bioAge2 = other.ageTracker.AgeBiologicalYearsFloat;
    float minDatingAge1 = generatedSettings.minDatingAge;
    float minDatingAge2 = otherSettings.minDatingAge;

    bool minAge1IsNotZero = minDatingAge1 != 0f;
    bool minAge2IsNotZero = minDatingAge2 != 0f;
    float generationChanceAgeFactor1 = 1f;
    float generationChanceAgeFactor2 = 1f;
    float generationChanceAgeGapFactor = 1f;
    float scaledBioAge1 = 0f;
    float scaledBioAge2 = 0f;
    if (minAge1IsNotZero)
    {
      scaledBioAge1 = PsycheHelper.DatingBioAgeToVanilla(bioAge1, minDatingAge1);
      generationChanceAgeFactor1 = GetGenerationChanceAgeFactor(scaledBioAge1);
    }
    if (minAge2IsNotZero)
    {
      scaledBioAge2 = PsycheHelper.DatingBioAgeToVanilla(bioAge2, minDatingAge2);
      generationChanceAgeFactor2 = GetGenerationChanceAgeFactor(scaledBioAge2);
    }
    if (minAge1IsNotZero && minAge2IsNotZero && generatedSettings.enableAgeGap && otherSettings.enableAgeGap)
    {
      float scaledChrAge1 = PsycheHelper.DatingBioAgeToVanilla(generated.ageTracker.AgeChronologicalYearsFloat, minDatingAge1);
      float scaledChrAge2 = PsycheHelper.DatingBioAgeToVanilla(other.ageTracker.AgeChronologicalYearsFloat, minDatingAge2);
      generationChanceAgeGapFactor = GetGenerationChanceAgeGapFactor(scaledBioAge1, scaledBioAge2, scaledChrAge1, scaledChrAge2, ex);
    }
    float incestFactor = 1f;
    if (generated.GetRelations(other).Any((PawnRelationDef x) => x.familyByBloodRelation))
    {
      incestFactor = 0.01f;
    }
    __result = existingExLoverFactor * sexualityFactor * generationChanceAgeFactor1 * generationChanceAgeFactor2 * generationChanceAgeGapFactor * incestFactor;// * melaninFactor;
    return false;
  }

  public static float GetGenerationChanceAgeFactor(float scaledAge)
  {
    return Mathf.InverseLerp(14f, 27f, scaledAge);
  }

  public static float GetGenerationChanceAgeGapFactor(float scaledBioAge1, float scaledBioAge2, float scaledChrAge1, float scaledChrAge2, bool ex)
  {
    float num = Mathf.Abs(scaledBioAge1 - scaledBioAge2);
    if (ex)
    {
      float num2 = MinPossibleAgeGapAtMinAgeToGenerateAsLovers(scaledBioAge2, scaledChrAge1, scaledChrAge2);
      if (num2 >= 0f)
      {
        num = Mathf.Min(num, num2);
      }
      float num3 = MinPossibleAgeGapAtMinAgeToGenerateAsLovers(scaledBioAge1, scaledChrAge2, scaledChrAge1);
      if (num3 >= 0f)
      {
        num = Mathf.Min(num, num3);
      }
    }
    if (num > 40f)
    {
      return 0f;
    }
    return Mathf.Clamp(GenMath.LerpDouble(0f, 20f, 1f, 0.001f, num), 0.001f, 1f);
  }

  // Port of vanilla's helper, working in "human-equivalent" ages (14 = minimum dating age).
  public static float MinPossibleAgeGapAtMinAgeToGenerateAsLovers(float scaledBioAge2, float scaledChrAge1, float scaledChrAge2)
  {
    float num = scaledChrAge1 - 14f;
    if (num < 0f)
    {
      Log.Warning("at < 0");
      return 0f;
    }
    float num2 = PawnRelationUtility.MaxPossibleBioAgeAt(scaledBioAge2, scaledChrAge2, num);
    float num3 = PawnRelationUtility.MinPossibleBioAgeAt(scaledBioAge2, num);
    if (num2 < 14f)
    {
      return -1f;
    }
    if (num3 <= 14f)
    {
      return 0f;
    }
    return num3 - 14f;
  }

}

// When a Polygamous pawn marries again, only non-Polygamous spouses become ex-spouses.
[HarmonyPatch(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.ChangeSpouseRelationsToExSpouse))]
public static class LovePartnerRelationUtility_PolygamousSpousePatch
{
  [HarmonyPrefix]
  internal static bool PolygamousException(Pawn pawn)
  {
    if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
    {
      IEnumerable<Pawn> spouses = (from p in pawn.relations.RelatedPawns
                                   where pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, p)
                                   select p);
      foreach (Pawn spousePawn in spouses)
      {
        if (!spousePawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
        {
          pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Spouse, spousePawn);
          pawn.relations.AddDirectRelation(PawnRelationDefOf.ExSpouse, spousePawn);
        }
      }
      return false;
    }
    return true;
  }
}

// Lovin frequency for a couple. Vanilla: 12 h x singleFactor(pawn) x singleFactor(partner) x relationship factors.
// The transpiler replaces the 12 with RomanceUtility.NewLovinMTBHoursScale (1 / the lovin frequency setting) and the
// postfix below replaces each single-pawn factor with RomanceUtility.LovinMtbSinglePawnFactorPsychology (pain,
// consciousness, genes, sqrt of the age curve hours, sex drive), so a couple's base interval is the geometric mean of
// their two lovin curve values. The Log.Message calls only run once, when the patch is applied.
[HarmonyPatch(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.GetLovinMtbHours))]
public static class LovePartnerRelationUtility_GetLovinMtbHours
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> GetLovinMtbHoursTranspiler(IEnumerable<CodeInstruction> codes)
  {
    Log.Message("GetLovinMtbHoursTranspiler, start");
    foreach (CodeInstruction c in codes)
    {
      if (c.operand is float val && val == 12f)
      {
        Log.Message("GetLovinMtbHoursTranspiler, found code");
        c.opcode = OpCodes.Ldarg_0;
        c.operand = null;
        yield return c;
        yield return new CodeInstruction(OpCodes.Ldarg_1);
        Log.Message("GetLovinMtbHoursTranspiler, start invoke");
        yield return CodeInstruction.Call(typeof(RomanceUtility), nameof(RomanceUtility.NewLovinMTBHoursScale), new Type[] { typeof(Pawn), typeof(Pawn) });
        Log.Message("GetLovinMtbHoursTranspiler, done invoke");
      }
      else
      {
        yield return c;
      }
    }
    Log.Message("GetLovinMtbHoursTranspiler, end");
  }
}

// See LovePartnerRelationUtility_GetLovinMtbHours; vanilla's "no lovin" (-1) results are kept.
[HarmonyPatch(typeof(LovePartnerRelationUtility), "LovinMtbSinglePawnFactor")]
public static class LovePartnerRelationUtility_LovinMtbSinglePawnFactor_Patch
{
  [HarmonyPostfix]
  public static void LovinMtbSinglePawnFactorPostfix(ref float __result, Pawn pawn)
  {
    if (__result < 0f)
    {
      return;
    }
    __result = RomanceUtility.LovinMtbSinglePawnFactorPsychology(pawn);
  }
}
