using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    // Something two pawns do together on a date or hang-out (playing a game, a walk, reading side by side...). Each
    // activity is a def with a worker, so a new one is an XML entry plus a small DateActivityWorker subclass.
    //   workerClass:  the DateActivityWorker that hands out the jobs
    //   commonality:  base weight when picking the next activity
    //   joyGiver:     optional vanilla joy giver the worker builds on; both pawns must be allowed it (CanBeGivenTo),
    //                 and the weight is scaled by how tired both are of its joy kind
    public class DateActivityDef : Def
    {
        public Type workerClass;
        public float commonality = 1f;
        public JoyGiverDef joyGiver;

        [Unsaved]
        private DateActivityWorker workerInt;

        public DateActivityWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (DateActivityWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (workerClass == null || !typeof(DateActivityWorker).IsAssignableFrom(workerClass))
            {
                yield return "workerClass must be a DateActivityWorker";
            }
        }
    }

    // Hands out the jobs for one kind of activity. TryStart sets up the activity (finds the game, the book, the walk...)
    // and returns false if it can't happen now; JobFor then gives each pawn its next job whenever that pawn needs one,
    // and returns null once the pawn is done. The activity ends for both as soon as either is done.
    public abstract class DateActivityWorker
    {
        public DateActivityDef def;

        public virtual bool CanDo(Pawn leader, Pawn partner)
        {
            JoyGiver giver = this.def.joyGiver?.Worker;
            return giver == null || (giver.CanBeGivenTo(leader) && giver.CanBeGivenTo(partner));
        }

        public virtual float Weight(Pawn leader, Pawn partner)
        {
            JoyKindDef kind = this.def.joyGiver?.jobDef?.joyKind;
            if (kind == null)
            {
                return this.def.commonality;
            }
            return this.def.commonality * (ToleranceFactor(leader, kind) + ToleranceFactor(partner, kind)) / 2f;
        }

        // True if the pair should do this before anything else (e.g. eat when hungry): it's then tried first, whatever its
        // weight.
        public virtual bool Urgent(Pawn leader, Pawn partner)
        {
            return false;
        }

        public abstract bool TryStart(DateActivity activity);

        public abstract Job JobFor(DateActivity activity, Pawn pawn);

        // Called once when the activity ends, for cleanup.
        public virtual void End(DateActivity activity)
        {
        }

        protected static float ToleranceFactor(Pawn pawn, JoyKindDef kind)
        {
            return pawn.needs?.joy?.tolerances?.JoyFactorFromTolerance(kind) ?? 1f;
        }

        // Spots (seats, watch cells...) are kept this close and in sight of each other so the two can talk: vanilla allows
        // 6 cells (InteractionUtility.MaxInteractRange), less a little for where they end up standing.
        protected const float TalkingRange = 5f;

        protected static bool InTalkingRange(IntVec3 a, IntVec3 b, Map map)
        {
            return a.InHorDistOf(b, TalkingRange) && GenSight.LineOfSight(a, b, map, true);
        }

        // Where the activity has put the pawn: the seat or spot of the last job it gave them (its B target), or Invalid if
        // it hasn't given them one yet.
        protected static IntVec3 SpotOf(DateActivity activity, Pawn pawn)
        {
            LocalTargetInfo spot = activity.LastJobSpot(pawn);
            return spot.IsValid ? spot.Cell : IntVec3.Invalid;
        }

        // A candidate cell within talking range of `near` (and not on it) that `usable` allows, preferably right beside it.
        protected static bool TryFindSpotNear(IntVec3 near, Map map, IEnumerable<IntVec3> candidates, Predicate<IntVec3> usable, out IntVec3 spot)
        {
            List<IntVec3> close = candidates.Where(c => c != near && InTalkingRange(c, near, map) && usable(c)).ToList();
            return close.TryRandomElementByWeight(c => c.InHorDistOf(near, 2.3f) ? 4f : 1f, out spot);
        }

        // For the second of the two to get their job: if vanilla's spot for them (the job's B target) is out of talking
        // range of the other's, a closer one among the candidates. False if the spot is fine, or there's no closer one
        // (then they keep vanilla's).
        protected static bool TryFindSpotNearOther(DateActivity activity, Pawn pawn, Job job, IEnumerable<IntVec3> candidates, Predicate<IntVec3> usable, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            IntVec3 other = SpotOf(activity, activity.Other(pawn));
            if (!other.IsValid || !job.targetB.IsValid || InTalkingRange(job.targetB.Cell, other, pawn.Map))
            {
                return false;
            }
            bool found = TryFindSpotNear(other, pawn.Map, candidates, usable, out spot);
            PsychologyDebug.Log($"Date activity {activity.def.defName}: {pawn}'s spot {job.targetB.Cell} is out of talking range of {activity.Other(pawn)}'s {other}; {(found ? "moved to " + spot : "no closer spot")}");
            return found;
        }

        // Stand still facing the other pawn for a moment, e.g. until they're ready.
        protected static Job WaitFor(Pawn other, int ticks = 120)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait, other);
            job.expiryInterval = ticks;
            return job;
        }
    }

    // One running activity: who leads (the pawn the activity was set up for; TryStart may swap the two, e.g. so the better
    // musician plays), the shared target, and per pawn the last job given and whether it's done. Kept in LordToil_HangOut; not saved, so after loading a new activity is picked.
    public class DateActivity
    {
        // Safety net: nothing a date does should take this long.
        private const int MaxDurationTicks = GenDate.TicksPerHour * 3;

        public DateActivityDef def;
        public Pawn leader;
        public Pawn partner;
        // For the worker: the shared thing or cell (game table, gather spot, skygaze cell, the leader's book...) and the
        // leader's first job, made in TryStart.
        public LocalTargetInfo target;
        public Job leaderJob;
        // For workers that choose among several joy givers (e.g. which game).
        public JoyGiverDef joyGiver;
        // For workers built around a skill (e.g. the passion one pawn shows the other).
        public SkillDef skill;
        // For workers that keep a tally (e.g. how many watch jobs the watcher was given).
        public int count;
        public int startTick;
        // For workers that run a set time rather than until a job ends (-1: none).
        public int endTick = -1;

        // The last job given each pawn, as it was when given: vanilla returns finished jobs to a pool, clears them and
        // hands them out again (JobMaker), so the Job itself can't be kept. Its loadID is new each time it's handed out.
        private readonly int[] lastJobId = { -1, -1 };
        private readonly JobDef[] lastJobDef = new JobDef[2];
        private readonly LocalTargetInfo[] lastJobSpot = { LocalTargetInfo.Invalid, LocalTargetInfo.Invalid };
        private readonly int[] given = new int[2];
        private readonly IntVec3[] spots = { IntVec3.Invalid, IntVec3.Invalid };
        private readonly bool[] done = new bool[2];
        private bool ended;

        public DateActivity(DateActivityDef def, Pawn leader, Pawn partner)
        {
            this.def = def;
            this.leader = leader;
            this.partner = partner;
            this.startTick = Find.TickManager.TicksGame;
        }

        public bool Ended => this.ended || Find.TickManager.TicksGame > this.startTick + MaxDurationTicks;

        public Pawn Other(Pawn pawn) => pawn == this.leader ? this.partner : this.leader;

        // How many jobs this activity has given the pawn so far.
        public int Given(Pawn pawn) => this.given[Index(pawn)];

        // The def and B target (seat or spot) of the last job given the pawn; null / Invalid if none yet.
        public JobDef LastJobDef(Pawn pawn) => this.lastJobDef[Index(pawn)];

        public LocalTargetInfo LastJobSpot(Pawn pawn) => this.lastJobSpot[Index(pawn)];

        // For workers that give each pawn a spot of its own (e.g. where to dance); Invalid if none.
        public IntVec3 Spot(Pawn pawn) => this.spots[Index(pawn)];

        public void SetSpot(Pawn pawn, IntVec3 cell) => this.spots[Index(pawn)] = cell;

        // For workers where each pawn finishes their own part (e.g. eating) and then keeps the other company.
        public bool IsDone(Pawn pawn) => this.done[Index(pawn)];

        public void MarkDone(Pawn pawn) => this.done[Index(pawn)] = true;

        // True while the pawn is still doing the last job this activity gave it.
        public bool IsDoing(Pawn pawn) => this.lastJobId[Index(pawn)] >= 0 && pawn.CurJob?.loadID == this.lastJobId[Index(pawn)];

        public Job NextJob(Pawn pawn)
        {
            if (this.Ended)
            {
                return null;
            }
            Job job = this.def.Worker.JobFor(this, pawn);
            if (job == null)
            {
                End();
                return null;
            }
            // Joy jobs normally stop at the pawn's sleep or work time; the date decides when it's over instead.
            job.ignoreJoyTimeAssignment = true;
            // Joy jobs also stop once the pawn's recreation is full, which would end the activity for both (and each new
            // one at once) as soon as either is fully entertained. As in vanilla's gatherings they carry on instead;
            // jobs that sit facing a building (TV, chess...) then take their length from expiryInterval.
            if (job.def.joyKind != null)
            {
                job.doUntilGatheringEnded = true;
                if (job.expiryInterval <= 0 && job.def.joyDuration > 0)
                {
                    job.expiryInterval = job.def.joyDuration;
                }
            }
            this.lastJobId[Index(pawn)] = job.loadID;
            this.lastJobDef[Index(pawn)] = job.def;
            this.lastJobSpot[Index(pawn)] = job.targetB;
            this.given[Index(pawn)]++;
            return job;
        }

        // Ends the activity for both: the other pawn stops its part, so the two pick the next activity together.
        public void End()
        {
            if (this.ended)
            {
                return;
            }
            this.ended = true;
            this.def.Worker.End(this);
            PsychologyDebug.Log($"Date activity {this.def.defName} of {this.leader} and {this.partner} ended after {(Find.TickManager.TicksGame - this.startTick) / (float)GenDate.TicksPerHour:0.00} h (recreation {this.leader.needs?.joy?.CurLevel ?? -1f:P0} / {this.partner.needs?.joy?.CurLevel ?? -1f:P0}; last jobs {this.lastJobDef[0]?.defName ?? "-"} / {this.lastJobDef[1]?.defName ?? "-"})");
            foreach (Pawn pawn in new[] { this.leader, this.partner })
            {
                if (IsDoing(pawn))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        private int Index(Pawn pawn) => pawn == this.leader ? 0 : 1;
    }
}
