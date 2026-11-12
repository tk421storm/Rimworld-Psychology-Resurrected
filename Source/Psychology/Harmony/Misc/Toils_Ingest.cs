using RimWorld;
using Verse;
using HarmonyLib;

namespace Psychology.Harmony;

// On a shared meal (DateActivityWorker_ShareMeal), vanilla seats each pawn at the free chair closest to where they got
// their food, which can put the two at different tables or rooms; the second to choose sits within talking range of the
// first instead, if there's a free seat at a table there.
[HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TryFindChairOrSpot))]
public static class Toils_Ingest_TryFindChairOrSpotPatch
{
  [HarmonyPostfix]
  public static void SitWithDate(Pawn pawn, ref IntVec3 cell, ref bool __result)
  {
    if (DateActivityWorker_ShareMeal.TryFindChewSpotNearOther(pawn, __result ? cell : IntVec3.Invalid, out IntVec3 seat))
    {
      cell = seat;
      __result = true;
    }
  }
}
