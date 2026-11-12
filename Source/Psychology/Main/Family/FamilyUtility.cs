using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology;

/// <summary>
/// Wanting children. A pawn's family drive (0 to 1) starts from their raw roll (Pawn_SexualityTracker.familyDrive) and
/// is shaped by personality and traits:
///   - personality: each node's distance from average (rating - 0.5) times its weight in PersonalityWeights, added on
///     (Romantic, Compassionate, Pure and Nostalgic raise it; Independent, Adventurous and Ambitious lower it);
///   - Matriarch/Patriarch: squeezed into 0.8-1; Child-free: squeezed into 0-0.15.
/// With Biotech, each pawn has their own pregnancy approach, the same towards anyone (PersonalApproach: avoid, normal or
/// try for a baby, from the family drive), and each pairing gets the more careful of the two (PairApproach). It's set
/// the first time anything reads a pairing's approach (OnApproachRead, from a Harmony postfix), so it also covers
/// hookups and other mods' pairings, and is brought up to date every 12 hours (UpdatePregnancyApproaches, from
/// PsychologyGameComponent) unless the player has changed it since. Child-free pawns always avoid pregnancy, whatever
/// the player or the setting says.
/// </summary>
public static class FamilyUtility
{
  private static readonly (PersonalityNodeDef node, float weight)[] PersonalityWeights =
  {
    (PersonalityNodeDefOf.Romantic, 0.3f),
    (PersonalityNodeDefOf.Compassionate, 0.2f),
    (PersonalityNodeDefOf.Pure, 0.15f),
    (PersonalityNodeDefOf.Nostalgic, 0.15f),
    (PersonalityNodeDefOf.Independent, -0.25f),
    (PersonalityNodeDefOf.Adventurous, -0.2f),
    (PersonalityNodeDefOf.Ambitious, -0.1f),
  };

  // A pawn's own approach: avoid pregnancy below AvoidBelow, try for a baby from TryForBabyFrom, otherwise normal.
  public const float AvoidBelow = 0.3f;
  public const float TryForBabyFrom = 0.7f;

  // Vanilla's per-pair approaches (Pawn_RelationsTracker.pregnancyApproaches), to find every pairing that has one.
  private static readonly AccessTools.FieldRef<Pawn_RelationsTracker, Dictionary<Pawn, PregnancyApproach>> pregnancyApproaches =
    AccessTools.FieldRefAccess<Pawn_RelationsTracker, Dictionary<Pawn, PregnancyApproach>>("pregnancyApproaches");

  public static bool IsChildFree(Pawn pawn) => pawn.story?.traits?.HasTrait(TraitDefOfPsychology.ChildFree) == true;

  public static bool IsMatriarch(Pawn pawn) => pawn.story?.traits?.HasTrait(TraitDefOfPsychology.Matriarch) == true;

  public static float FamilyDrive(Pawn pawn)
  {
    if (!PsycheHelper.PsychologyEnabled(pawn))
    {
      return 0.5f;
    }
    Pawn_PsycheTracker psyche = PsycheHelper.Comp(pawn).Psyche;
    float drive = PsycheHelper.Comp(pawn).Sexuality.familyDrive;
    foreach ((PersonalityNodeDef node, float weight) in PersonalityWeights)
    {
      drive += weight * (psyche.GetPersonalityRating(node) - 0.5f);
    }
    drive = Mathf.Clamp01(drive);
    if (IsMatriarch(pawn))
    {
      return Mathf.Lerp(0.8f, 1f, drive);
    }
    if (IsChildFree(pawn))
    {
      return 0.15f * drive;
    }
    return drive;
  }

  // Losing a child (died, lost, stillborn, miscarried) hits a matriarch harder and a child-free pawn less.
  public const float ChildLossFactorMatriarch = 1.5f;
  public const float ChildLossFactorChildFree = 0.5f;
  private static readonly string[] ChildLossThoughtNames = { "MySonDied", "MyDaughterDied", "MySonLost", "MyDaughterLost", "Stillbirth", "Miscarried", "PartnerMiscarried" };
  private static HashSet<ThoughtDef> childLossThoughts;

  public static bool IsChildLossThought(ThoughtDef def)
  {
    childLossThoughts ??= new HashSet<ThoughtDef>(ChildLossThoughtNames.Select(n => DefDatabase<ThoughtDef>.GetNamedSilentFail(n)).Where(d => d != null));
    return childLossThoughts.Contains(def);
  }

  public static float ChildLossMoodFactor(Pawn pawn)
  {
    return IsMatriarch(pawn) ? ChildLossFactorMatriarch : IsChildFree(pawn) ? ChildLossFactorChildFree : 1f;
  }

