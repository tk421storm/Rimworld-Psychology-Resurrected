using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology
{
    // Courtship: asking out someone who isn't a partner (RomanceUtility.GetRomanticIntent == Courtship). Weight: the base
    // courtship setting x attraction x opinion (a mild factor: pawns may barely know each other) x romantic drive x confidence x (1 + 0.5 per good date with them) x the
    // cheat chance if the asker has a partner. They don't ask again while "turned down my date" lasts. A yes plans a date
    // through the same scheduling as date night (RendezvousUtility.PlanDate); how the date went is decided in LordJob_Date.
    public class InteractionWorker_AskOutOnDate : InteractionWorker_DateBase
    {
        protected override RomanticIntent Intent => RomanticIntent.Courtship;

        protected override float CooldownDays => 3f;

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (PsychologySettings.courtshipChance <= 0f || !CanPlanDate(initiator, recipient))
            {
                return 0f;
            }
            if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
            {
                return 0f;
            }
            return AskWeight(initiator, recipient, false, out _);
        }

        // The weight before the gates above. With explain, details lists each factor (for the dev romance menu); it's
        // skipped otherwise, as this runs for every pair whenever a pawn picks an interaction.
        public static float AskWeight(Pawn initiator, Pawn recipient, bool explain, out string details)
        {
            CompPsychology comp = PsycheHelper.Comp(initiator);
            float attraction = Mathf.InverseLerp(RomanceUtility.MinAttraction, 1f, initiator.relations.SecondaryRomanceChanceFactor(recipient));
            // Attraction leads; opinion only nudges (0.6 at the minimum romance opinion, 1 at 100).
            float opinion = 0.6f + 0.4f * Mathf.InverseLerp(PsychologySettings.romanceOpinionThreshold, 100f, initiator.relations.OpinionOf(recipient));
            float drive = comp.Sexuality.AdjustedRomanticDrive;
            float confidence = 0.5f + comp.Psyche.GetPersonalityRating(PersonalityNodeDefOf.Confident);
            float history = 1f + 0.5f * RomanceUtility.GoodDateCount(initiator, recipient);
            float cheat = RomanceUtility.CheatFactor(initiator, recipient, out Pawn cheatOn);
            float colony = RendezvousUtility.ColonySizeFactor(initiator);
            details = !explain ? null : $"setting {PsychologySettings.courtshipChance:0.##} x attraction {attraction:0.00} x opinion {opinion:0.00} x romantic drive {drive:0.00} x confidence {confidence:0.00} x history {history:0.00} x cheat {cheat:0.00}{(cheatOn != null ? $" (on {cheatOn})" : "")} x colony size {colony:0.00}";
            return PsychologySettings.courtshipChance * attraction * opinion * drive * confidence * history * cheat * colony;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // The base call clears the outs and lets other mods' hooks on InteractionWorker.Interacted see this interaction
            // (Intimacy only raises its need there).
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            bool accepted = Ask(initiator, recipient, extraSentencePacks, out float chance, out string details);
            PsychologyDebug.Log($"{initiator} asks {recipient} out: accept chance {chance:P0} ({details}) -> {(accepted ? "yes" : "no")}");
        }

        // Rolls the answer, adds it to the log entry, and plans the date or gives the asker the rebuffed memory. Also used
        // by romance attempts routed to courtship (RomanceAttemptRouter).
        public static bool Ask(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out float chance, out string details)
        {
            chance = AcceptChance(recipient, initiator, out details);
            bool accepted = Rand.Chance(chance);
            if (accepted)
            {
                extraSentencePacks.Add(RulePackDefOfPsychology.DateAccepted);
                RendezvousUtility.PlanDate(initiator, recipient, true);
            }
            else
            {
                extraSentencePacks.Add(RulePackDefOfPsychology.DateDeclined);
                initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyDateRequest, recipient);
            }
            return accepted;
        }

        public static float AcceptChance(Pawn recipient, Pawn initiator)
        {
            return AcceptChance(recipient, initiator, out _);
        }

        // The answer. The recipient must have a psyche, be old enough to date and find the asker attractive (MinAttraction).
        // Chance = (0.3 + 0.7 x attraction) x (0.4 + 0.6 x opinion from -20 to 80) x (1 + 0.5 x compatibility)
        // x (1 + 0.25 per good date with them) x the cheat chance if the recipient has a partner. Easier than a romance
        // attempt: it's only a date. A pawn always says yes to the pawn they're psychically bonded with (the bond outlasts
        // a breakup).
        public static float AcceptChance(Pawn recipient, Pawn initiator, out string details)
        {
            if (!PsycheHelper.PsychologyEnabled(recipient) || !SpeciesHelper.RomanceEnabled(recipient, true))
            {
                details = "no psyche, or too young to date";
                return 0f;
            }
            if (recipient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicBond) is Hediff_PsychicBond bond && bond.target == initiator)
            {
                details = "psychically bonded";
                return 1f;
            }
            float romanceFactor = recipient.relations.SecondaryRomanceChanceFactor(initiator);
            string rawAttraction = RomanceUtility.AttractionBreakdown(recipient, initiator);
            if (romanceFactor < RomanceUtility.MinAttraction)
            {
                details = $"not attracted ({rawAttraction})";
                return 0f;
            }
            float attraction = 0.3f + 0.7f * Mathf.InverseLerp(RomanceUtility.MinAttraction, 1f, romanceFactor);
            // Strangers (opinion ~0) are open to a first date: 0.8 at 0, rising to 1 at +40, falling to 0.3 at -30
            float opinionOf = recipient.relations.OpinionOf(initiator);
            float opinion = opinionOf >= 0f ? Mathf.Lerp(0.8f, 1f, Mathf.InverseLerp(0f, 40f, opinionOf))
                : Mathf.Lerp(0.3f, 0.8f, Mathf.InverseLerp(-30f, 0f, opinionOf));
            float compatibility = 1f + 0.5f * RomanceUtility.Compatibility(recipient, initiator);
            float history = 1f + 0.25f * RomanceUtility.GoodDateCount(recipient, initiator);
            float cheat = RomanceUtility.CheatFactor(recipient, initiator, out Pawn cheatOn);
            details = $"attraction {attraction:0.00} ({rawAttraction}) x opinion {opinion:0.00} x compatibility {compatibility:0.00} x history {history:0.00} x cheat {cheat:0.00}{(cheatOn != null ? $" (on {cheatOn})" : "")}";
            return Mathf.Clamp01(attraction * opinion * compatibility * history * cheat);
        }
    }
}
