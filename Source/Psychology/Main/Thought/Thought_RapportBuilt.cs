using RimWorld;
using Verse;

namespace Psychology;

// Vanilla's "rapport built" memory (a warden chatting with a prisoner, +2 opinion of the warden each time; set as the
// RapportBuilt thoughtClass in ThoughtPatches.xml). With the "adjust rapport based on compatibility" setting on, each
// memory's opinion is multiplied by 1 + 2 x the two pawns' personality compatibility: about +2 for a typical pair, up
// to +6 for the best fits, and negative ("strained rapport") when their personalities clash. Off: vanilla +2.
public class Thought_RapportBuilt : Thought_MemorySocial
{
  private float cachedFactor = 1f;
  private int factorTick = -GenDate.TicksPerHour;

  public float CompatibilityFactor
  {
    get
    {
      if (!PsychologySettings.adjustRapportByCompatibility || otherPawn == null || !PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.PsychologyEnabled(otherPawn))
      {
        return 1f;
      }
      // Personalities change slowly; refresh about once an in-game hour.
      int ticks = Find.TickManager.TicksGame;
      if (ticks - factorTick >= GenDate.TicksPerHour)
      {
        cachedFactor = 1f + 2f * PsycheHelper.Comp(pawn).Psyche.PersonalityCompatibilityWith(otherPawn);
        factorTick = ticks;
      }
      return cachedFactor;
    }
  }

  public override float OpinionOffset()
  {
    return base.OpinionOffset() * CompatibilityFactor;
  }

  public override string LabelCap => CompatibilityFactor < 0f ? "RapportStrainedLabel".Translate().CapitalizeFirst() : base.LabelCap;
}