  // Living children, plus one on the way if the pawn is pregnant (Biotech). youngOnly: only those not grown up yet
  // (newborns, babies and children, i.e. under 13 for humans), plus the one on the way.
  public static int ChildCount(Pawn pawn, bool youngOnly = false)
  {
    int count = pawn.relations?.Children.Count(c => !c.Dead && (!youngOnly || c.DevelopmentalStage.Juvenile())) ?? 0;
    if (ModsConfig.BiotechActive && PregnancyUtility.GetPregnancyHediff(pawn) != null)
    {
      count++;
    }
    return count;
  }

  // Each child the pawn is still raising (ChildCount youngOnly: under adulthood, or on the way) lowers the drive used
  // for their pregnancy approach by this much, half as much for a matriarch, so the more young children, the less keen
  // on another. The family drive itself (psyche card, pregnancy attitude) isn't lowered.
  public const float ApproachDrivePerYoungChild = 0.1f;

  public static float ApproachDrive(Pawn pawn)
  {
    float perChild = IsMatriarch(pawn) ? ApproachDrivePerYoungChild / 2f : ApproachDrivePerYoungChild;
    return Mathf.Clamp01(FamilyDrive(pawn) - perChild * ChildCount(pawn, youngOnly: true));
  }

  // The approach a pawn wants with anyone: avoid if child-free, otherwise from their family drive less their young
  // children (ApproachDrive).
  public static PregnancyApproach PersonalApproach(Pawn pawn)
  {
    if (IsChildFree(pawn))
    {
      return PregnancyApproach.AvoidPregnancy;
    }
    float drive = ApproachDrive(pawn);
    return drive < AvoidBelow ? PregnancyApproach.AvoidPregnancy : drive >= TryForBabyFrom ? PregnancyApproach.TryForBaby : PregnancyApproach.Normal;
  }

  // A pair's approach: the more careful of their two (avoid, then normal, then trying), so someone who avoids
  // pregnancy avoids it with everyone, and a pair only tries for a baby if both want to.
  public static PregnancyApproach PairApproach(Pawn a, Pawn b)
  {
    PregnancyApproach approachA = PersonalApproach(a);
    PregnancyApproach approachB = PersonalApproach(b);
    return Carefulness(approachA) <= Carefulness(approachB) ? approachA : approachB;
  }

  private static int Carefulness(PregnancyApproach approach)
  {
    return approach == PregnancyApproach.AvoidPregnancy ? 0 : approach == PregnancyApproach.Normal ? 1 : 2;
  }

  // From the GetPregnancyApproachForPartner postfix, so any pairing (a lover's, a hookup's, another mod's) gets the
  // pawns' approach the moment anything asks for it: a pair with a child-free pawn is always set to avoid, and a pair
  // the pawns haven't decided on yet is set to their PairApproach. Later changes come from the 12-hourly pass.
  public static PregnancyApproach OnApproachRead(Pawn pawn, Pawn partner, PregnancyApproach current)
  {
    if (!ModsConfig.BiotechActive || partner == null || !PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.PsychologyEnabled(partner))
    {
      return current;
    }
    if (IsChildFree(pawn) || IsChildFree(partner))
    {
      Apply(pawn, partner, PregnancyApproach.AvoidPregnancy, current);
      return PregnancyApproach.AvoidPregnancy;
    }
    if (!PsychologySettings.pawnsChoosePregnancyApproach || PsycheHelper.Comp(pawn).Sexuality.pregnancyApproachChoices.ContainsKey(partner))
    {
      return current;
    }
    PregnancyApproach desired = PairApproach(pawn, partner);
    Apply(pawn, partner, desired, current);
    return desired;
  }

  // Every pairing a colonist on a map has (love partners, and anyone vanilla or another mod already keeps an approach
  // for), each once.
  public static void UpdatePregnancyApproaches()
  {
    if (!ModsConfig.BiotechActive || !Find.Storyteller.difficulty.ChildrenAllowed)
    {
      return;
    }
    HashSet<(int, int)> done = new HashSet<(int, int)>();
    foreach (Map map in Find.Maps)
    {
      foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
      {
        foreach (Pawn partner in Pairings(pawn))
        {
          if (done.Add(pawn.thingIDNumber < partner.thingIDNumber ? (pawn.thingIDNumber, partner.thingIDNumber) : (partner.thingIDNumber, pawn.thingIDNumber)))
          {
            UpdatePregnancyApproach(pawn, partner);
          }
        }
      }
    }
  }

