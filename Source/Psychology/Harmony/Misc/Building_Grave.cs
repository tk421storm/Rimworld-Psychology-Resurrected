using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace Psychology.Harmony
{
// DISABLED since 1.5: Building_Grave.Notify_CorpseBuried no longer exists, so the "filled a grave" Bleeding Heart
// thought is never given.
// TODO: re-hook to the 1.6+ burial notification (e.g. Building_Grave/Building_Casket content-added callbacks).
// (Psychology's own funerals were removed; Ideology's funeral ritual covers them.)
#if false
    [HarmonyPatch(typeof(Building_Grave), nameof(Building_Grave.Notify_CorpseBuried))]
    public static class Building_Grave_NotifyCorpseBuried_Patch
    {

        [HarmonyPostfix]
        public static void FillGraveThought(Building_Grave __instance, Pawn worker)
        {
            if (worker.needs.mood != null)
            {
                worker.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.FilledGraveBleedingHeart);
            }
        }
    }
#endif
}
