using Verse;

namespace Psychology;

/// <summary>
/// One completed courtship date as one pawn saw it, kept in PsychologyGameComponent's date history: who with, how it
/// went for this pawn (RomanceUtility.DateQuality, -1 to 1) and when it ended.
/// </summary>
public class DateRecord : IExposable
{
  public Pawn pawn;
  public Pawn partner;
  public float quality;
  public int tick;

  public DateRecord()
  {
  }

  public DateRecord(Pawn pawn, Pawn partner, float quality)
  {
    this.pawn = pawn;
    this.partner = partner;
    this.quality = quality;
    this.tick = Find.TickManager.TicksGame;
  }

  public void ExposeData()
  {
    Scribe_References.Look(ref this.pawn, "pawn");
    Scribe_References.Look(ref this.partner, "partner");
    Scribe_Values.Look(ref this.quality, "quality");
    Scribe_Values.Look(ref this.tick, "tick");
  }
}
