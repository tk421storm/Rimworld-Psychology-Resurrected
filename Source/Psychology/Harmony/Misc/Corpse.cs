using RimWorld;
using Verse;
using HarmonyLib;

namespace Psychology.Harmony;

// Seeing a corpse lying around (fresh or rotting) has a small daily-seeded chance (1 in 1000) of giving the observer the
// Desensitized trait. Since Ideology, corpses report being seen as a history event rather than an observed thought, and
// PawnObserver asks for it in a local function, so this postfixes the corpse's side instead.
[HarmonyPatch(typeof(Corpse), nameof(Corpse.GiveObservedHistoryEvent))]
public static class Corpse_GiveObservedHistoryEvent_Patch
{
    [HarmonyPostfix]
    public static void AddDesensitizedChance(HistoryEventDef __result, Pawn observer)
    {
        if (__result != HistoryEventDefOf.ObservedLayingCorpse && __result != HistoryEventDefOf.ObservedLayingRottingCorpse)
        {
            return;
        }
        if (observer?.story?.traits == null || observer.needs?.mood == null)
        {
            return;
        }
        if (!observer.story.traits.HasTrait(TraitDefOfPsychology.BleedingHeart) && !observer.story.traits.HasTrait(TraitDefOf.Psychopath) && !observer.story.traits.HasTrait(TraitDefOf.Bloodlust) && !observer.story.traits.HasTrait(TraitDefOfPsychology.Desensitized))
        {
            if (((observer.GetHashCode() ^ (GenLocalDate.DayOfYear(observer) + GenLocalDate.Year(observer) + (int)(GenLocalDate.DayPercent(observer) * 5) * 60) * 391) % 1000) == 0)
            {
                observer.story.traits.GainTrait(new Trait(TraitDefOfPsychology.Desensitized));
                observer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RecentlyDesensitized);
            }
        }
    }
}
