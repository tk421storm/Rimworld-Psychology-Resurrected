using System;
using System.Collections.Generic;
using Verse;
using HarmonyLib;
using RimWorld;
using System.Text;
using UnityEngine;

namespace Psychology.Harmony;

[HarmonyPatch(typeof(ConversionUtility), nameof(ConversionUtility.ConversionPowerFactor_MemesVsTraits))]
public static class ConversionUtility_ConversionPowerFactor_MemesVsTraits_Patch
{
    public static bool needToAddToSB = true;

    // Note that sb is a reference class that gets changed by this
    [HarmonyPostfix]
    public static void ConversionPowerFactor_MemesVsTraits(ref float __result, Pawn initiator, Pawn recipient, StringBuilder sb)
    {
        // Both pawns need a psyche (non-sapient or psyche-disabled species would otherwise throw here); leave vanilla's
        // factor and breakdown untouched when either lacks one.
        if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
        {
            return;
        }
        float initWithInitIdeo = 0.5f * PsychologySettings.ideoPsycheMultiplier * PsycheHelper.Comp(initiator).Psyche.CompatibilityWithIdeo(initiator.Ideo);
        float initWithReciIdeo = 0.5f * PsychologySettings.ideoPsycheMultiplier * PsycheHelper.Comp(initiator).Psyche.CompatibilityWithIdeo(recipient.Ideo);
        float reciWithInitIdeo = 1.0f * PsychologySettings.ideoPsycheMultiplier * PsycheHelper.Comp(recipient).Psyche.CompatibilityWithIdeo(initiator.Ideo);
        float reciWithReciIdeo = 1.0f * PsychologySettings.ideoPsycheMultiplier * PsycheHelper.Comp(recipient).Psyche.CompatibilityWithIdeo(recipient.Ideo);
        float additiveFactor =  initWithInitIdeo - initWithReciIdeo + reciWithInitIdeo - reciWithReciIdeo;
        float multiplicativeFactor = additiveFactor > 0f ? 1f + additiveFactor : 1f / (1f - additiveFactor);
        __result *= multiplicativeFactor;
        if (sb == null)
        {
            needToAddToSB = true;
            return;
        }
        if (!needToAddToSB)
        {
            return;
        }
        string text = string.Empty;
        NamedArgument initName = initiator.Named("PAWN");
        NamedArgument reciName = recipient.Named("PAWN");
        NamedArgument initIdeo = initiator.Ideo.Named("IDEO");
        NamedArgument reciIdeo = recipient.Ideo.Named("IDEO");
        text += PawnCompatWithIdeoText(reciName, initIdeo, reciWithInitIdeo, true);
        text += PawnCompatWithIdeoText(reciName, reciIdeo, reciWithReciIdeo, false);
        text += PawnCompatWithIdeoText(initName, initIdeo, initWithInitIdeo, true);
        text += PawnCompatWithIdeoText(initName, reciIdeo, initWithReciIdeo, false);
        sb.AppendInNewLine(" -  " + "AbilityIdeoConvertBreakdownPsychologyEffects".Translate() + ": " + multiplicativeFactor.ToStringPercent() + text);
        // #15: the personality factor on certainty loss (InteractionWorker_ConvertIdeoAttempt_CertaintyReduction_Patch).
        sb.AppendInNewLine(ConversionPersonality.BreakdownLine(initiator, recipient));
        needToAddToSB = false;
    }
    public static string PawnCompatWithIdeoText(NamedArgument pawnName, NamedArgument ideoName, float compat, bool isInitIdeo)
    {
        string compatText = compat > 0 ? "AbilityIdeoConvertBreakdownPawnCompatWithIdeo" : "AbilityIdeoConvertBreakdownPawnIncompatWithIdeo";
        float compatSigned = isInitIdeo ? compat : -compat;
        string compatPercent = compatSigned > 0 ? "+" + compatSigned.ToStringPercent() : compatSigned.ToStringPercent();
        return "\n   -  " + compatText.Translate(pawnName, ideoName) + ": " + compatPercent;
    }
}
