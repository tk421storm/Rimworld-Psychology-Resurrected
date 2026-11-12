using RimWorld;
using Verse;
using Verse.AI.Group;
using HarmonyLib;

namespace Psychology.Harmony;

// On a date or hang-out, joy activities (games, reading, skygazing...) don't stop as soon as joy is full: they run their
// normal length, so the pair isn't left idle once they've had their fun. The date's own activity picking takes over when
// they end (see DateActivity).
[HarmonyPatch(typeof(JoyUtility), nameof(JoyUtility.JoyTickCheckEnd))]
public static class JoyUtility_JoyTickCheckEndPatch
{
  [HarmonyPrefix]
  public static void KeepGoingOnDates(Pawn pawn, ref JoyTickFullJoyAction fullJoyAction)
  {
    if (fullJoyAction == JoyTickFullJoyAction.None)
    {
      return;
    }
    LordJob lordJob = pawn.GetLord()?.LordJob;
    if (lordJob is LordJob_Date || lordJob is LordJob_HangOut)
    {
      fullJoyAction = JoyTickFullJoyAction.None;
    }
  }
}
