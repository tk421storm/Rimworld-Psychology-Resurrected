using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HarmonyLib;
using UnityEngine;

namespace Psychology.Harmony

{
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), "DoRecruit")]
    //Pawn recruiter, Pawn recruitee, out string letterLabel, out string letter, bool useAudiovisualEffects = true, bool sendLetter = true); // 1.3
    // When a prisoner is recruited, they get a "captured me" opinion memory of every free colonist (the "imprisoned
    // debuff" setting controls its strength; 0 disables this).
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Pawn), typeof(string), typeof(string), typeof(bool), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
    public static class InteractionWorker_RecruitAttempt_DoRecruitPatch
    {

        [HarmonyPrefix]
        public static bool AddCapturedThoughts(Pawn recruiter, Pawn recruitee)
        {
            if (PsycheHelper.PsychologyEnabled(recruitee) && PsychologySettings.imprisonedDebuff != 0f)
            {
                IEnumerable<Pawn> allFactionPawns = Find.Maps.SelectMany(m => from p in m.mapPawns.FreeColonistsSpawned
                                                                              where p != recruitee
                                                                              select p);
                if (recruitee.IsPrisonerOfColony)
                {
                    foreach (Pawn pawn in allFactionPawns)
                    {
                        recruitee.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.CapturedMe, pawn);
                    }
                }
            }
            return true;
        }
    }

}
