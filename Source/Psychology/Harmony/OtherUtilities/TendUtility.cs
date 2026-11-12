using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using HarmonyLib;

namespace Psychology.Harmony
{
    // Bleeding Heart: tending someone gives the doctor a small happy thought.
    // Doctors without a mood need (e.g. Biotech mechanoid tenders) are skipped.
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class TendUtility_TendPatch
    {
        [HarmonyPostfix]
        public static void BleedingHeartThought(Pawn doctor, Pawn patient)
        {
            if (doctor?.needs?.mood != null)
            {
                doctor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DoctorBleedingHeart, patient);
            }
        }
    }
}
