using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology.Harmony;

// Growth moments only: the letter calls GenerateTraitsFor with no request, and PassionOptions with checkGenes false.
// Pawn generation (req set / checkGenes true) stays vanilla, because the psyche would be rolled mid-generation.
public static class GrowthMomentWeights
{
  private const float TraitStrength = 0.7f;
  private const float TraitMin = 0.6f;
  private const float TraitMax = 1.6f;
  private const float PassionStrength = 1.5f;
  private const float PassionMin = 0.33f;
  private const float PassionMax = 3f;

  // (rating - 0.5) x 2: -1 at the bottom of a node, 0 at average, 1 at the top.
  private static float Lean(Pawn_PsycheTracker psyche, PersonalityNodeDef node) => (psyche.GetPersonalityRating(node) - 0.5f) * 2f;

  public static float TraitWeight(Pawn pawn, Trait trait)
  {
    Pawn_PsycheTracker psyche = PsycheHelper.Comp(pawn).Psyche;
    float affinity = 0f;
    foreach (PersonalityNodeDef node in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
    {
      if (node.traitModifiers == null)
      {
        continue;
      }
      foreach (PersonalityNodeTraitModifier modifier in node.traitModifiers)
      {
        if (modifier.trait == trait.def && modifier.degree == trait.Degree)
        {
          affinity += modifier.modifier * Lean(psyche, node);
        }
      }
    }
    return Mathf.Clamp(Mathf.Exp(TraitStrength * affinity), TraitMin, TraitMax);
  }

  public static float PassionWeight(Pawn pawn, SkillDef skill)
  {
    Pawn_PsycheTracker psyche = PsycheHelper.Comp(pawn).Psyche;
    float affinity = 0f;
    foreach (PersonalityNodeDef node in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
    {
      if (node.passionAffinities == null)
      {
        continue;
      }
      foreach (PersonalityNodePassionAffinity link in node.passionAffinities)
      {
        if (link.skill == skill)
        {
          affinity += link.affinity * Lean(psyche, node);
        }
      }
    }
    return Mathf.Clamp(Mathf.Exp(PassionStrength * affinity), PassionMin, PassionMax);
  }

  // A weighted draw without replacement.
  public static List<T> Pick<T>(List<T> candidates, int count, System.Func<T, float> weight)
  {
    List<T> pool = new List<T>(candidates);
    List<T> picked = new List<T>();
    while (picked.Count < count && pool.Count > 0)
    {
      T choice = pool.RandomElementByWeight(weight);
      pool.Remove(choice);
      picked.Add(choice);
    }
    return picked;
  }
}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateTraitsFor))]
public static class PawnGenerator_GenerateTraitsFor_GrowthPatch
{
  private const int CandidateFactor = 3;
  private static bool drawing;

  [HarmonyPostfix]
  public static void GrowthMomentPostfix(Pawn pawn, int traitCount, PawnGenerationRequest? req, bool growthMomentTrait, ref List<Trait> __result)
  {
    if (drawing || !growthMomentTrait || req.HasValue || !PsycheHelper.PsychologyEnabled(pawn))
    {
      return;
    }
    drawing = true;
    try
    {
      // Vanilla draws the candidates, so all its rules and commonalities still hold.
      List<Trait> candidates = PawnGenerator.GenerateTraitsFor(pawn, traitCount * CandidateFactor, null, true);
      __result = GrowthMomentWeights.Pick(candidates, traitCount, t => GrowthMomentWeights.TraitWeight(pawn, t));
      PsychologyDebug.Log($"Growth moment traits for {pawn}: {string.Join(", ", candidates.Select(t => $"{t.Label} x{GrowthMomentWeights.TraitWeight(pawn, t):0.00}"))}; offered {string.Join(", ", __result.Select(t => t.Label))}");
    }
    finally
    {
      drawing = false;
    }
  }
}

[HarmonyPatch(typeof(ChoiceLetter_GrowthMoment), nameof(ChoiceLetter_GrowthMoment.PassionOptions))]
public static class ChoiceLetter_GrowthMoment_PassionOptions_Patch
{
  private static bool drawing;

  [HarmonyPostfix]
  public static void GrowthMomentPostfix(Pawn pawn, int count, bool checkGenes, ref List<SkillDef> __result)
  {
    if (drawing || checkGenes || !PsycheHelper.PsychologyEnabled(pawn))
    {
      return;
    }
    drawing = true;
    try
    {
      // Every valid skill, then a weighted pick in place of vanilla's uniform one.
      List<SkillDef> candidates = ChoiceLetter_GrowthMoment.PassionOptions(pawn, DefDatabase<SkillDef>.DefCount, false);
      __result = GrowthMomentWeights.Pick(candidates, count, s => GrowthMomentWeights.PassionWeight(pawn, s));
      PsychologyDebug.Log($"Growth moment passions for {pawn}: {string.Join(", ", candidates.Select(s => $"{s.label} x{GrowthMomentWeights.PassionWeight(pawn, s):0.00}"))}; offered {string.Join(", ", __result.Select(s => s.label))}");
    }
    finally
    {
      drawing = false;
    }
  }
}
