using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace Psychology
{
    // A game or show both can join: any joy giver on a building that takes 2+ participants (vanilla: horseshoes,
    // hoopstone, Game of Ur, chess, billiards, poker, TV; modded ones are picked up the same way). The leader picks the
    // building as vanilla would; each then gets that game's job for it, with a seat or spot of their own.
    public class DateActivityWorker_PlayTogether : DateActivityWorker
    {
        private static readonly Func<JoyGiver_InteractBuilding, Pawn, Thing, Job> tryGivePlayJob =
            AccessTools.MethodDelegate<Func<JoyGiver_InteractBuilding, Pawn, Thing, Job>>(AccessTools.Method(typeof(JoyGiver_InteractBuilding), "TryGivePlayJob"));

        private static List<JoyGiverDef> sharedGames;

        private static List<JoyGiverDef> SharedGames
        {
            get
            {
                if (sharedGames == null)
                {
                    sharedGames = DefDatabase<JoyGiverDef>.AllDefs.Where(g => g.Worker is JoyGiver_InteractBuilding && g.jobDef != null && g.jobDef.joyMaxParticipants >= 2).ToList();
                }
                return sharedGames;
            }
        }

        public override bool CanDo(Pawn leader, Pawn partner)
        {
            return SharedGames.Any(g => g.Worker.CanBeGivenTo(leader) && g.Worker.CanBeGivenTo(partner));
        }

        public override bool TryStart(DateActivity activity)
        {
            Pawn leader = activity.leader;
            Pawn partner = activity.partner;
            List<JoyGiverDef> games = SharedGames.Where(g => g.Worker.CanBeGivenTo(leader) && g.Worker.CanBeGivenTo(partner)).ToList();
            while (games.TryRandomElementByWeight(g => g.Worker.GetChance(leader) * (ToleranceFactor(leader, g.jobDef.joyKind) + ToleranceFactor(partner, g.jobDef.joyKind)), out JoyGiverDef game))
            {
                games.Remove(game);
                Job job = game.Worker.TryGiveJob(leader);
                Thing building = job?.targetA.Thing;
                if (building != null && partner.CanReserve(building, game.jobDef.joyMaxParticipants) && tryGivePlayJob((JoyGiver_InteractBuilding)game.Worker, partner, building) != null
                    && (!(game.Worker is JoyGiver_WatchBuilding) || TryFindSpotNear(job.targetB.Cell, building.Map, WatchCells(building), c => CanWatchFrom(partner, c, game.desireSit), out _)))
                {
                    activity.joyGiver = game;
                    activity.target = building;
                    activity.leaderJob = job;
                    return true;
                }
            }
            return false;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            if (activity.Given(pawn) > 0)
            {
                return null;
            }
            // Both jobs are made when each pawn asks, not in TryStart: the job reserves its seat or watch cell only
            // when started, so a job made earlier and started after the other pawn's can pick the same cell.
            Thing building = activity.target.Thing;
            Job job = building != null && building.Spawned ? tryGivePlayJob((JoyGiver_InteractBuilding)activity.joyGiver.Worker, pawn, building) : null;
            // Games watched from a spot (horseshoes, hoopstone, TV...) can put the two on opposite sides: keep them close.
            bool desireSit = activity.joyGiver.desireSit;
            if (job != null && activity.joyGiver.Worker is JoyGiver_WatchBuilding
                && TryFindSpotNearOther(activity, pawn, job, WatchCells(building), c => CanWatchFrom(pawn, c, desireSit), out IntVec3 cell))
            {
                job.targetB = cell;
                job.targetC = desireSit ? cell.GetEdifice(building.Map) : null;
            }
            return job;
        }

        private static IEnumerable<IntVec3> WatchCells(Thing building)
        {
            return WatchBuildingUtility.CalculateWatchCells(building.def, building.Position, building.Rotation, building.Map);
        }

        // Whether the pawn could watch from the cell, as vanilla checks it (WatchBuildingUtility.TryFindBestWatchCell; also
        // reachable, and not a bed). A game that wants its players seated (TV) needs a free seat there.
        private static bool CanWatchFrom(Pawn pawn, IntVec3 cell, bool desireSit)
        {
            Map map = pawn.Map;
            Building edifice = cell.GetEdifice(map);
            if (edifice is Building_Bed || cell.IsForbidden(pawn) || !pawn.CanReserveSittableOrSpot(cell) || !map.pawnDestinationReservationManager.CanReserve(cell, pawn)
                || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.None))
            {
                return false;
            }
            return !desireSit || (edifice != null && edifice.def.building.isSittable && pawn.CanReserve(edifice));
        }
    }

    // Relax socially at the same gather spot (vanilla picks a chair and maybe a drink for each), seated within talking
    // range of each other: a big table or a campfire ringed with chairs can put them far apart.
    public class DateActivityWorker_SocialRelax : DateActivityWorker
    {
        // Vanilla's (JoyGiver_SocialRelax): chairs and ground spots this close to a gather spot that isn't a table.
        private const float GatherRadius = 3.9f;

        public override bool TryStart(DateActivity activity)
        {
            Job job = this.def.joyGiver.Worker.TryGiveJob(activity.leader);
            Thing spot = job?.targetA.Thing;
            Job partnerJob = spot != null ? PartnerJob(activity.partner, spot) : null;
            if (partnerJob == null || !TryFindSpotNear(job.targetB.Cell, spot.Map, SeatCells(spot), c => CanSitAt(activity.partner, spot, c, partnerJob.targetB.HasThing), out _))
            {
                return false;
            }
            activity.target = spot;
            activity.leaderJob = job;
            return true;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            if (activity.Given(pawn) > 0)
            {
                return null;
            }
            // Made when asked, not in TryStart, so neither picks a chair the other has since taken (see PlayTogether).
            Thing spot = activity.target.Thing;
            Job job = PartnerJob(pawn, spot);
            bool chair = job != null && job.targetB.HasThing;
            if (job != null && TryFindSpotNearOther(activity, pawn, job, SeatCells(spot), c => CanSitAt(pawn, spot, c, chair), out IntVec3 cell))
            {
                job.targetB = chair ? (LocalTargetInfo)cell.GetEdifice(spot.Map) : cell;
            }
            return job;
        }

        private Job PartnerJob(Pawn pawn, Thing spot)
        {
            if (spot == null || !spot.Spawned)
            {
                return null;
            }
            Job job = this.def.joyGiver.Worker.TryGiveJobInGatheringArea(pawn, spot.Position, 4f);
            return job?.targetA.Thing == spot ? job : null;
        }

        // Where vanilla seats people at the spot: beside a table, or around anything else.
        private static IEnumerable<IntVec3> SeatCells(Thing spot)
        {
            return spot.def.surfaceType == SurfaceType.Eat ? GenAdj.CellsAdjacentCardinal(spot) : GenRadial.RadialCellsAround(spot.Position, GatherRadius, true);
        }

        // Whether the pawn could sit at the cell, as vanilla checks it (JoyGiver_SocialRelax): on a free seat there if
        // `chair` (vanilla gave them one), otherwise on the bare ground; in sight of the spot.
        private static bool CanSitAt(Pawn pawn, Thing spot, IntVec3 cell, bool chair)
        {
            Map map = spot.Map;
            if (!cell.InBounds(map) || cell.Fogged(map) || cell.VacuumConcernTo(pawn) || !GenSight.LineOfSight(spot.Position, cell, map, true))
            {
                return false;
            }
            Building edifice = cell.GetEdifice(map);
            if (!chair)
            {
                return edifice == null && pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.None);
            }
            CompAssignableToPawn_Throne throne = edifice?.TryGetComp<CompAssignableToPawn_Throne>();
            return edifice != null && edifice.def.building.isSittable && !edifice.IsForbidden(pawn) && Toils_Ingest.TryFindFreeSittingSpotOnThing(edifice, pawn, out _)
                && (throne == null || throne.AssignedPawns.Contains(pawn)) && pawn.CanReach(edifice, PathEndMode.OnCell, Danger.None);
        }
    }

    // Lie down and watch the sky next to each other: the leader picks the spot as vanilla does, the partner takes a free
    // unroofed cell beside it.
    public class DateActivityWorker_Skygaze : DateActivityWorker
    {
        public override bool TryStart(DateActivity activity)
        {
            Job job = this.def.joyGiver.Worker.TryGiveJob(activity.leader);
            if (job == null)
            {
                return false;
            }
            IntVec3 spot = job.targetA.Cell;
            Pawn partner = activity.partner;
            foreach (IntVec3 offset in GenAdj.AdjacentCells)
            {
                IntVec3 cell = spot + offset;
                if (cell.InBounds(partner.Map) && cell.Standable(partner.Map) && !cell.Roofed(partner.Map) && !cell.IsForbidden(partner) && partner.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.None))
                {
                    activity.target = cell;
                    activity.leaderJob = job;
                    return true;
                }
            }
            return false;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            if (activity.Given(pawn) > 0)
            {
                return null;
            }
            return pawn == activity.leader ? activity.leaderJob : JobMaker.MakeJob(this.def.joyGiver.jobDef, activity.target);
        }
    }

    // Read side by side. Vanilla carries the book to the free chair closest to where it was picked up, so the partner
    // gets a book from within a few cells of the leader's; both then sit down near the same shelf.
    public class DateActivityWorker_Read : DateActivityWorker
    {
        private const float MaxBookDistance = 8f;

        private static readonly Func<Thing, Pawn, bool> isValidBook =
            AccessTools.MethodDelegate<Func<Thing, Pawn, bool>>(AccessTools.Method(typeof(BookUtility), "IsValidBook"));

        public override bool TryStart(DateActivity activity)
        {
            Job job = this.def.joyGiver.Worker.TryGiveJob(activity.leader);
            Thing leaderBook = job?.targetA.Thing;
            if (leaderBook == null)
            {
                return false;
            }
            Pawn partner = activity.partner;
            IntVec3 near = leaderBook.PositionHeld;
            IEnumerable<Thing> books = partner.Map.listerThings.ThingsInGroup(ThingRequestGroup.Book)
                .Concat(partner.Map.listerThings.GetThingsOfType<Building_Bookcase>().SelectMany(x => x.HeldBooks));
            Thing partnerBook = books
                .Where(b => b != leaderBook && b.PositionHeld.InHorDistOf(near, MaxBookDistance) && isValidBook(b, partner) && BookUtility.CanReadBook((Book)b, partner, out _))
                .OrderBy(b => b.PositionHeld.DistanceToSquared(near))
                .FirstOrDefault();
            if (partnerBook == null)
            {
                return false;
            }
            activity.target = partnerBook;
            activity.leaderJob = job;
            return true;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            if (activity.Given(pawn) > 0)
            {
                return null;
            }
            return pawn == activity.leader ? activity.leaderJob : JobMaker.MakeJob(this.def.joyGiver.jobDef, activity.target);
        }
    }

    // A walk: the leader goes for a vanilla walk, the partner follows close behind (FollowClose ends whenever it catches
    // up, so it's given again for as long as the leader is walking).
    public class DateActivityWorker_Walk : DateActivityWorker
    {
        private const float FollowRadius = 3f;

        public override bool TryStart(DateActivity activity)
        {
            activity.leaderJob = this.def.joyGiver.Worker.TryGiveJob(activity.leader);
            return activity.leaderJob != null && activity.partner.CanReach(activity.leader, PathEndMode.Touch, Danger.None);
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            Pawn leader = activity.leader;
            if (pawn == leader)
            {
                return activity.Given(pawn) > 0 ? null : activity.leaderJob;
            }
            if (activity.Given(leader) == 0)
            {
                return WaitFor(leader, 60);
            }
            if (!activity.IsDoing(leader))
            {
                return null;
            }
            if (!JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, leader, FollowRadius))
            {
                return WaitFor(leader, 60);
            }
            Job job = JobMaker.MakeJob(JobDefOf.FollowClose, leader);
            job.followRadius = FollowRadius;
            job.expiryInterval = 300;
            return job;
        }
    }

    // Music and dancing at the nearest instrument (harp, piano... or a modded one). If nobody's playing it, the pair's
    // better musician (Artistic) plays while the other dances nearby, until the piece is over; if someone else is
    // already playing, both dance to it for an hour or so, and keep dancing if the music stops. Dancers stay within
    // earshot, so they get vanilla's "listening to..." mood too.
    public class DateActivityWorker_MusicAndDance : DateActivityWorker
    {
        private const float MaxDanceDistance = 5f;
        private static readonly IntRange JoinDanceTicks = new IntRange(GenDate.TicksPerHour, GenDate.TicksPerHour * 3 / 2);
        // Dance in short bouts like vanilla's dance parties, each with a fresh move and facing.
        private static readonly IntRange DanceBoutTicks = new IntRange(300, 600);

        public override bool CanDo(Pawn leader, Pawn partner)
        {
            return leader.Map.listerBuildings.AllBuildingsColonistOfClass<Building_MusicalInstrument>().Any();
        }

        public override float Weight(Pawn leader, Pawn partner)
        {
            return this.def.commonality * (ToleranceFactor(leader, JoyKindDefOf.Social) + ToleranceFactor(partner, JoyKindDefOf.Social)) / 2f;
        }

        public override bool TryStart(DateActivity activity)
        {
            Pawn leader = activity.leader;
            Pawn partner = activity.partner;
            Pawn musician = new[] { leader, partner }.Where(p => p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 0).FirstOrDefault();
            IEnumerable<Building_MusicalInstrument> instruments = leader.Map.listerBuildings.AllBuildingsColonistOfClass<Building_MusicalInstrument>()
                .Where(i => Usable(i, leader) && Usable(i, partner))
                .OrderBy(i => i.Position.DistanceToSquared(leader.Position));
            foreach (Building_MusicalInstrument instrument in instruments)
            {
                bool join = instrument.IsBeingPlayed;
                if (!join && (musician == null || PlayJob(musician, instrument) == null))
                {
                    continue;
                }
                List<IntVec3> spots = DanceSpots(instrument, leader, partner);
                if (spots.NullOrEmpty())
                {
                    continue;
                }
                activity.target = instrument;
                IntVec3 spot = spots.RandomElement();
                if (join)
                {
                    // Dance next to each other.
                    if (!spots.Where(c => c != spot && c.InHorDistOf(spot, 1.5f)).TryRandomElement(out IntVec3 nextTo))
                    {
                        continue;
                    }
                    activity.SetSpot(leader, spot);
                    activity.SetSpot(partner, nextTo);
                    activity.endTick = Find.TickManager.TicksGame + JoinDanceTicks.RandomInRange;
                    PsychologyDebug.Log($"Date activity: {leader} and {partner} dance to {instrument.LabelShort}, already being played");
                }
                else
                {
                    // The musician leads; the dancer gets a spot.
                    if (musician == partner)
                    {
                        activity.partner = leader;
                        activity.leader = musician;
                    }
                    activity.SetSpot(activity.partner, spot);
                    PsychologyDebug.Log($"Date activity: {activity.leader} plays {instrument.LabelShort}, {activity.partner} dances");
                }
                return true;
            }
            return false;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            if (!(activity.target.Thing is Building_MusicalInstrument instrument) || !instrument.Spawned)
            {
                return null;
            }
            IntVec3 spot = activity.Spot(pawn);
            if (!spot.IsValid)
            {
                // The musician: one piece, then the activity is over for both. The job is made when asked, so the
                // instrument is checked again (see PlayTogether).
                return activity.Given(pawn) > 0 ? null : PlayJob(pawn, instrument);
            }
            if (activity.endTick >= 0 && Find.TickManager.TicksGame >= activity.endTick)
            {
                return null;
            }
            if (pawn.Position != spot)
            {
                return JobMaker.MakeJob(JobDefOf.Goto, spot);
            }
            Job dance = JobMaker.MakeJob(JobDefOfPsychology.DateDance, activity.Other(pawn));
            dance.expiryInterval = DanceBoutTicks.RandomInRange;
            return dance;
        }

        private static bool Usable(Building_MusicalInstrument instrument, Pawn pawn)
        {
            return instrument.Spawned && !instrument.IsForbidden(pawn) && instrument.IsSociallyProper(pawn) && pawn.CanReach(instrument, PathEndMode.Touch, Danger.None);
        }

        // As vanilla's joy giver (JoyGiver_InteractBuildingInteractionCell) does it, which only exists with Royalty.
        private static Job PlayJob(Pawn pawn, Building_MusicalInstrument instrument)
        {
            IntVec3 cell = instrument.InteractionCell;
            if (instrument.IsBeingPlayed || !cell.Standable(instrument.Map) || cell.IsForbidden(pawn) || pawn.Map.pawnDestinationReservationManager.IsReserved(cell) || !pawn.CanReserveSittableOrSpot(cell))
            {
                return null;
            }
            return JobMaker.MakeJob(JobDefOf.Play_MusicalInstrument, instrument, cell);
        }

        // Free cells near the instrument and within earshot (same room, in range), not right on top of it.
        private static List<IntVec3> DanceSpots(Building_MusicalInstrument instrument, Pawn leader, Pawn partner)
        {
            Map map = instrument.Map;
            float radius = Mathf.Min(instrument.def.building.instrumentRange, MaxDanceDistance);
            return GenRadial.RadialCellsAround(instrument.Position, radius, false)
                .Where(c => c.InBounds(map) && !c.InHorDistOf(instrument.Position, 1.9f) && c != instrument.InteractionCell && c.Standable(map)
                    && !map.pawnDestinationReservationManager.IsReserved(c) && !c.IsForbidden(leader) && !c.IsForbidden(partner)
                    && Building_MusicalInstrument.IsAffectedByInstrument(instrument.def, instrument.Position, c, map)
                    && leader.CanReach(c, PathEndMode.OnCell, Danger.None) && partner.CanReach(c, PathEndMode.OnCell, Danger.None))
                .ToList();
        }
    }

    // A meal together, first thing once either is hungry (Urgent). Each gets food as vanilla would (JobGiver_GetFood:
    // stored meals, a dispenser, food they carry...), unless nearly full as at a party; the Toils_Ingest patch seats the
    // second at a table within talking range of the first. Whoever has eaten, or found nothing to eat, keeps the other
    // company until they're done too. It only fails to start if neither can get food.
    public class DateActivityWorker_ShareMeal : DateActivityWorker
    {
        private const float HungryLevel = 0.3f;
        // Vanilla's for eating at a gathering (JobGiver_EatInGatheringArea).
        private const float FullLevel = 0.9f;
        // Food jobs per pawn: one or two to get food (a hopper to fill, food from someone's inventory) and the meal.
        private const int MaxFoodJobs = 3;
        private const float FollowRadius = 3f;

        private static readonly JobGiver_GetFood getFood = new JobGiver_GetFood();

        public override float Weight(Pawn leader, Pawn partner)
        {
            return 0f;
        }

        public override bool Urgent(Pawn leader, Pawn partner)
        {
            return IsHungry(leader) || IsHungry(partner);
        }

        public override bool TryStart(DateActivity activity)
        {
            // Only a check: the food jobs are made when each starts eating (JobFor). One made now would be stale by then,
            // as the other may reserve the same meal first (vanilla makes a food job and starts it right away).
            return (WantsFood(activity.leader) && FoodJob(activity.leader) != null) || (WantsFood(activity.partner) && FoodJob(activity.partner) != null);
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            Pawn other = activity.Other(pawn);
            if (!activity.IsDone(pawn))
            {
                Job job = null;
                if (WantsFood(pawn) && activity.Given(pawn) < MaxFoodJobs && activity.LastJobDef(pawn) != JobDefOf.Ingest)
                {
                    job = FoodJob(pawn);
                }
                if (job != null)
                {
                    return job;
                }
                activity.MarkDone(pawn);
                if (activity.Given(pawn) == 0)
                {
                    PsychologyDebug.Log($"Date activity {this.def.defName}: {pawn} {(WantsFood(pawn) ? "found nothing to eat" : "isn't hungry")}; keeps {other} company");
                }
            }
            return activity.IsDone(other) ? null : CompanyJob(pawn, other);
        }

        // Sit with the other while they eat: follow them while they fetch their food, then take a seat at a table in
        // talking range of where they eat (or stand nearby if there's none).
        private static Job CompanyJob(Pawn pawn, Pawn other)
        {
            IntVec3 spot = EatingSpot(other);
            if (!spot.IsValid)
            {
                if (other.pather.Moving && JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, other, FollowRadius))
                {
                    Job follow = JobMaker.MakeJob(JobDefOf.FollowClose, other);
                    follow.followRadius = FollowRadius;
                    follow.expiryInterval = 300;
                    return follow;
                }
                return WaitFor(other, 60);
            }
            if (pawn.Position != spot && InTalkingRange(pawn.Position, spot, pawn.Map) && (pawn.Position.GetEdifice(pawn.Map)?.def.building?.isSittable ?? false))
            {
                return WaitFor(other);
            }
            if (TryFindSeatNear(pawn, spot, out IntVec3 seat))
            {
                return pawn.Position == seat ? WaitFor(other) : JobMaker.MakeJob(JobDefOf.Goto, seat);
            }
            if (!InTalkingRange(pawn.Position, spot, pawn.Map) && JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, other, FollowRadius))
            {
                Job follow = JobMaker.MakeJob(JobDefOf.FollowClose, other);
                follow.followRadius = FollowRadius;
                follow.expiryInterval = 300;
                return follow;
            }
            return WaitFor(other);
        }

        // For the Toils_Ingest patch: if the pawn is eating on a shared meal and vanilla's chair or spot for them is out of
        // talking range of where the other eats, a seat at a table closer by. False to keep vanilla's.
        public static bool TryFindChewSpotNearOther(Pawn pawn, IntVec3 vanillaSpot, out IntVec3 seat)
        {
            seat = IntVec3.Invalid;
            DateActivity activity = (pawn.GetLord()?.CurLordToil as LordToil_HangOut)?.activity;
            if (activity == null || activity.Ended || !(activity.def.Worker is DateActivityWorker_ShareMeal) || (pawn != activity.leader && pawn != activity.partner))
            {
                return false;
            }
            Pawn other = activity.Other(pawn);
            IntVec3 spot = EatingSpot(other);
            if (!spot.IsValid && activity.IsDone(other) && !other.pather.Moving)
            {
                // The other isn't eating (they've eaten, or found nothing): sit near them.
                spot = other.Position;
            }
            if (!spot.IsValid || (vanillaSpot.IsValid && InTalkingRange(vanillaSpot, spot, pawn.Map)))
            {
                return false;
            }
            bool found = TryFindSeatNear(pawn, spot, out seat);
            PsychologyDebug.Log($"Date activity ShareMeal: {pawn}'s seat {vanillaSpot} is out of talking range of {other}'s {spot}; {(found ? "moved to " + seat : "no closer seat")}");
            return found;
        }

        // Where the pawn will eat, once they've chosen (the spot their meal job reserved on the way there), or Invalid.
        private static IntVec3 EatingSpot(Pawn pawn)
        {
            if (pawn.CurJob?.def != JobDefOf.Ingest || pawn.Map == null)
            {
                return IntVec3.Invalid;
            }
            PawnDestinationReservationManager.PawnDestinationReservation reservation = pawn.Map.pawnDestinationReservationManager.MostRecentReservationFor(pawn);
            return reservation != null && !reservation.obsolete && reservation.job == pawn.CurJob ? reservation.target : IntVec3.Invalid;
        }

        // A free seat beside a table (so no "ate without a table"), within talking range of the spot, as vanilla checks a
        // chair for eating (Toils_Ingest.TryFindChairOrSpot).
        private static bool TryFindSeatNear(Pawn pawn, IntVec3 spot, out IntVec3 seat)
        {
            Map map = pawn.Map;
            return TryFindSpotNear(spot, map, GenRadial.RadialCellsAround(spot, TalkingRange, false), c =>
            {
                if (!c.InBounds(map) || c.Fogged(map) || !GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).Any(a => a.InBounds(map) && a.HasEatSurface(map)))
                {
                    return false;
                }
                Building chair = c.GetEdifice(map);
                return chair != null && chair.def.building.isSittable && !chair.IsForbidden(pawn) && (chair.Faction == pawn.Faction || pawn.Faction == null || !pawn.Faction.IsPlayer)
                    && chair.TryGetComp<CompAssignableToPawn_Throne>() == null && pawn.CanReserveSittableOrSpot(c) && pawn.CanReach(c, PathEndMode.OnCell, Danger.None)
                    && c.GetDangerFor(pawn, map) == Danger.None;
            }, out seat);
        }

        private static Job FoodJob(Pawn pawn)
        {
            return getFood.TryIssueJobPackage(pawn, default(JobIssueParams)).Job;
        }

        private static bool IsHungry(Pawn pawn)
        {
            return pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < HungryLevel;
        }

        private static bool WantsFood(Pawn pawn)
        {
            return pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage <= FullLevel;
        }
    }

    // Sharing a passion: one pawn with a burning passion in a skill does real work that uses it, while the other watches
    // from close by (JobDriver_DateWorkwatch), picking up a little of the skill if they can learn it. Only work whose
    // target is in the home area, of work types the worker is assigned in the Work tab (the watcher's don't matter);
    // never Doctor, Warden, Hunting, Firefighter, Hauling or Cleaning work. If both could, the more skilled one works.
    // Jobs of that skill follow one another for an hour or two; the one under way when time is up is finished first.
    public class DateActivityWorker_ShareWork : DateActivityWorker
    {
        private static readonly IntRange DurationTicks = new IntRange(GenDate.TicksPerHour, GenDate.TicksPerHour * 2);
        // A job that fails as soon as it starts would otherwise be handed out again and again.
        private const int MaxWorkJobs = 15;
        // The watch job is renewed this often, so the watcher notices when the work is over.
        private const int WatchJobTicks = 600;
        private const float SharedPassionFactor = 1.5f;

        public override bool CanDo(Pawn leader, Pawn partner)
        {
            return PassionSkills(leader).Any() || PassionSkills(partner).Any();
        }

        // Social recreation, as for dancing; likelier if the one watching has a passion of their own for the skill.
        public override float Weight(Pawn leader, Pawn partner)
        {
            bool shared = PassionSkills(leader).Any(s => HasPassion(partner, s)) || PassionSkills(partner).Any(s => HasPassion(leader, s));
            return this.def.commonality * (shared ? SharedPassionFactor : 1f) * (ToleranceFactor(leader, JoyKindDefOf.Social) + ToleranceFactor(partner, JoyKindDefOf.Social)) / 2f;
        }

        public override bool TryStart(DateActivity activity)
        {
            Pawn leader = activity.leader;
            Pawn partner = activity.partner;
            var options = new[] { leader, partner }.SelectMany(p => PassionSkills(p).Select(s => (worker: p, skill: s)))
                .OrderByDescending(o => o.worker.skills.GetSkill(o.skill).Level).ThenBy(_ => Rand.Value).ToList();
            foreach ((Pawn worker, SkillDef skill) in options)
            {
                Pawn watcher = worker == leader ? partner : leader;
                // Only a check: the job is made when the worker starts (JobFor), as one made now could be stale by then.
                if (!watcher.CanReach(worker, PathEndMode.Touch, Danger.None) || WorkJob(worker, skill) == null)
                {
                    continue;
                }
                // The worker leads.
                activity.leader = worker;
                activity.partner = watcher;
                activity.skill = skill;
                activity.endTick = Find.TickManager.TicksGame + DurationTicks.RandomInRange;
                PsychologyDebug.Log($"Date activity: {worker} shows {watcher} their passion for {skill.label}");
                return true;
            }
            return false;
        }

        public override Job JobFor(DateActivity activity, Pawn pawn)
        {
            Pawn worker = activity.leader;
            bool timeUp = Find.TickManager.TicksGame >= activity.endTick;
            if (pawn == worker)
            {
                return timeUp || activity.Given(pawn) >= MaxWorkJobs ? null : WorkJob(pawn, activity.skill);
            }
            if (!activity.IsDoing(worker))
            {
                // Between two jobs, or the worker hasn't started yet.
                return timeUp ? null : WaitFor(worker, 60);
            }
            Job watch = JobMaker.MakeJob(JobDefOfPsychology.DateWorkwatch, worker);
            watch.expiryInterval = WatchJobTicks;
            activity.count++;
            return watch;
        }

        // A watcher who got to see the work comes away with an opinion of how good the worker is at it
        // (Thought_SawSkillOnDate).
        public override void End(DateActivity activity)
        {
            Pawn worker = activity.leader;
            Pawn watcher = activity.partner;
            if (activity.count > 0 && activity.skill != null && !worker.Dead && !watcher.Dead && watcher.needs?.mood?.thoughts?.memories != null)
            {
                Thought_SawSkillOnDate.Gain(watcher, worker, activity.skill);
            }
        }

        // The pawn's burning passions they can show: skills they can do, with a work type that uses it, allowed here and
        // assigned to them.
        private static IEnumerable<SkillDef> PassionSkills(Pawn pawn)
        {
            if (pawn.skills == null || pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return Enumerable.Empty<SkillDef>();
            }
            return pawn.skills.skills.Where(r => r.passion == Passion.Major && !r.TotallyDisabled
                && DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(w => Shows(w, r.def) && !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.WorkIsActive(w)))
                .Select(r => r.def);
        }

        private static bool HasPassion(Pawn pawn, SkillDef skill)
        {
            SkillRecord record = pawn.skills?.GetSkill(skill);
            return record != null && !record.TotallyDisabled && record.passion != Passion.None;
        }

        private static bool Shows(WorkTypeDef workType, SkillDef skill)
        {
            return workType != null && workType.relevantSkills != null && workType.relevantSkills.Contains(skill) && workType != WorkTypeDefOf.Doctor && workType != WorkTypeDefOf.Warden
                && workType != WorkTypeDefOf.Hunting && workType != WorkTypeDefOf.Firefighter && workType != WorkTypeDefOf.Hauling && workType != WorkTypeDefOf.Cleaning;
        }

        // The nearest job of the skill the worker would take, as vanilla looks for work (JobGiver_Work: their work givers in
        // priority order, the closest target each can use), but only targets in the home area and safe to reach. A bill
        // must be one whose recipe uses the skill.
        private static Job WorkJob(Pawn worker, SkillDef skill)
        {
            Map map = worker.Map;
            Area home = map?.areaManager.Home;
            if (home == null)
            {
                return null;
            }
            foreach (WorkGiver giver in worker.workSettings.WorkGiversInOrderNormal)
            {
                if (!(giver is WorkGiver_Scanner scanner) || !Shows(giver.def.workType, skill) || !CanUse(worker, giver))
                {
                    continue;
                }
                try
                {
                    Job job = null;
                    if (scanner.def.scanThings)
                    {
                        IEnumerable<Thing> things = scanner.PotentialWorkThingsGlobal(worker) ?? (scanner.PotentialWorkThingRequest.IsUndefined ? null : map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest));
                        Thing thing = things == null ? null : GenClosest.ClosestThing_Global_Reachable(worker.Position, map, things, scanner.PathEndMode, TraverseParms.For(worker, Danger.None), 9999f,
                            t => t.Spawned && home[t.Position] && !t.IsForbidden(worker) && scanner.HasJobOnThing(worker, t));
                        job = thing != null ? scanner.JobOnThing(worker, thing) : null;
                    }
                    if (job == null && scanner.def.scanCells)
                    {
                        foreach (IntVec3 cell in scanner.PotentialWorkCellsGlobal(worker).Where(c => home[c] && !c.IsForbidden(worker)).OrderBy(c => c.DistanceToSquared(worker.Position)))
                        {
                            if (scanner.HasJobOnCell(worker, cell) && worker.CanReach(cell, scanner.PathEndMode, Danger.None))
                            {
                                job = scanner.JobOnCell(worker, cell);
                                break;
                            }
                        }
                    }
                    if (job != null && (job.RecipeDef?.workSkill == null || job.RecipeDef.workSkill == skill))
                    {
                        job.workGiverDef = scanner.def;
                        return job;
                    }
                }
                catch (Exception e)
                {
                    Log.ErrorOnce($"[Psychology] Date activity ShareWork: {worker} threw in work giver {giver.def.defName}: {e}", Gen.HashCombineInt(giver.def.shortHash, 0x5A1E));
                }
            }
            return null;
        }

        // As vanilla checks it (JobGiver_Work.PawnCanUseWorkGiver).
        private static bool CanUse(Pawn pawn, WorkGiver giver)
        {
            return !pawn.WorkTagIsDisabled(giver.def.workTags) && (giver.def.workType == null || !pawn.WorkTypeIsDisabled(giver.def.workType))
                && !giver.ShouldSkip(pawn) && giver.MissingRequiredCapacity(pawn) == null;
        }
    }
}
