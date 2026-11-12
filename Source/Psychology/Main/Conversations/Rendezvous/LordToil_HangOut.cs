using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace Psychology
{
    // Shared toil for dates and hang-outs; also holds the pair's current activity (see DateActivity).
    public class LordToil_HangOut : LordToil
    {
        public LordToil_HangOut(Pawn[] pawns)
        {
            this.friends = pawns;
        }

        // Keeps the pair talking to each other, not to whoever happens to be nearby: vanilla's most sociable rate (an
        // interaction every 550 ticks on average, checked every 60), picking the interaction by the usual weights, as long
        // as both can talk and are close enough.
        public override void LordToilTick()
        {
            if (this.friends == null || this.friends.Length < 2 || !this.friends[0].IsHashIntervalTick(60) || !Rand.MTBEventOccurs(550f, 1f, 60f))
            {
                return;
            }
            Pawn initiator = Rand.Bool ? this.friends[0] : this.friends[1];
            Pawn recipient = initiator == this.friends[0] ? this.friends[1] : this.friends[0];
            if (!initiator.Spawned || !recipient.Spawned || initiator.interactions == null || initiator.interactions.InteractedTooRecentlyToInteract())
            {
                return;
            }
            if (!SocialInteractionUtility.CanInitiateRandomInteraction(initiator) || !SocialInteractionUtility.CanReceiveRandomInteraction(recipient))
            {
                return;
            }
            if (DefDatabase<InteractionDef>.AllDefsListForReading.TryRandomElementByWeight(x => initiator.interactions.CanInteractNowWith(recipient, x) ? x.Worker.RandomSelectionWeight(initiator, recipient) : 0f, out InteractionDef interaction))
            {
                PsychologyDebug.Log($"Outing talk: {initiator} -> {recipient}: {interaction.defName}");
                initiator.interactions.TryInteractWith(recipient, interaction);
            }
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOfPsychology.HangOut, this.friends[0].Position, this.friends[1].Position, -1f);
            }
        }

        // The pair's current activity (see DateActivity), shared by both pawns' JobGiver_SpendTimeTogether. When it has
        // ended, a new one is picked: first any that's urgent (DateActivityWorker.Urgent, e.g. a meal when hungry), then
        // weighted by DateActivityWorker.Weight, a repeat of the last one less likely. If nothing can be done together,
        // they don't look again for half an hour. Not saved: after loading, a new one is picked. A worker that throws is
        // logged once and skipped, so one broken activity can't stall dates; with debug logging on, each pick logs the
        // options, what couldn't be done, and what started.
        public DateActivity CurrentActivity(Pawn pawn)
        {
            if (this.activity != null && !this.activity.Ended)
            {
                return this.activity;
            }
            this.activity = null;
            if (Find.TickManager.TicksGame < this.nextPickTick)
            {
                return null;
            }
            Pawn other = pawn == this.friends[0] ? this.friends[1] : this.friends[0];
            Pawn leader = Rand.Bool ? pawn : other;
            Pawn partner = leader == pawn ? other : pawn;
            List<string> failed = new List<string>();
            foreach (DateActivityDef d in DefDatabase<DateActivityDef>.AllDefs)
            {
                if (Safely(d, "Urgent", () => d.Worker.Urgent(leader, partner), false) && Safely(d, "CanDo", () => d.Worker.CanDo(leader, partner), false))
                {
                    DateActivity next = new DateActivity(d, leader, partner);
                    if (Safely(d, "TryStart", () => d.Worker.TryStart(next), false))
                    {
                        return Start(next, other, failed);
                    }
                    failed.Add(d.defName + " (urgent)");
                }
            }
            Dictionary<DateActivityDef, float> options = new Dictionary<DateActivityDef, float>();
            List<string> unavailable = new List<string>();
            foreach (DateActivityDef d in DefDatabase<DateActivityDef>.AllDefs)
            {
                if (!Safely(d, "CanDo", () => d.Worker.CanDo(leader, partner), false))
                {
                    unavailable.Add(d.defName);
                    continue;
                }
                float weight = Safely(d, "Weight", () => d.Worker.Weight(leader, partner), 0f) * (d == this.lastActivity ? 0.25f : 1f);
                if (weight <= 0f)
                {
                    unavailable.Add(d.defName + " (weight 0)");
                    continue;
                }
                options[d] = weight;
            }
            PsychologyDebug.Log($"Date activity: picking for {leader} and {partner}: options {(options.Any() ? string.Join(", ", options.Select(o => $"{o.Key.defName} {o.Value:0.00}{(o.Key == this.lastActivity ? " (last)" : "")}")) : "none")}; can't do {(unavailable.Any() ? string.Join(", ", unavailable) : "-")}");
            while (options.Keys.TryRandomElementByWeight(d => options[d], out DateActivityDef def))
            {
                options.Remove(def);
                DateActivity next = new DateActivity(def, leader, partner);
                if (Safely(def, "TryStart", () => def.Worker.TryStart(next), false))
                {
                    return Start(next, other, failed);
                }
                failed.Add(def.defName);
            }
            PsychologyDebug.Log($"Date activity: nothing {leader} and {partner} can do together right now{(failed.Any() ? "; couldn't start " + string.Join(", ", failed) : "")}");
            this.nextPickTick = Find.TickManager.TicksGame + GenDate.TicksPerHour / 2;
            return null;
        }

        private DateActivity Start(DateActivity next, Pawn other, List<string> failed)
        {
            this.activity = next;
            this.lastActivity = next.def;
            // TryStart may have swapped leader and partner.
            PsychologyDebug.Log($"Date activity: {next.leader} and {next.partner} start {next.def.defName}{(next.joyGiver != null ? " (" + next.joyGiver.defName + ")" : "")}{(failed.Any() ? "; couldn't start " + string.Join(", ", failed) : "")}");
            // Fetch the other pawn from hanging around, so both start now.
            Job otherJob = other.CurJob;
            if (otherJob != null && (otherJob.def == JobDefOf.Wait || otherJob.def == JobDefOf.GotoWander || otherJob.def == JobDefOf.Goto || otherJob.jobGiver is JobGiver_StandAndBeSociallyActive))
            {
                other.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            return this.activity;
        }

        // Runs one step of an activity's worker; if it throws, logs the error once per activity and step and returns
        // the fallback.
        private static T Safely<T>(DateActivityDef def, string step, Func<T> call, T fallback)
        {
            try
            {
                return call();
            }
            catch (Exception e)
            {
                Log.ErrorOnce($"[Psychology] Date activity {def.defName} threw in {step}: {e}", Gen.HashCombineInt(def.shortHash, step.GetHashCode()));
                return fallback;
            }
        }

        public Pawn[] friends;
        public DateActivity activity;
        public DateActivityDef lastActivity;
        public int nextPickTick = 0;
    }
}