  // Brings a pair's approach up to date with their drives (which change with traits and personality), unless it's no
  // longer what they last chose: then the player has changed it, and it's left alone. Child-free pairs are kept on
  // avoid by OnApproachRead, through the getter here.
  public static void UpdatePregnancyApproach(Pawn a, Pawn b)
  {
    if (!PsycheHelper.PsychologyEnabled(a) || !PsycheHelper.PsychologyEnabled(b))
    {
      return;
    }
    PregnancyApproach current = a.relations.GetPregnancyApproachForPartner(b);
    if (IsChildFree(a) || IsChildFree(b) || !PsychologySettings.pawnsChoosePregnancyApproach)
    {
      return;
    }
    if (PsycheHelper.Comp(a).Sexuality.pregnancyApproachChoices.TryGetValue(b, out PregnancyApproach lastChoice) && lastChoice != current)
    {
      return;
    }
    Apply(a, b, PairApproach(a, b), current);
  }

  private static void Apply(Pawn a, Pawn b, PregnancyApproach desired, PregnancyApproach current)
  {
    PsycheHelper.Comp(a).Sexuality.pregnancyApproachChoices[b] = desired;
    PsycheHelper.Comp(b).Sexuality.pregnancyApproachChoices[a] = desired;
    if (desired != current)
    {
      a.relations.SetPregnancyApproach(b, desired);
      PsychologyDebug.Log($"Pregnancy approach of {a} and {b}: {current} -> {desired} ({a.LabelShort} wants {PersonalApproach(a)}, drive {ApproachDrive(a):0.00} with {ChildCount(a, youngOnly: true)} young child(ren); {b.LabelShort} wants {PersonalApproach(b)}, drive {ApproachDrive(b):0.00} with {ChildCount(b, youngOnly: true)} young child(ren))");
    }
  }

  // Love partners plus everyone the pawn already has an approach for.
  private static List<Pawn> Pairings(Pawn pawn)
  {
    IEnumerable<Pawn> partners = LovePartnerRelationUtility.ExistingLovePartners(pawn, false).Select(r => r.otherPawn);
    Dictionary<Pawn, PregnancyApproach> approaches = pregnancyApproaches(pawn.relations);
    if (approaches != null)
    {
      partners = partners.Concat(approaches.Keys);
    }
    return partners.Where(p => p != null && !p.Dead && p != pawn).Distinct().ToList();
  }

  // For the dev menu: each colonist's family drive, own approach and children, and each pairing's approach.
  public static string Report(Map map)
  {
    List<string> lines = new List<string>();
    foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(PsycheHelper.PsychologyEnabled).OrderBy(p => p.LabelShort))
    {
      string trait = IsMatriarch(pawn) ? $", {TraitDefOfPsychology.Matriarch.DataAtDegree(0).GetLabelFor(pawn)}" : IsChildFree(pawn) ? ", child-free" : "";
      lines.Add($"  {pawn.LabelShort}: family drive {FamilyDrive(pawn):0.00} (raw {PsycheHelper.Comp(pawn).Sexuality.familyDrive:0.00}{trait}), children {ChildCount(pawn)} ({ChildCount(pawn, youngOnly: true)} young), approach drive {ApproachDrive(pawn):0.00}, wants {PersonalApproach(pawn)}");
      if (!ModsConfig.BiotechActive)
      {
        continue;
      }
      foreach (Pawn partner in Pairings(pawn).OrderBy(p => p.LabelShort))
      {
        if (!PsycheHelper.PsychologyEnabled(partner))
        {
          lines.Add($"    with {partner.LabelShort}: no psyche, left to vanilla");
          continue;
        }
        PregnancyApproach current = pawn.relations.GetPregnancyApproachForPartner(partner);
        bool chosen = PsycheHelper.Comp(pawn).Sexuality.pregnancyApproachChoices.TryGetValue(partner, out PregnancyApproach lastChoice);
        string source = !chosen ? "not chosen by them yet" : lastChoice == current ? "their choice" : $"player's choice (they chose {lastChoice})";
        AcceptanceReport canHave = PregnancyUtility.CanEverProduceChild(pawn, partner);
        string partnerTag = LovePartnerRelationUtility.LovePartnerRelationExists(pawn, partner) ? " (partner)" : "";
        string cant = canHave.Accepted ? "" : $" (can't have children: {canHave.Reason})";
        lines.Add($"    with {partner.LabelShort}{partnerTag}: {current}, {source}; they'd pick {PairApproach(pawn, partner)}{cant}");
      }
    }
    return string.Join(System.Environment.NewLine, lines);
  }
}
