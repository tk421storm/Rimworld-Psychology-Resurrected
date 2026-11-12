using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using UnityEngine;

namespace Psychology.Harmony
{
    // Cool-down after lovin: like vanilla (the lovin age curve at the pawn's age, times gene and hediff lovin factors,
    // randomised, at least 30 min), but using the pawn's own curve from RomanceUtility (race curve / human-equivalent age)
    // and divided by the lovin frequency setting. Pawns Psychology doesn't handle keep vanilla's value.
    [HarmonyPatch(typeof(JobDriver_Lovin), "GenerateRandomMinTicksToNextLovin")]
    public static class JobDriver_Lovin_GenerateRandomMinTicksToNextLovin_Patch
    {
        [HarmonyPostfix]
        public static void GenerateRandomMinTicksToNextLovinPostfix(ref int __result, Pawn pawn)
        {
            if (DebugSettings.alwaysDoLovin)
            {
                __result = 100;
                return;
            }
            float num = RomanceUtility.LovinCurveHours(pawn);
            if (num < 0f)
            {
                return;
            }
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                foreach (Gene gene in pawn.genes.GenesListForReading)
                {
                    num *= gene.def.lovinMTBFactor;
                }
            }
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.TryGetComp<HediffComp_GiveLovinMTBFactor>() is HediffComp_GiveLovinMTBFactor lovinFactor)
                {
                    num *= lovinFactor.Props.lovinMTBFactor;
                }
            }
            num /= PsychologySettings.lovinFrequency;

            num = Rand.Gaussian(num, 0.3f);
            if (num < 0.5f)
            {
                num = 0.5f;
            }

            __result = (int)(num * GenDate.TicksPerHour);
        }
    }
}

