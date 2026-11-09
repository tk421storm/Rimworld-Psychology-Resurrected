using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Psychology;

// The walk-up and embrace after an accepted hug (InteractionWorker_Hug). Both pawns get this job: each walks to their own
// of two neighbouring cells near the midpoint (TargetB), waits for the other, then they turn to face each other and lean
// together for a couple of seconds. The partner is TargetA. A pawn's interrupted job is suspended and resumes afterwards;
// one that can't be suspended ends, and the pawn picks a new job. If either has dropped out before the embrace, the other
// stops too.
public class JobDriver_Hug : JobDriver
{
  private const int HugTicks = 150;
  private const int ExpiryTicks = 900;
  private const int LeanTicks = 25;
  private const float LeanDistance = 0.3f;
  private const float MeetingRadius = 2.9f;

  private bool hugging;
  private int startedTick = -1;

  private Pawn Partner => (Pawn)TargetThingA;

  // Starts the hug if both can drop what they're doing and there's somewhere to stand. Otherwise the hug stays a bubble
  // and a log line, and whyNot says what stopped it.
  public static bool TryStart(Pawn initiator, Pawn recipient, out string whyNot)
  {
    whyNot = BusyReason(initiator) ?? BusyReason(recipient);
    if (whyNot != null)
    {
      return false;
    }
    if (!TryFindCells(initiator, recipient, out IntVec3 initiatorCell, out IntVec3 recipientCell))
    {
      whyNot = "nowhere to stand";
      return false;
    }
    Start(initiator, recipient, initiatorCell);
    Start(recipient, initiator, recipientCell);
    if (initiator.CurJobDef != JobDefOfPsychology.Hug || recipient.CurJobDef != JobDefOfPsychology.Hug)
    {
      whyNot = "the job didn't start";
      return false;
    }
    return true;
  }

  private static void Start(Pawn pawn, Pawn partner, IntVec3 cell)
  {
    Job job = JobMaker.MakeJob(JobDefOfPsychology.Hug, partner, cell);
    job.expiryInterval = ExpiryTicks;
    pawn.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: true);
  }

  // Not while the player has ordered the job, nor during anything urgent, a lord's duty (rituals, caravans, dates) or
  // while carrying something (the carried thing would be dropped).
  private static string BusyReason(Pawn pawn)
  {
    Job job = pawn.CurJob;
    string reason = null;
    if (!pawn.Spawned || pawn.Downed || pawn.Drafted || pawn.InMentalState || !pawn.Awake())
    {
      reason = "unavailable";
    }
    else if (pawn.GetLord() != null)
    {
      reason = "in a lord duty";
    }
    else if (pawn.carryTracker?.CarriedThing != null)
    {
      reason = "carrying something";
    }
    else if (pawn.Map.dangerWatcher.DangerRating == StoryDanger.High)
    {
      reason = "danger";
    }
    else if (job != null && job.playerForced)
    {
      reason = $"on a player order ({job.def.defName})";
    }
    else if (job != null && !job.def.casualInterruptible)
    {
      reason = $"job can't be interrupted ({job.def.defName})";
    }
    return reason == null ? null : $"{pawn.LabelShort} {reason}";
  }

  private static bool TryFindCells(Pawn a, Pawn b, out IntVec3 cellA, out IntVec3 cellB)
  {
    IntVec3 middle = new IntVec3((a.Position.x + b.Position.x) / 2, 0, (a.Position.z + b.Position.z) / 2);
    foreach (IntVec3 cell in GenRadial.RadialCellsAround(middle, MeetingRadius, true))
    {
      if (!CanStand(a, cell))
      {
        continue;
      }
      bool found = false;
      IntVec3 best = default;
      foreach (IntVec3 offset in GenAdj.CardinalDirections)
      {
        IntVec3 neighbour = cell + offset;
        if (CanStand(b, neighbour) && (!found || neighbour.DistanceToSquared(b.Position) < best.DistanceToSquared(b.Position)))
        {
          found = true;
          best = neighbour;
        }
      }
      if (found)
      {
        cellA = cell;
        cellB = best;
        return true;
      }
    }
    cellA = cellB = IntVec3.Invalid;
    return false;
  }

  private static bool CanStand(Pawn pawn, IntVec3 cell)
  {
    Map map = pawn.Map;
    if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetDoor(map) != null || cell.IsForbidden(pawn))
    {
      return false;
    }
    Pawn occupant = cell.GetFirstPawn(map);
    return (occupant == null || occupant == pawn) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some);
  }

  public override Vector3 ForcedBodyOffset
  {
    get
    {
      Pawn partner = Partner;
      if (!hugging || partner == null || !partner.Spawned)
      {
        return Vector3.zero;
      }
      int elapsed = HugTicks - ticksLeftThisToil;
      float lean = Mathf.Clamp01(Mathf.Min(elapsed, ticksLeftThisToil) / (float)LeanTicks);
      return (partner.Position - pawn.Position).ToVector3().normalized * (LeanDistance * lean);
    }
  }

  public override bool TryMakePreToilReservations(bool errorOnFailed)
  {
    return true;
  }

  public override void Notify_Starting()
  {
    base.Notify_Starting();
    startedTick = Find.TickManager.TicksGame;
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Values.Look(ref hugging, "hugging");
    Scribe_Values.Look(ref startedTick, "startedTick", -1);
  }

  protected override IEnumerable<Toil> MakeNewToils()
  {
    this.FailOnDespawnedOrNull(TargetIndex.A);
    // Not on the tick the job starts: fail conditions run as the first toil begins, and the second pawn's job only starts
    // just after the first's.
    AddFailCondition(() => !hugging && Find.TickManager.TicksGame != startedTick && !PartnerStillComing());

    yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

    Toil wait = ToilMaker.MakeToil("MakeNewToils");
    wait.tickIntervalAction = delegate
    {
      pawn.rotationTracker.FaceCell(Partner.Position);
      Pawn partner = Partner;
      if (!partner.pather.Moving && partner.CurJob?.targetB.Cell == partner.Position)
      {
        ReadyForNextToil();
      }
    };
    wait.handlingFacing = true;
    wait.defaultCompleteMode = ToilCompleteMode.Never;
    yield return wait;

    Toil hug = ToilMaker.MakeToil("MakeNewToils");
    hug.initAction = delegate
    {
      hugging = true;
      if (LovePartnerRelationUtility.LovePartnerRelationExists(pawn, Partner))
      {
        FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
      }
    };
    hug.tickIntervalAction = delegate
    {
      pawn.rotationTracker.FaceCell(Partner.Position);
    };
    hug.handlingFacing = true;
    hug.defaultCompleteMode = ToilCompleteMode.Delay;
    hug.defaultDuration = HugTicks;
    yield return hug;
  }

  private bool PartnerStillComing()
  {
    Pawn partner = Partner;
    return partner.CurJobDef == JobDefOfPsychology.Hug && partner.CurJob.targetA.Thing == pawn;
  }
}
