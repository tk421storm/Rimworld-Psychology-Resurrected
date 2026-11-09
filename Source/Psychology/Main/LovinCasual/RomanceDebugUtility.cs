using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Psychology;

/// <summary>
/// Dev-mode "DEV: Romance" button on colonists (CompPsychology.CompGetGizmosExtra) for testing dates and hookups. Lists
/// every other free colonist on the map with this pawn's RomanticIntent towards them and the chances involved, and fires
/// the matching action, skipping the random selection and cooldowns:
///   - DateNight: plans a date night (PlanDate interaction)
///   - Courtship: asks them out (AskOutOnDate interaction; the answer is still rolled)
///   - Hookup: starts the hookup job (the target still rolls whether to accept)
/// Also "start planned date now", "finish this date now", and printing the date history and the date memories (always
/// logged). Results go to the play log, and to the debug log when
/// "Print debug info" is on.
/// </summary>
[StaticConstructorOnStartup]
public static class RomanceDebugUtility
{
  private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("Things/Mote/Heart");

  public static Command_Action RomanceGizmo(Pawn pawn)
  {
    return new Command_Action
    {
      defaultLabel = "DEV: Romance",
      defaultDesc = "Force a date night, courtship date or hookup with another colonist, chosen by this pawn's romantic intent towards them, skipping the random chance and cooldowns. Also start a planned date or finish the current date now.",
      icon = Icon,
      action = () => Find.WindowStack.Add(new FloatMenu(Options(pawn)))
    };
  }

  private static List<FloatMenuOption> Options(Pawn pawn)
  {
    List<FloatMenuOption> options = new List<FloatMenuOption>();
    foreach (Hediff_PlannedDate planned in pawn.health.hediffSet.hediffs.OfType<Hediff_PlannedDate>())
    {
      options.Add(new FloatMenuOption($"Start planned {(planned.courtship ? "date" : "date night")} with {planned.partner?.LabelShort} now", () =>
      {
        planned.StartNow();
        PsychologyDebug.Log($"DEV: starting the planned date of {pawn} and {planned.partner} now");
      }));
    }
    if (pawn.GetLord() is Lord lord && lord.LordJob is LordJob_Date date)
    {
      options.Add(new FloatMenuOption($"Finish this {(date.courtship ? "date" : "date night")} now", () =>
      {
        PsychologyDebug.Log($"DEV: finishing the date of {date.initiator} and {date.recipient} now");
        date.Finished();
        lord.lordManager.RemoveLord(lord);
      }));
    }
    // A copy: FreeColonistsSpawned is a shared cached list, and the chances below query it again (colony size, safety).
    foreach (Pawn target in pawn.Map.mapPawns.FreeColonistsSpawned.ToList())
    {
      if (target != pawn)
      {
        options.Add(TargetOption(pawn, target));
      }
    }
    if (options.Count == 0)
    {
      options.Add(new FloatMenuOption("No other colonists on this map", null));
    }
    options.Add(new FloatMenuOption("Print date history (the game's record)", PrintDateHistory));
    options.Add(new FloatMenuOption("Print date memories (everyone on this map)", () => PrintDateMemories(pawn.Map)));
    options.Add(new FloatMenuOption("Ask for second dates now (every eligible pair, skipping the daily chance)", () => RomanceUtility.TrySecondDates(true)));
    options.Add(new FloatMenuOption("Print family drives and every pairing's pregnancy approach (this map)", () => Log.Message("[Psychology] Family drives on " + pawn.Map + ":" + System.Environment.NewLine + FamilyUtility.Report(pawn.Map))));
    options.Add(new FloatMenuOption("Let pawns choose their pregnancy approach now", () => FamilyUtility.UpdatePregnancyApproaches()));
    return options;
  }

  /// <summary>
  /// Logs the game's date history (PsychologyGameComponent): every pawn's completed courtship dates, by partner, with how
  /// each went for that pawn.
  /// </summary>
  public static void PrintDateHistory()
  {
    PsychologyGameComponent comp = PsycheHelper.GameComp;
    StringBuilder text = new StringBuilder();
    int pawns = 0;
    int dates = 0;
    foreach (Pawn pawn in comp.PawnsWithDateHistory.OrderBy(p => p.LabelShort))
    {
      pawns++;
      text.AppendLine($"  {pawn.LabelShort}{(pawn.Dead ? " (dead)" : "")}:");
      foreach (KeyValuePair<Pawn, List<DateRecord>> partner in comp.DateHistoryOf(pawn).OrderBy(e => e.Key.LabelShort))
      {
        dates += partner.Value.Count;
        text.AppendLine($"    {partner.Key.LabelShort}: {partner.Value.Count} date(s), average {partner.Value.Average(r => r.quality):+0.00;-0.00}: "
          + string.Join(", ", partner.Value.Select(r => $"{r.quality:+0.00;-0.00} ({DaysAgo(Find.TickManager.TicksGame - r.tick)})")));
      }
    }
    Log.Message($"[Psychology] Date history: {pawns} pawn(s), {dates} date record(s) (one per pawn per date)"
      + (pawns > 0 ? "\n" + text.ToString().TrimEnd() : ""));
  }

