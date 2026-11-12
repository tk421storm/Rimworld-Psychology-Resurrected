using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Psychology
{
  /// <summary>
  /// What a vanilla romance attempt turns into; see RomanceAttemptRouter.GetAttemptType.
  /// </summary>
  public enum RomanceAttemptType
  {
    Direct,     // vanilla's "become lovers" roll: the end of a good date, a player-ordered romance, or nothing else fits
    DateNight,  // they're already partners: plan a date night
    Courtship,  // ask them out on a date
    Hookup,     // proposition them for a casual hookup
    Cheating,   // an affair: a hookup behind a partner's back (one who would mind)
    DateLovin   // partners on a date together: an invitation to bed
  }

  /// <summary>
  /// Routes vanilla romance attempts (InteractionDefOf.RomanceAttempt, picked by the social interaction system, the
  /// Histrionic advance job or other mods) through Psychology's romance: RomanticIntent decides between a date night,
  /// courtship and a hookup, and a hookup that would be cheating becomes an affair. The attempt is resolved on the spot
  /// so the social log entry carries its type and result; see InteractionWorker_RomanceAttempt_Interacted_Patches.
  /// </summary>
  public static class RomanceAttemptRouter
  {
    // Set while a romance attempt should run vanilla's lovers roll instead of being routed; the text is the reason
    // (RomanceUtility.TryFireRomanceAttempt at the end of a good date).
    public static string directReason;

    public static RomanceAttemptType GetAttemptType(Pawn initiator, Pawn recipient, out Pawn cheatOn, out string reason)
    {
      cheatOn = null;
      if (directReason != null)
      {
        reason = directReason;
        return RomanceAttemptType.Direct;
      }
      if (initiator.CurJob?.def == JobDefOf.TryRomance)
      {
        reason = "ordered by the player";
        return RomanceAttemptType.Direct;
      }
      RomanticIntent intent = RomanceUtility.GetRomanticIntent(initiator, recipient, out reason);
      switch (intent)
      {
        case RomanticIntent.DateNight:
          if (DateTogether(initiator, recipient) != null)
          {
            reason = "partners on a date together";
            return RomanceAttemptType.DateLovin;
          }
          return RomanceAttemptType.DateNight;
        case RomanticIntent.None:
          return RomanceAttemptType.Direct;
      }
      RomanceUtility.CheatFactor(initiator, recipient, out cheatOn);
      if (cheatOn != null && RomanceUtility.WillPawnTryHookup(initiator))
      {
        // The cheat chance was already rolled into the romance attempt's selection weight.
        reason += $", cheating on {cheatOn.LabelShort}";
        return RomanceAttemptType.Cheating;
      }
      return intent == RomanticIntent.Courtship ? RomanceAttemptType.Courtship : RomanceAttemptType.Hookup;
    }

    // Why the routed attempt can't happen right now (English, for the debug log), or null if it can. A blocked attempt
    // gets no selection weight. Direct attempts are never blocked here.
    public static string BlockReason(RomanceAttemptType type, Pawn initiator, Pawn recipient)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          return ((InteractionWorker_DateBase)InteractionDefOfPsychology.PlanDate.Worker).BlockReason(initiator, recipient);
        case RomanceAttemptType.DateLovin:
          return DateLovinBlockReason(initiator, recipient);
        case RomanceAttemptType.Courtship:
          if (PsychologySettings.courtshipChance <= 0f)
          {
            return "courtship is off";
          }
          if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
          {
            return "they turned this pawn's date request down recently";
          }
          return ((InteractionWorker_DateBase)InteractionDefOfPsychology.AskOutOnDate.Worker).BlockReason(initiator, recipient);
        case RomanceAttemptType.Hookup:
        case RomanceAttemptType.Cheating:
          if (OnVisitDuty(initiator) || OnVisitDuty(recipient))
          {
            return "visitor on a duty (trading, visiting, raiding or a quest)";
          }
          if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyHookupAttempt) > 0)
          {
            return "they turned this pawn's hookup down recently";
          }
          if (LovePartnerRelationUtility.GetLovinMtbHours(initiator, recipient) < 0f)
          {
            return "lovin' not possible (age, health or drive)";
          }
          if (Find.TickManager.TicksGame < initiator.mindState.canLovinTick || Find.TickManager.TicksGame < recipient.mindState.canLovinTick)
          {
            return "too soon after their last lovin'";
          }
          if (!new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, initiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo()
              && !initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
          {
            return "ideoligion forbids lovin outside marriage";
          }
          if (!RomanceUtility.IsPawnFree(initiator))
          {
            return "busy (needs, danger, drafted or an important job)";
          }
          return null;
        default:
          return null;
      }
    }

    // A non-colonist following one of vanilla's group orders (a trade caravan, a visit, a raid or siege, a quest's
    // lodgers or shuttle). Guests of mods with their own lord jobs, like Hospitality's, can still hook up.
    public static bool OnVisitDuty(Pawn pawn)
    {
      return !pawn.IsColonist && pawn.GetLord()?.LordJob?.GetType().Assembly == typeof(LordJob).Assembly;
    }

    // The date the two are on together, or null.
    internal static LordJob_Date DateTogether(Pawn a, Pawn b)
    {
      Lord lord = a.GetLord();
      return lord != null && lord.LordJob is LordJob_Date date && lord == b.GetLord() ? date : null;
    }

    // Selection weight of a lovin' invitation between partners on a date together: DateLovinBaseWeight x the
    // initiator's sex drive x the lovin' frequency setting. Against chitchat's 1 and the party pace of talk on a date,
    // the base gives an invitation on roughly half to three quarters of date nights for typical drives; one per date.
    public const float DateLovinBaseWeight = 0.04f;

    public static float DateLovinWeight(Pawn initiator, Pawn recipient)
    {
      if (DateLovinBlockReason(initiator, recipient) != null)
      {
        return 0f;
      }
      return DateLovinBaseWeight * PsycheHelper.Comp(initiator).Sexuality.AdjustedSexDrive * PsychologySettings.lovinFrequency;
    }

    private static string DateLovinBlockReason(Pawn initiator, Pawn recipient)
    {
      LordJob_Date date = DateTogether(initiator, recipient);
      if (date == null)
      {
        return "not on a date together";
      }
      if (date.lovinAsked)
      {
        return "already asked on this date";
      }
      if (LovePartnerRelationUtility.GetLovinMtbHours(initiator, recipient) < 0f)
      {
        return "lovin' not possible (age, health or drive)";
      }
      int now = Find.TickManager.TicksGame;
      if (now < initiator.mindState.canLovinTick || now < recipient.mindState.canLovinTick)
      {
        return "too soon after their last lovin'";
      }
      if (!initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient)
          && !new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, initiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
      {
        return "ideoligion forbids lovin outside marriage";
      }
      if (SharedBed(initiator, recipient) == null)
      {
        return "no bed they share";
      }
      return null;
    }

    // A non-medical bed for two that both own and both can reach, with no one else in it.
    private static Building_Bed SharedBed(Pawn a, Pawn b)
    {
      Building_Bed bed = a.ownership?.OwnedBed;
      if (bed == null || bed.Medical || bed.SleepingSlotsCount < 2 || !bed.OwnersForReading.Contains(b))
      {
        return null;
      }
      foreach (Pawn occupant in bed.CurOccupants)
      {
        if (occupant != a && occupant != b)
        {
          return null;
        }
      }
      return a.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some, bed.SleepingSlotsCount, 0) && b.CanReach(bed, PathEndMode.OnCell, Danger.Some) ? bed : null;
    }

    // The partner's answer: their sex drive (asexual pawns rarely say yes), mood (x0.5 at 20% or less, x1.2 at 80% or
    // more) and opinion of the asker (x0.3 at -20, x1 at +40). Their ideoligion must allow lovin' outside marriage
    // unless they're married.
    public static float DateLovinAcceptChance(Pawn recipient, Pawn initiator, out string details)
    {
      if (!recipient.relations.DirectRelationExists(PawnRelationDefOf.Spouse, initiator)
          && !new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, recipient.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
      {
        details = "recipient's ideoligion forbids lovin outside marriage";
        return 0f;
      }
      float sexDrive = PsycheHelper.Comp(recipient).Sexuality.AdjustedSexDrive;
      float drive = Mathf.Clamp01(0.1f + 0.9f * sexDrive);
      float mood = Mathf.Lerp(0.5f, 1.2f, Mathf.InverseLerp(0.2f, 0.8f, recipient.needs?.mood?.CurLevel ?? 0.5f));
      float opinion = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(-20f, 40f, recipient.relations.OpinionOf(initiator)));
      details = $"drive {drive:0.00} (sex drive {sexDrive:0.00}) x mood {mood:0.00} x opinion {opinion:0.00}";
      return Mathf.Clamp01(drive * mood * opinion);
    }

    // One invitation per date. A yes sends the asker to bed with vanilla's lovin' job (thoughts, pregnancy and the
    // lovin' cooldown as usual), which calls the partner in once there; the partner walks over at the same time, so
    // neither waits long. The date carries on afterwards.
    private static string InviteToBed(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
    {
      DateTogether(initiator, recipient).lovinAsked = true;
      float chance = DateLovinAcceptChance(recipient, initiator, out string details);
      string calc = $"accept chance {chance:P0} = {details}";
      if (!Rand.Chance(chance))
      {
        extraSentencePacks.Add(RulePackDefOfPsychology.DateLovinDeclined);
        return "not tonight; " + calc;
      }
      Building_Bed bed = SharedBed(initiator, recipient);
      extraSentencePacks.Add(RulePackDefOfPsychology.DateLovinAccepted);
      initiator.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Lovin, recipient, bed), JobCondition.InterruptForced);
      recipient.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, RestUtility.GetBedSleepingSlotPosFor(recipient, bed)), JobCondition.InterruptForced);
      return $"yes, going to {bed.LabelShort}; " + calc;
    }

    public static RulePackDef TypeSentence(RomanceAttemptType type)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          return RulePackDefOfPsychology.RomanceAttemptDateNight;
        case RomanceAttemptType.DateLovin:
          return RulePackDefOfPsychology.RomanceAttemptDateLovin;
        case RomanceAttemptType.Courtship:
          return RulePackDefOfPsychology.RomanceAttemptCourtship;
        case RomanceAttemptType.Hookup:
          return RulePackDefOfPsychology.RomanceAttemptHookup;
        case RomanceAttemptType.Cheating:
          return RulePackDefOfPsychology.RomanceAttemptCheating;
        default:
          return RulePackDefOfPsychology.RomanceAttemptDirect;
      }
    }

    /// <summary>
    /// Carries out a routed (non-Direct) attempt, adding the result sentence to the log entry. Returns the result and the
    /// calculations behind it (English, for the debug log).
    /// </summary>
    public static string Resolve(RomanceAttemptType type, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          RendezvousUtility.PlanDate(initiator, recipient, false);
          return "date night planned";
        case RomanceAttemptType.DateLovin:
          return InviteToBed(initiator, recipient, extraSentencePacks);
        case RomanceAttemptType.Courtship:
          {
            bool accepted = InteractionWorker_AskOutOnDate.Ask(initiator, recipient, extraSentencePacks, out float chance, out string details);
            return $"{(accepted ? "date accepted" : "date declined")}; accept chance {chance:P0} = {details}";
          }
        default:
          return ProposeHookup(initiator, recipient, extraSentencePacks);
      }
    }

    /// <summary>
    /// Romance while socializing (Pawn_InteractionsTracker_TryInteractRandomlyPatch). Every super-active social job
    /// (relaxing socially at a table or campfire, standing at a party or Psychology hangout, Ideology dance parties and
    /// rituals with socially active spectators, billiards) gives each of the pawn's random interactions a chance, the
    /// "romance while socializing" setting, to become a look around for romance instead. A pawn looking has a need:
    /// single, or allowed another partner, or rolling their cheat chance (RomanticNeed). Candidates are the pawns
    /// close enough to interact with now, weighted by the romance attempt's selection weight (attraction, opinion,
    /// cooldowns, the router's gates), and the one picked gets a romance attempt, routed as usual. Not on a date, nor for
    /// visitors on a vanilla group order (OnVisitDuty).
    /// True if an attempt was made, which stands in for that random interaction.
    /// </summary>
    private static readonly System.Func<Pawn_InteractionsTracker, RandomSocialMode> currentSocialMode =
      AccessTools.MethodDelegate<System.Func<Pawn_InteractionsTracker, RandomSocialMode>>(
        AccessTools.PropertyGetter(typeof(Pawn_InteractionsTracker), "CurrentSocialMode"));

    public static bool TrySocialRomance(Pawn pawn)
    {
      if (PsychologySettings.socialRomanceChance <= 0f || !PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      // Vanilla's social mode: the job's, capped by the lord duty, drafting and mental states.
      if (currentSocialMode(pawn.interactions) != RandomSocialMode.SuperActive || pawn.GetLord()?.LordJob is LordJob_Date || OnVisitDuty(pawn))
      {
        return false;
      }
      if (!Rand.Chance(PsychologySettings.socialRomanceChance / 100f))
      {
        return false;
      }
      float need = RomanticNeed(pawn, out string needReason);
      if (!Rand.Chance(need))
      {
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) isn't looking for romance: {needReason}");
        return false;
      }
      InteractionDef def = InteractionDefOf.RomanceAttempt;
      List<Pawn> candidates = new List<Pawn>();
      List<float> weights = new List<float>();
      foreach (Pawn p in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
      {
        if (p == pawn || pawn.HostileTo(p) || !SocialInteractionUtility.CanReceiveRandomInteraction(p) || !pawn.interactions.CanInteractNowWith(p, def))
        {
          continue;
        }
        float weight = def.Worker.RandomSelectionWeight(pawn, p);
        if (weight > 0f)
        {
          candidates.Add(p);
          weights.Add(weight);
        }
      }
      if (candidates.Count == 0)
      {
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) looks for romance ({needReason}): no one nearby");
        return false;
      }
      int picked = candidates.Count - 1;
      float roll = Rand.Value * weights.Sum();
      for (int i = 0; i < candidates.Count; i++)
      {
        roll -= weights[i];
        if (roll < 0f)
        {
          picked = i;
          break;
        }
      }
      if (PsychologyDebug.Enabled)
      {
        string list = string.Join(", ", candidates.Select((p, i) => $"{p.LabelShort} {weights[i]:0.00}"));
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) looks for romance ({needReason}): candidates {list} -> {candidates[picked].LabelShort}");
      }
      return pawn.interactions.TryInteractWith(candidates[picked], def);
    }

    // Whether a pawn is looking for romance, as a chance: 1 if they have no love partner or another one wouldn't be
    // cheating (ideoligion allows it, Polygamous, partners who don't mind); otherwise the cheat chance on the partner
    // they'd feel worst about cheating on (RomanceUtility.CheatFactor).
    public static float RomanticNeed(Pawn pawn, out string reason)
    {
      if (!LovePartnerRelationUtility.HasAnyLovePartner(pawn))
      {
        reason = "single";
        return 1f;
      }
      float chance = RomanceUtility.CheatFactor(pawn, null, out Pawn cheatOn);
      reason = cheatOn == null ? "has a partner, but it wouldn't be cheating" : $"would cheat on {cheatOn.LabelShort}, chance {chance:P0}";
      return chance;
    }

    // The hookup proposal, answered on the spot: the recipient must be free and willing, then rolls
    // RomanceUtility.HookupAcceptChance. A yes sends both to a bed for two (JobDriver_DoLovinCasual, which gives cheated-on
    // partners their thoughts); a no gives the asker a rebuffed memory and the other a failed-attempt memory.
    private static string ProposeHookup(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
    {
      float chance;
      string details;
      if (!RomanceUtility.IsPawnFree(recipient))
      {
        chance = 0f;
        details = "recipient busy";
      }
      else if (!RomanceUtility.WillPawnTryHookup(recipient))
      {
        chance = 0f;
        details = "recipient won't hook up (asexual, ideoligion, settings or age)";
      }
      else
      {
        chance = RomanceUtility.HookupAcceptChance(recipient, initiator, out details);
      }
      string calc = $"accept chance {chance:P0} = {details}";
      if (!Rand.Chance(chance))
      {
        initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyHookupAttempt, recipient);
        recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.FailedHookupAttemptOnMe, initiator);
        extraSentencePacks.Add(RulePackDefOfPsychology.HookupFailed);
        return "hookup declined; " + calc;
      }
      Building_Bed bed = RomanceUtility.FindHookupBed(initiator, recipient);
      if (bed == null)
      {
        extraSentencePacks.Add(RulePackDefOfPsychology.HookupNoBed);
        return "hookup accepted, but no bed for two; " + calc;
      }
      extraSentencePacks.Add(RulePackDefOfPsychology.HookupSucceeded);
      Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InitiatedLovin, initiator.Named(HistoryEventArgsNames.Doer)));
      initiator.jobs.jobQueue.EnqueueFirst(new Job(JobDefOfPsychology.DoLovinCasual, recipient, bed, bed.GetSleepingSlotPos(0)));
      recipient.jobs.jobQueue.EnqueueFirst(new Job(JobDefOfPsychology.DoLovinCasual, initiator, bed, bed.GetSleepingSlotPos(1)));
      recipient.jobs.EndCurrentJob(JobCondition.InterruptOptional);
      initiator.jobs.EndCurrentJob(JobCondition.InterruptOptional);
      return $"hookup accepted, going to {bed.LabelShort}; " + calc;
    }
  }
}
