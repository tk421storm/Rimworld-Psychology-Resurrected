using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    // Panic attack: cower where they stand (vanilla's flee-and-cower with no flee distance), for the whole attack. The
    // think tree gives a new one when vanilla's 1200-tick cower ends.
    public class JobGiver_PanicAttack : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return JobMaker.MakeJob(JobDefOf.FleeAndCower, pawn.Position);
        }
    }
}
