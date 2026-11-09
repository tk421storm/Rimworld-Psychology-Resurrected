using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Psychology.Harmony;

// Compatibility with other romance mods.
//
// Less Stupid Romance Attempt (Mlie.LessStupidRomanceAttempt): its Harmony patches are removed, as Psychology already
// covers what they do, and they misfire alongside it:
//   - romance weight: no attempts by or on anyone who likes their lover (25+), or after any rebuff, and none between
//     partners. Psychology weighs cheating by personality, ideoligion and polyamory (RomanceUtility.CheatFactor), has its
//     own rebuff memories, and uses attempts between partners as the date-night lovin' invitation.
//   - orientation (romance success, breakups, marriage proposals): read from the vanilla Gay/Bisexual traits, which
//     pawns don't have with the Kinsey system on, so every same-gender couple counted as mismatched (proposals never
//     made or always refused, breakups doubled).
// Its XML patches (Psychopaths don't get thoughts about lovers, deaths, beauty...) stay.
//
// No Hopeless Romance (cedaro.NoHopelessRomance) works as is: its prefix filters new romances before Psychology's
// (orientation through AttractedToGender, which Psychology patches for Kinsey), and couples' date-night invitations
// skip it (InteractionWorker_RomanceAttempt_SelectionWeightPatch.PartnerWeightPrefix).
//
// Romance Tweaks (BDew.RomanceTweaks) works as is: its sliders multiply Psychology's romance weight, success chance and
// breakup weight. Couples' date-night invitations keep Psychology's weight (RandomSelectionWeightPostfix), since it
// counts a polyamorous pawn's other partners as cheating.
[StaticConstructorOnStartup]
public static class RomanceModCompat
{
  private const string LessStupidRomanceAttemptHarmonyId = "Harmony_RipRomanceAttempt";

  static RomanceModCompat()
  {
    if (ModsConfig.IsActive("Mlie.LessStupidRomanceAttempt"))
    {
      // Queued, so it runs after every mod's [StaticConstructorOnStartup] (where that mod patches), whatever the load order.
      LongEventHandler.ExecuteWhenFinished(RemoveLessStupidRomanceAttemptPatches);
    }
  }

  private static void RemoveLessStupidRomanceAttemptPatches()
  {
    MethodBase[] methods =
    {
      AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight)),
      AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance)),
      AccessTools.Method(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.RandomSelectionWeight)),
      AccessTools.Method(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.RandomSelectionWeight)),
      AccessTools.Method(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.AcceptanceChance)),
    };
    HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate");
    int removed = 0;
    foreach (MethodBase method in methods)
    {
      if (method == null)
      {
        continue;
      }
      try
      {
        Patches info = HarmonyLib.Harmony.GetPatchInfo(method);
        if (info == null || !info.Owners.Contains(LessStupidRomanceAttemptHarmonyId))
        {
          continue;
        }
        harmony.Unpatch(method, HarmonyPatchType.All, LessStupidRomanceAttemptHarmonyId);
        removed++;
      }
      catch (Exception ex)
      {
        Log.Warning("Psychology: couldn't remove Less Stupid Romance Attempt's patch on " + method.Name + ": " + ex.Message);
      }
    }
    Log.Message("Psychology: Less Stupid Romance Attempt is active; removed its romance patches from " + removed
      + " methods (Psychology handles romance, proposals and breakups; its Psychopath thought changes still apply).");
  }
}
