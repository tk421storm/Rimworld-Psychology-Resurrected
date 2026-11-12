using System.Runtime.CompilerServices;
using Verse;

namespace Psychology;

// Empty hooks that the Humanoid Alien Races compatibility assembly postfixes. They live in their own class with no static
// state on purpose: that assembly patches them from a Mod constructor, before defs and languages are loaded, and Harmony
// runs the patched method's class constructor, which for SpeciesHelper or RomanceUtility would fail that early.
public static class AlienRaceHooks
{
  // Disables the psyche for non-sentient races and the age gap for ageless/non-flesh races (SpeciesHelper defaults).
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static void HeuristicSettings(SpeciesSettings settings, ThingDef def)
  {
  }

  // The race's own lovin interval curve, or null for vanilla's (RomanceUtility.GetCorrectAgeCurve).
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static SimpleCurve LovinAgeCurve(Pawn pawn)
  {
    return null;
  }
}
