using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;
using System.Xml;
using System.Security.Cryptography;

namespace Psychology.Harmony;

// Romance eligibility (incl. the "romance" float-menu option): vanilla's human minimum age of 16 is replaced with the
// species' minimum LOVIN age (RomanceUtility.InterdictRomanceAges always passes isDating = false).
[HarmonyPatch(typeof(RelationsUtility), nameof(RelationsUtility.RomanceEligible))]
public static class RelationsUtility_RomanceEligible_Patches
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> RomanceEligiblePair_Transpiler(IEnumerable<CodeInstruction> codes)
  {
    return RomanceUtility.InterdictRomanceAges(codes, OpCodes.Ldarg_0);
  }
}

// Same for pairs (the age check is on the second pawn here).
[HarmonyPatch(typeof(RelationsUtility), nameof(RelationsUtility.RomanceEligiblePair))]
public static class RelationsUtility_RomanceEligiblePair_Patches
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> RomanceEligiblePair_Transpiler(IEnumerable<CodeInstruction> codes)
  {
    return RomanceUtility.InterdictRomanceAges(codes, OpCodes.Ldarg_1);
  }
}

// (Kinsey only) AttractedToGender from the Kinsey rating: asexual -> nobody; Kinsey 0 -> other gender only; 6 -> same
// gender only; anything in between -> both. Pawns without a psyche are treated as attracted to no one.
public static class RelationsUtility_KinseyEnabledPatches
{
  public static bool AttractedToGender_KinseyEnabled_Prefix(ref bool __result, Pawn pawn, Gender gender)
  {
    if (!PsycheHelper.PsychologyEnabled(pawn))
    {
      __result = false;
      return false;
    }
    Pawn_SexualityTracker st = PsycheHelper.Comp(pawn).Sexuality;
    if (st.IsAsexual)
    {
      __result = false;
      return false;
    }
    if (st.kinseyRating == 0 && pawn.gender == gender)
    {
      __result = false;
      return false;
    }
    if (st.kinseyRating == 6 && pawn.gender != gender)
    {
      __result = false;
      return false;
    }
    __result = true;
    return false;
  }
}

