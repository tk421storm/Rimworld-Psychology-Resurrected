using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Psychology
{
    // A date one pawn has planned (RendezvousUtility.PlanDate). Waits until the planned day and hour, then starts
    // LordJob_Date if both are free (not downed, starving, bleeding, exhausted, having a mental break other than a social fight, working, drafted, or on another map). Sleeping
    // pawns wake up for it unless they're heavy sleepers: if only one turns up the other "missed the date" (if neither,
    // both slept through it); if either isn't free, both get "date cancelled". If either is busy with a ritual or
    // the like (IsBusy), it's pushed back an hour, up to three times, before it's cancelled. A date night is
    // dropped if they stop being partners; a courtship date only if the other pawn is gone. Either one dying drops it. Each outcome, and the start,
    // gets a top-left message.
    public class Hediff_PlannedDate : Hediff
    {
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.partner, "partner");
            Scribe_Values.Look(ref this.hour, "hour", 0);
            Scribe_Values.Look(ref this.day, "day", 0);
            Scribe_Values.Look(ref this.courtship, "courtship", false);
            Scribe_Values.Look(ref this.delays, "delays", 0);
        }


        public override void Tick()
        {
            base.Tick();
            if(this.partner == null || this.partner.Dead || (!this.courtship && !LovePartnerRelationUtility.LovePartnerRelationExists(this.pawn, this.partner)))
            {
                PsychologyDebug.Log($"Planned date of {this.pawn} with {this.partner} dropped (partner gone, or no longer partners)");
                if (this.partner != null && this.pawn.Spawned)
                {
                    RendezvousUtility.DateMessage("DateCalledOff", this.pawn, this.partner, this.courtship, MessageTypeDefOf.NeutralEvent);
                }
                this.pawn.health.RemoveHediff(this);
            }
            else if(Find.TickManager.TicksAbs >= this.day && GenLocalDate.HourOfDay(this.pawn) == this.hour)
            {
                string cancelReason = CancelReason(pawn, partner) ?? CancelReason(partner, pawn);
                Pawn busy = cancelReason == null ? (IsBusy(pawn) ? pawn : IsBusy(partner) ? partner : null) : null;
                if (busy != null)
                {
                    // Busy with a ritual, a caravan forming...: an hour later, up to MaxDelays times, then cancelled.
                    if (this.delays < MaxDelays)
                    {
                        this.delays++;
                        this.hour = (this.hour + 1) % GenDate.HoursPerDay;
                        this.day = Find.TickManager.TicksAbs + 1;
                        PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.pawn} and {this.partner} pushed back to hour {this.hour}: {busy} is busy ({busy.GetLord().LordJob.GetType().Name}), delay {this.delays}/{MaxDelays}");
                        return;
                    }
                    cancelReason = "DateReasonBusy".Translate(busy);
                }
                if(cancelReason == null)
                {
                    // Sleepers get up for the date (stopping their job below wakes them), except heavy sleepers, who
                    // sleep through it. Decide who turns up before stopping any jobs.
                    bool pawnAwake = WakesForDate(pawn);
                    bool partnerAwake = WakesForDate(partner);
                    PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.pawn} and {this.partner}: {(pawnAwake && partnerAwake ? "starting" : "missed (heavy sleeper slept through)")}");
                    if (pawnAwake && partnerAwake)
                    {
                        pawn.jobs.StopAll();
                        partner.jobs.StopAll();
                        if(pawn.GetLord() != null)
                        {
                            pawn.GetLord().Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                        }
                        if (partner.GetLord() != null)
                        {
                            partner.GetLord().Notify_PawnLost(partner, PawnLostCondition.ForcedToJoinOtherLord);
                        }
                        LordMaker.MakeNewLord(this.pawn.Faction, new LordJob_Date(this.pawn, this.partner, this.courtship), this.pawn.Map, new Pawn[] { this.pawn, this.partner });
                        RendezvousUtility.DateMessage("DateStarted", this.pawn, this.partner, this.courtship, MessageTypeDefOf.PositiveEvent);
                    }
                    else if(pawnAwake)
                    {
                        this.pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.MissedDate, this.partner);
                        RendezvousUtility.DateMessage("DateMissed", this.pawn, this.partner, this.courtship, MessageTypeDefOf.NegativeEvent);
                    }
                    else if (partnerAwake)
                    {
                        this.partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.MissedDate, this.pawn);
                        RendezvousUtility.DateMessage("DateMissed", this.partner, this.pawn, this.courtship, MessageTypeDefOf.NegativeEvent);
                    }
                    else
                    {
                        RendezvousUtility.DateMessage("DateBothSlept", this.pawn, this.partner, this.courtship, MessageTypeDefOf.NeutralEvent);
                    }
                }
                else
                {
                    PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.pawn} and {this.partner} cancelled: {cancelReason}");
                    RendezvousUtility.DateMessage("DateCancelled", this.pawn, this.partner, this.courtship, MessageTypeDefOf.NeutralEvent, cancelReason);
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DateCancelled);
                    this.partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DateCancelled);
                }
                this.pawn.health.RemoveHediff(this);
            }
        }

        // Dead pawns' hediffs don't tick, so the partner's side of the plan is handled here: "date called off", and the
        // plan goes. (Vanilla calls this for each hediff from last to first, so removing it is safe.)
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            PsychologyDebug.Log($"Planned date of {this.pawn} with {this.partner} dropped ({this.pawn} died)");
            if (this.partner != null && !this.partner.Dead && this.partner.Spawned && this.partner.Faction == Faction.OfPlayer)
            {
                RendezvousUtility.DateMessage("DateCalledOff", this.pawn, this.partner, this.courtship, MessageTypeDefOf.NeutralEvent);
            }
            this.pawn.health.RemoveHediff(this);
        }

        // Dev romance menu: start on the next tick.
        public void StartNow()
        {
            this.day = Find.TickManager.TicksAbs;
            this.hour = GenLocalDate.HourOfDay(this.pawn);
        }

        // Whether p turns up: awake, or asleep but not a heavy sleeper (Psychology's, or Vanilla Traits Expanded's).
        private static bool WakesForDate(Pawn p)
        {
            if (p.Awake())
            {
                return true;
            }
            TraitSet traits = p.story?.traits;
            if (traits == null)
            {
                return true;
            }
            TraitDef vteHeavySleeper = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_HeavySleeper");
            return !traits.HasTrait(TraitDefOfPsychology.HeavySleeper) && (vteHeavySleeper == null || !traits.HasTrait(vteHeavySleeper));
        }

        // In a lord the date mustn't pull them out of: anything but a gathering anyone can join or leave (parties) or a
        // hang-out. Starting the date would take them out of a ritual, a caravan forming and so on.
        public static bool IsBusy(Pawn p)
        {
            LordJob job = p.GetLord()?.LordJob;
            return job != null && !(job is LordJob_VoluntarilyJoinable) && !(job is LordJob_HangOut);
        }

        // When the date will start (absolute ticks): the first planned hour from `day` (or now, if that's passed) on.
        public int StartTick => RendezvousUtility.DateStartTick(Math.Max(this.day, Find.TickManager.TicksAbs), this.hour, this.pawn.MapHeld ?? this.partner?.MapHeld);

        // Why p can't go on the date right now (translated, for the cancelled message), or null if they can.
        private static string CancelReason(Pawn p, Pawn partner)
        {
            string key = p.Map != partner.Map ? "DateReasonAway"
                : p.Downed ? "DateReasonDowned"
                : p.needs != null && p.needs.food.Starving ? "DateReasonStarving"
                : p.health.hediffSet.BleedRateTotal > 0f ? "DateReasonBleeding"
                : p.needs.rest?.CurLevel < RendezvousUtility.DateTooTiredRest ? "DateReasonTired"
                : RendezvousUtility.EndsOuting(p.MentalStateDef) ? "DateReasonMentalState"
                : p.IsPrisoner ? "DateReasonPrisoner"
                : p.GetTimeAssignment() == TimeAssignmentDefOf.Work ? "DateReasonWork"
                : p.Drafted ? "DateReasonDrafted"
                : null;
            return key == null ? null : (string)key.Translate(p);
        }

        private const int MaxDelays = 3;

        public Pawn partner;
        public int hour;
        public int day;
        public bool courtship;
        // Hours it has been pushed back because one of them was busy (IsBusy).
        public int delays;
    }
}
