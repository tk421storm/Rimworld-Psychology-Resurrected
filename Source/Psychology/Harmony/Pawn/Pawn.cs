using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Arrests: Psychology's odds replace vanilla's arrester stat. Vanilla's automatic cases (downed, incapable of violence,
// guilty colonist, revenant hypnosis, genes that block aggressive breaks) still always go quietly, and the pawn kind's
// acceptArrestChanceFactor still applies. Otherwise: the arrester's ArrestPeacefullyChance (the "peaceful arrest base
// chance" setting + 5% per Social level, scaled by Talking) x the arrestee's opinion of the arrester (0 at -100, 0.5
// at 0, 1 at +100) x 1.5 if they share a faction x 0.2 if the arrestee is in a mental state.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetAcceptArrestChance))]
public static class Pawn_GetAcceptArrestChance_Patch
{
    [HarmonyPostfix]
    public static void PsychologyArrestChance(Pawn __instance, Pawn arrester, ref float __result)
    {
        Pawn pawn = __instance;
        if (arrester == null || pawn.relations == null || AlwaysAcceptsArrest(pawn))
        {
            return;
        }
        float chance = arrester.GetStatValue(StatDefOfPsychology.ArrestPeacefullyChance)
            * Mathf.InverseLerp(-100f, 100f, pawn.relations.OpinionOf(arrester))
            * (arrester.Faction == pawn.Faction ? 1.5f : 1f)
            * (pawn.InMentalState ? 0.2f : 1f);
        __result = Mathf.Clamp01(chance * pawn.kindDef.acceptArrestChanceFactor);
    }

    // Mirrors the early returns in vanilla's GetAcceptArrestChance.
    private static bool AlwaysAcceptsArrest(Pawn pawn)
    {
        if (pawn.Downed || pawn.WorkTagIsDisabled(WorkTags.Violent) || (pawn.guilt != null && pawn.guilt.IsGuilty && pawn.IsColonist && !pawn.IsQuestLodger()))
        {
            return true;
        }
        if (ModsConfig.AnomalyActive && pawn.health.hediffSet.HasHediff(HediffDefOf.RevenantHypnosis))
        {
            return true;
        }
        return ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.AggroMentalBreakSelectionChanceFactor <= 0f;
    }
}

// Copies the "peaceful arrest base chance" setting (percent) into the ArrestPeacefullyChance stat's Social skill bonus
// base value, so the stat shown on the info card matches. Run at startup and whenever the settings are saved.
[StaticConstructorOnStartup]
public static class ArrestChanceSetting
{
    private static readonly FieldInfo baseValueField = AccessTools.Field(typeof(SkillNeed_BaseBonus), "baseValue");

    static ArrestChanceSetting()
    {
        Apply();
    }

    public static void Apply()
    {
        StatDef stat = StatDefOfPsychology.ArrestPeacefullyChance;
        if (stat?.skillNeedFactors == null)
        {
            return;
        }
        foreach (SkillNeed need in stat.skillNeedFactors)
        {
            if (need is SkillNeed_BaseBonus baseBonus)
            {
                baseValueField.SetValue(baseBonus, PsychologySettings.arrestBaseChance / 100f);
            }
        }
    }
}

// Bleeding Heart: selling a humanlike gives every colonist/prisoner the "knows a prisoner was sold" thought
// (the ThoughtDef itself is restricted to Bleeding Hearts).
[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreTraded))]
public static class Pawn_PreTradedPatch
{
    [HarmonyPostfix]
    public static void BleedingHeartThought(Pawn __instance, TradeAction action, Pawn playerNegotiator, ITrader trader)
    {
        if (action == TradeAction.PlayerSells)
        {
            if (__instance.RaceProps.Humanlike)
            {
                foreach (Pawn current in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonistsAndPrisoners)
                {
                    current.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.KnowPrisonerSoldBleedingHeart, null);
                }
            }
        }
    }
}