  /// <summary>
  /// Logs every date the pawns on the map remember with each other, from their memories (saved with each pawn) rather
  /// than the game's date history, so dates drop off when those expire. Courtship dates
  /// leave a good or bad date memory on each side, kept 20 days and up to 5 per pair, and each pawn's quality is worked
  /// back out from its opinion (opinion / the good or bad date setting); the two sides of one date are matched by age.
  /// A side whose date was too forgettable to leave a memory shows as "-". Date nights and hang-outs keep only the
  /// latest one per pair (5 and 4 days).
  /// </summary>
  public static void PrintDateMemories(Map map)
  {
    ThoughtDef good = ThoughtDefOfPsychology.DateWentWell;
    ThoughtDef bad = ThoughtDefOfPsychology.DateWentBadly;
    ThoughtDef dateNight = ThoughtDefOfPsychology.WentOnDate;
    ThoughtDef hungOut = ThoughtDefOfPsychology.HungOut;
    HashSet<Pawn> pawns = new HashSet<Pawn>(map.mapPawns.AllHumanlikeSpawned.Where(p => p.needs?.mood?.thoughts?.memories != null));
    // Each remembered date, by pair (lower thing ID first).
    Dictionary<(Pawn, Pawn), List<Thought_Memory>> byPair = new Dictionary<(Pawn, Pawn), List<Thought_Memory>>();
    foreach (Pawn pawn in pawns)
    {
      foreach (Thought_Memory memory in pawn.needs.mood.thoughts.memories.Memories)
      {
        if ((memory.def == good || memory.def == bad || memory.def == dateNight || memory.def == hungOut) && memory.otherPawn != null && pawns.Contains(memory.otherPawn))
        {
          (Pawn, Pawn) pair = pawn.thingIDNumber < memory.otherPawn.thingIDNumber ? (pawn, memory.otherPawn) : (memory.otherPawn, pawn);
          if (!byPair.TryGetValue(pair, out List<Thought_Memory> list))
          {
            byPair[pair] = list = new List<Thought_Memory>();
          }
          list.Add(memory);
        }
      }
    }
    StringBuilder text = new StringBuilder();
    int courtships = 0;
    foreach (KeyValuePair<(Pawn, Pawn), List<Thought_Memory>> entry in byPair.OrderBy(e => e.Key.Item1.LabelShort))
    {
      (Pawn a, Pawn b) = entry.Key;
      // The pair's courtship dates, newest first: a memory joins the date before it if they're within a few hours and
      // that date has nothing from this pawn yet.
      List<List<Thought_Memory>> dates = new List<List<Thought_Memory>>();
      foreach (Thought_Memory memory in entry.Value.Where(m => m.def == good || m.def == bad).OrderBy(m => m.age))
      {
        List<Thought_Memory> last = dates.LastOrDefault();
        if (last != null && memory.age - last[0].age < GenDate.TicksPerHour * 3 && !last.Any(m => m.pawn == memory.pawn))
        {
          last.Add(memory);
        }
        else
        {
          dates.Add(new List<Thought_Memory> { memory });
        }
      }
      courtships += dates.Count;
      text.AppendLine($"  {a.LabelShort} & {b.LabelShort}: {dates.Count} courtship date(s){Average(dates, a, "for " + a.LabelShort)}{Average(dates, b, "for " + b.LabelShort)}"
        + $"{Latest(entry.Value, dateNight, ", date night ")}{Latest(entry.Value, hungOut, ", hung out ")}");
      foreach (List<Thought_Memory> date in dates)
      {
        text.AppendLine($"    {DaysAgo(date[0].age)}: {a.LabelShort} {Side(date, a)}, {b.LabelShort} {Side(date, b)}");
      }
    }
    Log.Message($"[Psychology] Date memories on {map}: {byPair.Count} pair(s), {courtships} courtship date(s) remembered"
      + $" (dates are kept {good.durationDays:0} days, date nights {dateNight.durationDays:0}, hang-outs {hungOut.durationDays:0})"
      + (byPair.Count > 0 ? "\n" + text.ToString().TrimEnd() : ""));
  }

