using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
  // A hookup or affair (RomanceAttemptRouter.ProposeHookup gives each of the pair this job, with their own bed slot).
  // Modelled on vanilla's JobDriver_Lovin: both lie down in the bed (vanilla's lay-down toil, so comfort and the lying
  // posture work as in vanilla), lovin' lasts 1000 ticks (2750 with a love enhancer) and gives recreation, and when it
  // finishes each pawn gets vanilla's lovin' thought, history events, lovin' cooldown and pregnancy roll, and rates the
  // hookup (a fun or awful hookup memory, RomanceUtility.GainHookupMemory). Partners who
  // mind being cheated on get "cheated on me": always when they're on the same map, 25% otherwise.
  public class JobDriver_DoLovinCasual : JobDriver
  {
    private const TargetIndex PartnerInd = TargetIndex.A;
    private const TargetIndex BedInd = TargetIndex.B;
    private const TargetIndex SlotInd = TargetIndex.C;
    private const int TicksForEnhancer = 2750;
    private const int TicksOtherwise = 1000;
    private const int TicksToWaitForPartner = 300;
    private const float PregnancyChance = 0.05f;
    private const float CheatNoticedOffMapChance = 0.25f;

    private int ticksLeft = -1;

    // Vanilla's lovin' cooldown (with Psychology's patch on it), from a spare driver instance; it only reads the pawn.
    private static readonly Func<Pawn, int> nextLovinTicks = AccessTools.MethodDelegate<Func<Pawn, int>>(
      AccessTools.Method(typeof(JobDriver_Lovin), "GenerateRandomMinTicksToNextLovin"), new JobDriver_Lovin());

    private Building_Bed Bed => (Building_Bed)job.GetTarget(BedInd).Thing;
    private Pawn Partner => (Pawn)job.GetTarget(PartnerInd).Thing;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
      return pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed)
        && pawn.Reserve(Bed, job, Bed.SleepingSlotsCount, 0, null, errorOnFailed);
    }

    public override bool CanBeginNowWhileLyingDown()
    {
      return JobInBedUtility.InBedOrRestSpotNow(pawn, job.GetTarget(BedInd));
    }

    private static bool InOrByBed(Building_Bed bed, Pawn p)
    {
      for (int i = 0; i < bed.SleepingSlotsCount; i++)
      {
        if (bed.GetSleepingSlotPos(i).InHorDistOf(p.Position, 1f))
        {
          return true;
        }
      }
      return false;
    }

    private bool LoveEnhancer(Pawn p)
    {
      return p.health?.hediffSet?.HasHediff(HediffDefOf.LoveEnhancer) == true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
      this.FailOnDespawnedOrNull(BedInd);
      this.FailOnDespawnedOrNull(PartnerInd);
      this.FailOn(() => !Partner.health.capacities.CanBeAwake);
      this.KeepLyingDown(BedInd);

      yield return Toils_Goto.Goto(SlotInd, PathEndMode.OnCell);

      // Wait (up to 5 seconds) for the partner to reach the bed.
      Toil wait = ToilMaker.MakeToil("WaitForPartner");
      wait.initAction = delegate { ticksLeftThisToil = TicksToWaitForPartner; };
      wait.tickIntervalAction = delegate
      {
        if (InOrByBed(Bed, Partner))
        {
          ReadyForNextToil();
        }
      };
      wait.defaultCompleteMode = ToilCompleteMode.Delay;
      yield return wait;

      Toil start = ToilMaker.MakeToil("StartLovinCasual");
      start.initAction = delegate
      {
        if (ticksLeft < 0)
        {
          ticksLeft = LoveEnhancer(pawn) || LoveEnhancer(Partner) ? TicksForEnhancer : TicksOtherwise;
          NoticeCheating();
        }
      };
      start.defaultCompleteMode = ToilCompleteMode.Instant;
      yield return start;

      Toil lovin = Toils_LayDown.LayDown(BedInd, hasBed: true, lookForOtherJobs: false, canSleep: false, gainRestAndHealth: false);
      lovin.AddFailCondition(() => Partner.Dead || (ticksLeft > 100 && !InOrByBed(Bed, Partner)));
      lovin.AddPreTickIntervalAction(delegate (int delta)
      {
        ticksLeft -= delta;
        if (ticksLeft <= 0)
        {
          ReadyForNextToil();
          return;
        }
        if (pawn.IsHashIntervalTick(100, delta))
        {
          FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
        }
        if (pawn.needs?.joy != null)
        {
          JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
        }
      });
      lovin.socialMode = RandomSocialMode.Off;
      yield return lovin;

      // Only when the lovin' actually finishes (vanilla does this on any end of the lay-down toil).
      Toil finish = ToilMaker.MakeToil("FinishLovinCasual");
      finish.initAction = Finished;
      finish.defaultCompleteMode = ToilCompleteMode.Instant;
      yield return finish;
    }

    // This pawn's partners who mind (RomanceUtility.IsThisCheating) find out: always on the same map, 25% elsewhere.
    // Not those whose ideoligion approves of free love.
    private void NoticeCheating()
    {
      RomanceUtility.IsThisCheating(pawn, Partner, out List<Pawn> cheatedOn);
      foreach (Pawn p in cheatedOn)
      {
        if (p.Dead || p.needs?.mood == null)
        {
          continue;
        }
        if (PreceptDefOfPsychology.Lovin_FreeApproved != null && p.Ideo?.HasPrecept(PreceptDefOfPsychology.Lovin_FreeApproved) == true)
        {
          continue;
        }
        if ((p.MapHeld != null && p.MapHeld == pawn.MapHeld) || Rand.Chance(CheatNoticedOffMapChance))
        {
          p.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.CheatedOnMe, pawn);
        }
      }
    }

    private void Finished()
    {
      Pawn partner = Partner;
      if (pawn.needs?.mood != null)
      {
        Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
        if (LoveEnhancer(pawn) || LoveEnhancer(partner))
        {
          thought.moodPowerFactor = 1.5f;
        }
        pawn.needs.mood.thoughts.memories.TryGainMemory(thought, partner);
      }
      Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.GotLovin, pawn.Named(HistoryEventArgsNames.Doer)));
      HistoryEventDef def = pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner) ? HistoryEventDefOf.GotLovin_Spouse : HistoryEventDefOf.GotLovin_NonSpouse;
      Find.HistoryEventsManager.RecordEvent(new HistoryEvent(def, pawn.Named(HistoryEventArgsNames.Doer)));
      pawn.mindState.canLovinTick = Find.TickManager.TicksGame + nextLovinTicks(pawn);
      RomanceUtility.GainHookupMemory(pawn, partner);
      TryImpregnate(partner);
    }

    // As vanilla's lovin': each of the pair rolls, scaled by the couple's pregnancy approach and fertility.
    private void TryImpregnate(Pawn partner)
    {
      if (!ModsConfig.BiotechActive)
      {
        return;
      }
      Pawn man = pawn.gender == Gender.Male ? pawn : (partner.gender == Gender.Male ? partner : null);
      Pawn woman = pawn.gender == Gender.Female ? pawn : (partner.gender == Gender.Female ? partner : null);
      if (man == null || woman == null || !Rand.Chance(PregnancyChance * PregnancyUtility.PregnancyChanceForPartners(woman, man)))
      {
        return;
      }
      GeneSet genes = PregnancyUtility.GetInheritedGeneSet(man, woman, out bool success);
      if (success)
      {
        Hediff_Pregnant pregnancy = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, woman);
        pregnancy.SetParents(null, man, genes);
        woman.health.AddHediff(pregnancy);
      }
      else if (PawnUtility.ShouldSendNotificationAbout(man) || PawnUtility.ShouldSendNotificationAbout(woman))
      {
        Messages.Message("MessagePregnancyFailed".Translate(man.Named("FATHER"), woman.Named("MOTHER")) + ": " + "CombinedGenesExceedMetabolismLimits".Translate(),
          new LookTargets(man, woman), MessageTypeDefOf.NegativeEvent);
      }
    }
  }
}
