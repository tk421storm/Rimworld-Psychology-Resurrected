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
    // A 3-5 hour hang-out; same rules as LordJob_Date (only injuries during it end it early, as does a mental break), "hung out" memory at the
    // end. Unlike dates, no message when it's cut short.
    class LordJob_HangOut : LordJob
    {
        public LordJob_HangOut()
        { }

        public LordJob_HangOut(Pawn initiator, Pawn recipient)
        {
            this.initiator = initiator;
            this.recipient = recipient;
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
            transition.AddTrigger(new Trigger_TickCondition(() => this.initiator.Map == null || this.recipient.Map == null));
            transition.AddTrigger(new Trigger_PawnLost());
            stateGraph.AddTransition(transition);
            this.timeoutTrigger = new Trigger_TicksPassed(Rand.RangeInclusive(GenDate.TicksPerHour*3, GenDate.TicksPerHour*5));
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
            this.initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.HungOut, this.recipient);
            this.recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.HungOut, this.initiator);
        }
        
        public override void ExposeData()
        {
            Scribe_References.Look(ref this.initiator, "initiator");
            Scribe_References.Look(ref this.recipient, "recipient");
            Scribe_Values.Look(ref this.initiatorHealth, "initiatorHealth", 1f);
            Scribe_Values.Look(ref this.recipientHealth, "recipientHealth", 1f);
        }
        
        public override string GetReport(Pawn pawn)
        {
            return "LordReportHangingOut".Translate();
        }

        // A mental break ends the hang-out, as for dates (not a social fight).
        public override void Notify_InMentalState(Pawn pawn, MentalStateDef stateDef)
        {
            if (RendezvousUtility.EndsOuting(stateDef))
            {
                this.lord.Notify_PawnLost(pawn, PawnLostCondition.InMentalState);
            }
        }
        
        private bool ShouldBeCalledOff()
        {
            return !GatheringsUtility.AcceptableGameConditionsToContinueGathering(base.Map)
                || this.initiator.GetTimeAssignment() == TimeAssignmentDefOf.Work
                || this.recipient.GetTimeAssignment() == TimeAssignmentDefOf.Work
                || this.initiator.needs.rest?.CurLevel < 0.3f
                || this.recipient.needs.rest?.CurLevel < 0.3f
                || (this.initiator.needs.rest == null && this.recipient.needs.rest == null && this.initiator.needs.joy?.CurLevel > 0.9f && this.recipient.needs.joy?.CurLevel > 0.9f);
        }

        private Trigger_TicksPassed timeoutTrigger;
        public Pawn initiator;
        public Pawn recipient;
        public float initiatorHealth = 1f;
        public float recipientHealth = 1f;
    }
}