  // A pawn's quality for one courtship date, from -1 to 1, worked back out of its memory's opinion; null if the setting
  // it was scaled by is now 0.
  private static float? Quality(Thought_Memory memory)
  {
    float scale = memory.def == ThoughtDefOfPsychology.DateWentWell ? PsychologySettings.dateOpinionGood : PsychologySettings.dateOpinionBad;
    if (!(memory is Thought_MemorySocial social) || scale == 0f)
    {
      return null;
    }
    float quality = Mathf.Abs(social.opinionOffset / scale);
    return memory.def == ThoughtDefOfPsychology.DateWentWell ? quality : -quality;
  }

  private static string Side(List<Thought_Memory> date, Pawn pawn)
  {
    Thought_Memory memory = date.FirstOrDefault(m => m.pawn == pawn);
    if (memory == null)
    {
      return "-";
    }
    float? quality = Quality(memory);
    return (memory.def == ThoughtDefOfPsychology.DateWentWell ? "good" : "bad") + (quality.HasValue ? $" {quality.Value:+0.00;-0.00}" : "");
  }

  private static string Average(List<List<Thought_Memory>> dates, Pawn pawn, string label)
  {
    List<float> qualities = dates.Select(d => d.FirstOrDefault(m => m.pawn == pawn)).Where(m => m != null).Select(Quality).Where(q => q.HasValue).Select(q => q.Value).ToList();
    return qualities.Count == 0 ? "" : $", average {qualities.Average():+0.00;-0.00} {label}";
  }

  private static string Latest(List<Thought_Memory> memories, ThoughtDef def, string label)
  {
    Thought_Memory latest = memories.Where(m => m.def == def).OrderBy(m => m.age).FirstOrDefault();
    return latest == null ? "" : label + DaysAgo(latest.age);
  }

  private static string DaysAgo(int ticks) => $"{ticks / (float)GenDate.TicksPerDay:0.0} d ago";

  private static FloatMenuOption TargetOption(Pawn pawn, Pawn target)
  {
    RomanticIntent intent = RomanceUtility.GetRomanticIntent(pawn, target, out string reason);
    string label = $"{target.LabelShort}: {intent}";
    switch (intent)
    {
      case RomanticIntent.DateNight:
        {
          InteractionDef def = InteractionDefOfPsychology.PlanDate;
          string blocked = ((InteractionWorker_DateBase)def.Worker).BlockReason(pawn, target);
          label += $" (weight {def.Worker.RandomSelectionWeight(pawn, target):0.00}{(blocked != null ? ", normally blocked: " + blocked : "")})";
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} plans a date night with {target} ({reason})");
            RomanceUtility.ForceInteraction(pawn, target, def);
          });
        }
      case RomanticIntent.Courtship:
        {
          InteractionDef def = InteractionDefOfPsychology.AskOutOnDate;
          string blocked = ((InteractionWorker_DateBase)def.Worker).BlockReason(pawn, target);
          if (blocked == null && RomanceUtility.CountMemoriesOf(pawn, target, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
          {
            blocked = "they turned this pawn down recently";
          }
          float weight = InteractionWorker_AskOutOnDate.AskWeight(pawn, target, true, out string weightDetails);
          float accept = InteractionWorker_AskOutOnDate.AcceptChance(target, pawn);
          label += $" (romance attempt weight {InteractionDefOf.RomanceAttempt.Worker.RandomSelectionWeight(pawn, target):0.00} incl. courtship {weight:0.00}, accept {accept:P0}{(blocked != null ? ", normally blocked: " + blocked : "")})";
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} asks {target} out ({reason}); ask weight {weight:0.00} = {weightDetails}");
            RomanceUtility.ForceInteraction(pawn, target, def);
          });
        }
      case RomanticIntent.Hookup:
        {
          float cheat = RomanceUtility.CheatFactor(pawn, target, out Pawn cheatOn);
          Building_Bed bed = JoyGiver_CasualHookup.FindHookupBed(pawn, target);
          label += $" (cheat chance {cheat:P0}{(cheatOn != null ? " on " + cheatOn.LabelShort : "")}{(bed == null ? ", no bed for two" : "")})";
          if (bed == null)
          {
            return new FloatMenuOption(label, null);
          }
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} goes to propose a hookup to {target} ({reason}); skipped cheat check {cheat:P0}");
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOfPsychology.LeadHookup, target, bed));
          });
        }
      default:
        return new FloatMenuOption($"{label} - {reason}", null);
    }
  }
}
