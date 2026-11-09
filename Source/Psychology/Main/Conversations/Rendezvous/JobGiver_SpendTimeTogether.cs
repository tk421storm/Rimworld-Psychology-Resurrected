using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Reflection;

namespace Psychology
{
    // Job giver for the HangOut duty (dates and hang-outs). The pair does activities together one after another (DateActivityDef: a game, a walk, reading side by side...).
    // When there's nothing they can do together, they stay within talking distance and wander or idle near each other.
    // Partners on a date may go to bed together when one invites the other (RomanceAttemptRouter, DateLovin).
    public class JobGiver_SpendTimeTogether : ThinkNode_JobGiver
    {

        protected override Job TryGiveJob(Pawn pawn)
        {
            LordToil_HangOut toil = pawn.GetLord()?.CurLordToil as LordToil_HangOut;
            if (toil == null)
                return null;
            Pawn friend = (pawn == toil.friends[0] ? toil.friends[1] : toil.friends[0]);
            if (friend == null || pawn.needs?.joy == null)
                return null;
            /* Vanilla re-runs the think tree now and then while a job is running (CheckForJobOverride); returning the
               current job tells it to carry on, so an activity in progress isn't cut short. */
            if (toil.activity != null && !toil.activity.Ended && toil.activity.IsDoing(pawn))
                return pawn.CurJob;
            /* Their activity together: the next step of the current one, or (if it's over) start a new one; a meal first
               if either is hungry (DateActivityWorker_ShareMeal). */
            DateActivity activity = toil.CurrentActivity(pawn);
            Job job = activity?.NextJob(pawn);
            if (job == null && activity != null)
            {
                job = toil.CurrentActivity(pawn)?.NextJob(pawn);
            }
            if (job != null)
            {
                return job;
            }
            if (((pawn.Position - friend.Position).LengthHorizontalSquared >= 54 || !GenSight.LineOfSight(pawn.Position, friend.Position, pawn.Map, true)))
            { /* Make sure they are close to each other if they're not actively doing a joy activity. */
              /* If the other pawn is already walking over, just hang around until they get there. */
                /* (No current job right after the date starts, as both pawns' jobs are stopped.) */
                if (friend.CurJob?.def != JobDefOf.Goto)
                    return new Job(JobDefOf.Goto, friend);
                else
                {
                    pawn.rotationTracker.FaceCell(friend.Position);
                    return null;
                }
            }
            else
            {
                /* If they are already standing close enough, but can't do the joy activity together, then wander around. */
                IntVec3 result;
                /* Make sure they only wander within conversational distance. */
                Predicate<IntVec3> validator = (IntVec3 x) => x.Standable(pawn.Map) && x.InAllowedArea(pawn) && !x.IsForbidden(pawn) && pawn.CanReserveAndReach(x, PathEndMode.OnCell, Danger.None, 1, -1, null, false)
                                                                && (x - friend.Position).LengthHorizontalSquared < 50 && GenSight.LineOfSight(x, friend.Position, pawn.Map, true) && x != friend.Position;
                if (CellFinder.TryFindRandomReachableNearbyCell(pawn.Position, pawn.Map, 12f, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false), (IntVec3 x) => validator(x), null, out result))
                {
                    if ((pawn.Position - friend.Position).LengthHorizontalSquared >= 5 || (LovePartnerRelationUtility.LovePartnerRelationExists(pawn, friend) && pawn.Position != friend.Position))
                    {
                        /* Sending them to goto a friend ends with them standing right next to/on top of them. So make them respect personal space a little more. */
                        pawn.mindState.nextMoveOrderIsWait = !pawn.mindState.nextMoveOrderIsWait;
                        if (!result.IsValid || pawn.mindState.nextMoveOrderIsWait)
                        {
                            /* Alternate between relaxing socially and wandering. */
                            pawn.rotationTracker.FaceCell(friend.Position);
                            return null;
                        }
                    }
                    Job wander = new Job(JobDefOf.GotoWander, result);
                    pawn.Map.pawnDestinationReservationManager.Reserve(pawn, wander, result);
                    return wander;
                }
                /* If we can't find a valid spot, just relax socially. */
                pawn.rotationTracker.FaceCell(friend.Position);
                return null;
            }
        }
    }
}
