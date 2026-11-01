using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    // Watching the other at work on a date (DateActivityWorker_ShareWork), as children watch adults (vanilla's
    // JobDriver_Workwatching): keep within a few cells of the worker (TargetA), facing them, and while in sight learn the
    // skill their job uses at a quarter of vanilla's rate, unless the watcher can't learn it. Both get a little recreation
    // (Social: the job def's joyGainRate for the watcher, WorkerJoyGainRate for the worker). Ends when the job expires.
    public class JobDriver_DateWorkwatch : JobDriver
    {
        // Vanilla's work-watching gives 0.16 XP per tick.
        private const float XpPerTick = 0.04f;
        private const float WorkerJoyGainRate = 0.4f;
        // Vanilla's: follow when further than this, learn within LearnDistance.
        private const float FollowDistance = 3f;
        private const float LearnDistance = 12f;

        private float xpGained;

        private Pawn Worker => (Pawn)TargetThingA;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.tickIntervalAction = delegate (int delta)
            {
                Pawn worker = Worker;
                bool inSight = pawn.Position.InHorDistOf(worker.Position, LearnDistance) && GenSight.LineOfSight(pawn.Position, worker.Position, pawn.Map, true);
                if (inSight)
                {
                    Learn(worker, delta);
                    worker.needs?.joy?.GainJoy(WorkerJoyGainRate * 0.36f / GenDate.TicksPerHour * delta, JoyKindDefOf.Social);
                    JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
                }
                if ((!inSight || !pawn.Position.InHorDistOf(worker.Position, FollowDistance)) && (!pawn.pather.Moving || pawn.pather.Destination != worker))
                {
                    if (!pawn.CanReach(worker, PathEndMode.Touch, Danger.None))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    pawn.pather.StartPath(worker, PathEndMode.Touch);
                }
            };
            toil.socialMode = RandomSocialMode.SuperActive;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            AddFinishAction(_ => PsychologyDebug.Log($"Date activity ShareWork: {pawn} watched {Worker} work, {xpGained:0.#} XP"));
            yield return toil;
        }

        // As vanilla: the recipe's skill for a bill, otherwise split between the work type's skills.
        private void Learn(Pawn worker, int delta)
        {
            if (pawn.skills == null)
            {
                return;
            }
            float xp = XpPerTick * delta;
            SkillDef recipeSkill = worker.CurJob?.RecipeDef?.workSkill;
            if (recipeSkill != null)
            {
                LearnSkill(recipeSkill, xp);
                return;
            }
            List<SkillDef> skills = worker.CurJob?.workGiverDef?.workType?.relevantSkills;
            if (skills.NullOrEmpty())
            {
                return;
            }
            foreach (SkillDef skill in skills)
            {
                LearnSkill(skill, xp / skills.Count);
            }
        }

        private void LearnSkill(SkillDef skill, float xp)
        {
            SkillRecord record = pawn.skills.GetSkill(skill);
            if (record == null || record.TotallyDisabled)
            {
                return;
            }
            pawn.skills.Learn(skill, xp);
            xpGained += xp;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref xpGained, "xpGained", 0f);
        }
    }
}
