using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;

namespace Psychology;

/// <summary>
/// Per-game state and periodic logic:
///   - A cache of each pawn's personality-driven daily certainty change (Ideology).
///   - On load, syncs vanilla orientation traits with the Kinsey setting.
///   - Every 12 hours, lets pawns pick their pregnancy approach (FamilyUtility.UpdatePregnancyApproaches).
///   - Once a day, lets pairs whose last courtship date went well ask for another (RomanceUtility.TrySecondDates).
///   - The date history: every completed courtship date, one DateRecord per pawn (RecordDate). Unlike the date memories
///     it doesn't expire; a pawn's own records are cleared when they marry (ClearDateHistory), and records of pawns the
///     game has discarded are dropped on save.
/// </summary>
public class PsychologyGameComponent : GameComponent
{
  public bool firstTimeWithUpdate = true;
  public bool taraiSiblingsGenerated = false;
  public Dictionary<int, float> CachedCertaintyChangePerDayDict = new Dictionary<int, float>();
  private List<DateRecord> dateHistory = new List<DateRecord>();
  // Static (not saved).
  public static int certaintyTick = 12;

  public PsychologyGameComponent(Game game)
  {
  }

  public override void ExposeData()
  {
    Scribe_Values.Look(ref this.firstTimeWithUpdate, "Psychology_FirstTimeWithUpdate", true);
    Scribe_Values.Look(ref taraiSiblingsGenerated, "Psychology_TaraiSiblingsGenerated", false);
    if (Scribe.mode == LoadSaveMode.Saving)
    {
      // A discarded pawn isn't saved, so a reference to it couldn't be loaded.
      dateHistory.RemoveAll(r => r.pawn == null || r.partner == null || r.pawn.Discarded || r.partner.Discarded);
    }
    Scribe_Collections.Look(ref dateHistory, "Psychology_DateHistory", LookMode.Deep);
    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
      dateHistory ??= new List<DateRecord>();
      dateHistory.RemoveAll(r => r.pawn == null || r.partner == null);
    }
  }

  // Records how a completed courtship date went for pawn (see DateRecord).
  public void RecordDate(Pawn pawn, Pawn partner, float quality)
  {
    dateHistory.Add(new DateRecord(pawn, partner, quality));
  }

  // Pawn's completed dates by partner, oldest first.
  public Dictionary<Pawn, List<DateRecord>> DateHistoryOf(Pawn pawn)
  {
    return dateHistory.Where(r => r.pawn == pawn).GroupBy(r => r.partner).ToDictionary(g => g.Key, g => g.ToList());
  }

  // Each pair's most recent courtship date: both pawns' latest records of each other, once per pair. A pair missing a
  // side (a pawn's records go when they marry) is left out.
  public IEnumerable<(DateRecord, DateRecord)> LastDatePerPair()
  {
    Dictionary<(Pawn, Pawn), DateRecord> last = new Dictionary<(Pawn, Pawn), DateRecord>();
    foreach (DateRecord record in dateHistory)
    {
      if (!last.TryGetValue((record.pawn, record.partner), out DateRecord previous) || record.tick >= previous.tick)
      {
        last[(record.pawn, record.partner)] = record;
      }
    }
    foreach (KeyValuePair<(Pawn, Pawn), DateRecord> entry in last)
    {
      if (entry.Key.Item1.thingIDNumber < entry.Key.Item2.thingIDNumber && last.TryGetValue((entry.Key.Item2, entry.Key.Item1), out DateRecord other))
      {
        yield return (entry.Value, other);
      }
    }
  }

  // Every pawn with a date history.
  public IEnumerable<Pawn> PawnsWithDateHistory => dateHistory.Select(r => r.pawn).Distinct();

  public void ClearDateHistory(Pawn pawn)
  {
    int removed = dateHistory.RemoveAll(r => r.pawn == pawn);
    if (removed > 0)
    {
      PsychologyDebug.Log($"Date history of {pawn} cleared ({removed} date(s))");
    }
  }

  // ToDo: check which of these things causes crash
  public override void LoadedGame()
  {
    //Log.Message("Psychology: loading game");
    //SpeciesHelper.RegisterHumanlikeSpeciesLoadedGame(); //ToDo: figure out why this causes crash
    ImplementSexualOrientation();
  }

  public override void GameComponentTick()
  {
    if (GenTicks.TicksGame % 250 == 97)
    {
      certaintyTick++;
    }
    if (GenTicks.TicksGame % (12 * GenDate.TicksPerHour) == 1291)
    {
      FamilyUtility.UpdatePregnancyApproaches();
    }
    if (GenTicks.TicksGame % GenDate.TicksPerDay == 31337 % GenDate.TicksPerDay)
    {
      RomanceUtility.TrySecondDates();
    }
  }

  // Cached CalculateCertaintyChangePerDay. Each pawn's entry is refreshed whenever (id + certaintyTick) % 100 == 37, i.e.
  // roughly once every 100 * 250 ticks (~10 in-game hours), spread out across pawns.
  public float CertaintyChange(Pawn pawn, bool addToDicts)
  {
    float certaintyChange;
    int idnumber;
    idnumber = pawn.thingIDNumber;
    if (!CachedCertaintyChangePerDayDict.TryGetValue(idnumber, out certaintyChange) || (idnumber + certaintyTick) % 100 == 37)
    {
      certaintyChange = PsycheHelper.Comp(pawn).Psyche.CalculateCertaintyChangePerDay(pawn.Ideo, addToDicts);
      CachedCertaintyChangePerDayDict[idnumber] = certaintyChange;
    }
    return certaintyChange;
  }

  public virtual void ImplementSexualOrientation()
  {
    if (PsychologySettings.enableKinsey)
    {
      foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
      {
        PsycheHelper.CorrectTraitsForPawnKinseyEnabled(pawn);
      }
    }
    else
    {
      foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
      {
        PsycheHelper.CorrectTraitsForPawnKinseyDisabled(pawn);
      }
    }
  }
}
