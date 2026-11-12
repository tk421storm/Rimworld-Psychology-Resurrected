using Verse;
using HarmonyLib;
using RimWorld;

namespace Psychology.Harmony;

// Adds the personality-driven certainty drift (Pawn_PsycheTracker.CalculateCertaintyChangePerDay, cached by
// PsychologyGameComponent.CertaintyChange) to vanilla's per-day certainty change. Shows up in the ideoligion tooltip too.
// Vanilla's IdeoTrackerTickInterval(delta) applies CertaintyChangePerDay / 60000 x delta on each pawn's own update
// interval, so the drift needs no tick patch of its own. (The 1.4-era "run 1 tick in 250" prefix and 60000 -> 240
// transpiler were removed: in 1.6 they froze most off-screen pawns' certainty and sped up the rest.)
[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.CertaintyChangePerDay), MethodType.Getter)]
public static class Pawn_IdeoTracker_CertaintyChangePerDay_Patch
{
    [HarmonyPostfix]
    public static void CertaintyChangePerDay(ref float __result, Pawn ___pawn)
    {
        if (!PsycheHelper.PsychologyEnabled(___pawn))
        {
            return;
        }
        __result += Current.Game.GetComponent<PsychologyGameComponent>().CertaintyChange(___pawn, true);
    }
}
