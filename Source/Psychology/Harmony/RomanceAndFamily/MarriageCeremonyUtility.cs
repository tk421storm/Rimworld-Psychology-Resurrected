using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Same for getting married: old break-up/rejection thoughts between the couple are cleared via RomanceHelperMethods.
// Both spouses' date histories are cleared too (PsychologyGameComponent). A Highmate bonds with their spouse at the
// wedding, since the bond waits for marriage (InteractionWorker_RomanceAttempt_TryCreatePsychicBondPatch).
[HarmonyPatch(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married))]
public static class MarriageCeremonyUtility_MarriedPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
    }

    [HarmonyPostfix]
    public static void ClearDateHistories(Pawn firstPawn, Pawn secondPawn)
    {
        PsycheHelper.GameComp.ClearDateHistory(firstPawn);
        PsycheHelper.GameComp.ClearDateHistory(secondPawn);
    }

    // Vanilla's letter for a bond formed at lovin, which doesn't mention lovin. BONDPAWN is the Highmate.
    [HarmonyPostfix]
    public static void BondHighmate(Pawn firstPawn, Pawn secondPawn)
    {
        if (!InteractionWorker_RomanceAttempt.CanCreatePsychicBondBetween(firstPawn, secondPawn) || !InteractionWorker_RomanceAttempt.TryCreatePsychicBondBetween(firstPawn, secondPawn))
        {
            return;
        }
        Pawn bondPawn = firstPawn.genes?.GetFirstGeneOfType<Gene_PsychicBonding>() != null ? firstPawn : secondPawn;
        Pawn otherPawn = bondPawn == firstPawn ? secondPawn : firstPawn;
        PsychologyDebug.Log($"Psychic bond: {bondPawn} bonded with {otherPawn} at their wedding");
        if (PawnUtility.ShouldSendNotificationAbout(firstPawn) || PawnUtility.ShouldSendNotificationAbout(secondPawn))
        {
            Find.LetterStack.ReceiveLetter("LetterPsychicBondCreatedLovinLabel".Translate(), "LetterPsychicBondCreatedLovinText".Translate(bondPawn.Named("BONDPAWN"), otherPawn.Named("OTHERPAWN")), LetterDefOf.PositiveEvent, new LookTargets(firstPawn, secondPawn));
        }
    }
}

