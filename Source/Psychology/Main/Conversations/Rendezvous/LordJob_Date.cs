using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;

namespace Psychology
{
    // A 4-6 hour date. Ends early on danger, a new injury, a mental break (which ruins it for the other: a bad date memory),
    // drafting, work time or exhaustion. Finishing any date, date night or courtship, gives each a good or bad date memory
    // scaled by how it went for them (RomanceUtility.DateQuality). A courtship date is also put on record, and if it went
    // well for both it may end in a romance attempt. The outcome (or why it was cut short) is shown as a top-left message.
    // Partners may be invited to bed once per date (lovinAsked; RomanceAttemptRouter, DateLovin).
    class LordJob_Date : LordJob
    {
        public LordJob_Date()
        { }

        public LordJob_Date(Pawn initiator, Pawn recipient, bool courtship)
        {
            this.initiator = initiator;
            this.recipient = recipient;
            this.courtship = courtship;
            // Only injuries during the date end it; old scars or missing parts would otherwise end it at once.
            this.initiatorHealth = initiator.health.summaryHealth.SummaryHealthPercent;
            this.recipientHealth = recipient.health.summaryHealth.SummaryHealthPercent;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_HangOut lordToil_HangOut = new LordToil_HangOut(new Pawn[] { initiator, recipient });
            stateGraph.AddToil(lordToil_HangOut);
            LordToil_End lordToil_End = new LordToil_End();
            stateGraph.AddToil(lordToil_End);
            Transition transition = new Transition(lordToil_HangOut, lordToil_End);
            transition.AddTrigger(new Trigger_TickCondition(() => this.ShouldBeCalledOff()));
            transition.AddTrigger(new Trigger_TickCondition(() => RendezvousUtility.HurtSince(this.initiator, this.initiatorHealth) || RendezvousUtility.HurtSince(this.recipient, this.recipientHealth)));
            transition.AddTrigger(new Trigger_TickCondition(() => this.initiator.Drafted || this.recipient.Drafted));
            transition.AddTrigger(new Trigger_PawnLost());
            transition.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                this.CutShort();
            }));
            stateGraph.AddTransition(transition);
            this.timeoutTrigger = new Trigger_TicksPassed(Rand.RangeInclusive(GenDate.TicksPerHour*4, GenDate.TicksPerHour*6));
            Transition transition2 = new Transition(lordToil_HangOut, lordToil_End);
            transition2.AddTrigger(this.timeoutTrigger);
            transition2.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                this.Finished();
            }));
            stateGraph.AddTransition(transition2);
            return stateGraph;
        }
        public void Finished()
        {
            if (this.courtship)
            {
                FinishedCourtship();
                return;
            }
            // Rated like a courtship date. (WentOnDate, the old flat memory, is kept only so old saves load.)
            float initiatorQuality = RomanceUtility.DateQuality(this.initiator, this.recipient);
            float recipientQuality = RomanceUtility.DateQuality(this.recipient, this.initiator);
            RomanceUtility.GainDateMemory(this.initiator, this.recipient, initiatorQuality);
            RomanceUtility.GainDateMemory(this.recipient, this.initiator, recipientQuality);
            OutcomeMessage(initiatorQuality, recipientQuality);
            PsychologyDebug.Log($"Date night of {this.initiator} and {this.recipient} finished: quality {initiatorQuality:0.00} / {recipientQuality:0.00} (compatibility {RomanceUtility.Compatibility(this.initiator, this.recipient):0.00})");
        }

        // How it went: well for both, well for one (named first), or badly. Courtship dates and date nights have their own
        // keys (DateWentWell / DateNightWentWell...).
        private void OutcomeMessage(float initiatorQuality, float recipientQuality)
        {
            string outcome = initiatorQuality > 0f && recipientQuality > 0f ? "WentWell" : initiatorQuality > 0f || recipientQuality > 0f ? "WentWellForOne" : "WentBadly";
            bool initiatorFirst = initiatorQuality > 0f || recipientQuality <= 0f;
            RendezvousUtility.DateMessage((this.courtship ? "Date" : "DateNight") + outcome, initiatorFirst ? this.initiator : this.recipient, initiatorFirst ? this.recipient : this.initiator, this.courtship, outcome == "WentBadly" ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
        }

        // Message for a date that ended before its time, with the first reason that applies. One pawn's mental break
        // instead ruins the date for the other (RuinedBy).
        private void CutShort()
        {
            bool initiatorBroke = RendezvousUtility.EndsOuting(this.initiator.MentalStateDef);
            if (initiatorBroke != RendezvousUtility.EndsOuting(this.recipient.MentalStateDef))
            {
                this.RuinedBy(initiatorBroke ? this.initiator : this.recipient);
                return;
            }
            string reason = null;
            foreach (Pawn p in new Pawn[] { this.initiator, this.recipient })
            {
                if (reason != null)
                {
                    break;
                }
                float healthAtStart = p == this.initiator ? this.initiatorHealth : this.recipientHealth;
                reason = RendezvousUtility.EndsOuting(p.MentalStateDef) ? "DateReasonMentalState".Translate(p)
                    : p.Dead || !p.Spawned || !this.lord.ownedPawns.Contains(p) ? "DateReasonLeft".Translate(p)
                    : p.Drafted ? "DateReasonDrafted".Translate(p)
                    : RendezvousUtility.HurtSince(p, healthAtStart) ? "DateReasonHurt".Translate(p)
                    : p.GetTimeAssignment() == TimeAssignmentDefOf.Work ? "DateReasonWork".Translate(p)
                    : p.needs.rest?.CurLevel < RendezvousUtility.DateTooTiredRest ? "DateReasonTired".Translate(p)
                    : (string)null;
            }
            reason ??= "DateReasonDanger".Translate();
            PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.initiator} and {this.recipient} cut short: {reason}");
            RendezvousUtility.DateMessage("DateCutShort", this.initiator, this.recipient, this.courtship, MessageTypeDefOf.NeutralEvent, reason);
        }

        // The breaker's mental break ruined the date for the other: a bad date memory of the breaker, at least
        // RuinedDateQuality bad (worse if it was going badly anyway), and on record for a courtship date. Nothing for the
        // breaker, who's busy with their break.
        private void RuinedBy(Pawn breaker)
        {
            Pawn other = breaker == this.initiator ? this.recipient : this.initiator;
            float quality = Mathf.Min(RomanceUtility.DateQuality(other, breaker), RuinedDateQuality);
            if (this.courtship)
            {
                PsycheHelper.GameComp.RecordDate(other, breaker, quality);
            }
            RomanceUtility.GainDateMemory(other, breaker, quality);
            PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.initiator} and {this.recipient} ruined by {breaker}'s mental break ({breaker.MentalStateDef}): quality {quality:0.00} for {other}");
            RendezvousUtility.DateMessage("DateRuined", breaker, other, this.courtship, MessageTypeDefOf.NegativeEvent);
        }

        private void FinishedCourtship()
        {
            // Count earlier good dates first, so this one doesn't count towards its own romance roll.
            float previousGoodDates = (RomanceUtility.GoodDateCount(this.initiator, this.recipient) + RomanceUtility.GoodDateCount(this.recipient, this.initiator)) / 2f;
            float initiatorQuality = RomanceUtility.DateQuality(this.initiator, this.recipient);
            float recipientQuality = RomanceUtility.DateQuality(this.recipient, this.initiator);
            PsycheHelper.GameComp.RecordDate(this.initiator, this.recipient, initiatorQuality);
            PsycheHelper.GameComp.RecordDate(this.recipient, this.initiator, recipientQuality);
            RomanceUtility.GainDateMemory(this.initiator, this.recipient, initiatorQuality);
            RomanceUtility.GainDateMemory(this.recipient, this.initiator, recipientQuality);
            OutcomeMessage(initiatorQuality, recipientQuality);
            PsychologyDebug.Log($"Date of {this.initiator} and {this.recipient} finished: quality {initiatorQuality:0.00} / {recipientQuality:0.00} (compatibility {RomanceUtility.Compatibility(this.initiator, this.recipient):0.00}), earlier good dates {previousGoodDates:0.#}");
            if (initiatorQuality <= 0f || recipientQuality <= 0f || LovePartnerRelationUtility.LovePartnerRelationExists(this.initiator, this.recipient))
            {
                PsychologyDebug.Log("  no romance roll (the date didn't go well for both, or they're already partners)");
                return;
            }
            float chance = RomanceUtility.RomanceChanceAfterDate(this.initiator, this.recipient, previousGoodDates);
            bool roll = Rand.Chance(chance);
            PsychologyDebug.Log($"  romance chance {chance:P0} (opinions {this.initiator.relations.OpinionOf(this.recipient)} / {this.recipient.relations.OpinionOf(this.initiator)}) -> {(roll ? "romance attempt" : "not this time")}");
            if (roll)
            {
                RomanceUtility.TryFireRomanceAttempt(this.initiator, this.recipient);
            }
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref this.initiator, "initiator");
            Scribe_References.Look(ref this.recipient, "recipient");
            Scribe_Values.Look(ref this.courtship, "courtship", false);
            Scribe_Values.Look(ref this.initiatorHealth, "initiatorHealth", 1f);
            Scribe_Values.Look(ref this.recipientHealth, "recipientHealth", 1f);
            Scribe_Values.Look(ref this.lovinAsked, "lovinAsked", false);
        }
        
        public override string GetReport(Pawn pawn)
        {
            return "LordReportDate".Translate();
        }

        // A mental break ends the date (Trigger_PawnLost fires), even one that isn't violent: hallucinations or a daze
        // wander off too. A social fight, even with each other, doesn't (RendezvousUtility.EndsOuting).
        public override void Notify_InMentalState(Pawn pawn, MentalStateDef stateDef)
        {
            if (RendezvousUtility.EndsOuting(stateDef))
            {
                this.lord.Notify_PawnLost(pawn, PawnLostCondition.InMentalState);
            }
        }
        
        private bool ShouldBeCalledOff()
        {
            return !GatheringsUtility.AcceptableGameConditionsToContinueGathering(base.Map) || this.initiator.GetTimeAssignment() == TimeAssignmentDefOf.Work || this.recipient.GetTimeAssignment() == TimeAssignmentDefOf.Work || this.initiator.needs.rest?.CurLevel < RendezvousUtility.DateTooTiredRest || this.recipient.needs.rest?.CurLevel < RendezvousUtility.DateTooTiredRest;
        }
        
        private const float RuinedDateQuality = -0.5f;

        private Trigger_TicksPassed timeoutTrigger;
        public Pawn initiator;
        public Pawn recipient;
        public bool courtship;
        public float initiatorHealth = 1f;
        public float recipientHealth = 1f;
        public bool lovinAsked;
    }
}
