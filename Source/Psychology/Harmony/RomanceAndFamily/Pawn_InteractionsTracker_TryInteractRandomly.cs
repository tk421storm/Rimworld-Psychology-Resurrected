using RimWorld;
using Verse;
using HarmonyLib;

namespace Psychology.Harmony;

// Romance while socializing: before vanilla picks a random interaction, a pawn in a super-active social job may look
// for someone nearby to make a romance attempt on instead (RomanceAttemptRouter.TrySocialRomance). The same "too soon"
// and "can initiate" checks as vanilla come first; if no attempt is made, vanilla runs as normal.
[HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractRandomly")]
public static class Pawn_InteractionsTracker_TryInteractRandomlyPatch
{
  [HarmonyPrefix]
  public static bool SocialRomance(Pawn_InteractionsTracker __instance, Pawn ___pawn, ref bool __result)
  {
    if (__instance.InteractedTooRecentlyToInteract() || !SocialInteractionUtility.CanInitiateRandomInteraction(___pawn))
    {
      return true;
    }
    if (RomanceAttemptRouter.TrySocialRomance(___pawn))
    {
      __result = true;
      return false;
    }
    return true;
  }
}
