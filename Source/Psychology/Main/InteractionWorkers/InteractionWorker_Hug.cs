using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology;

// A hug between two people close to each other, picked in vanilla's random social roll alongside chitchat, conversations
// and romance attempts. Only for a strong bond, felt both ways (see HasStrongBond). A hug gives both a little mood and
// opinion; a turned-down one costs the initiator a little. A pair can't try again while the memory lasts. When both can
// stop what they're doing they walk together and embrace (JobDriver_Hug).
public class InteractionWorker_Hug : InteractionWorker
{
  private const float BaseWeight = 0.1f;
  private const float PartnerWeightFactor = 2f;

  // Lowest opinion each must hold of the other.
  private const float MinOpinionPartner = 10f;
  private const float MinOpinionFamily = 20f;
  private const float MinOpinionFriend = 40f;

  private static readonly HashSet<PawnRelationDef> closeFamily = new HashSet<PawnRelationDef>();

  // setting x 0.1 x 2 for lovers/fiances/spouses x the initiator's warmth: Friendly, Empathetic and Emotional above
  // average raise it, below average lower it (about x4 to /4 at the extremes).
  public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
  {
    if (PsychologySettings.hugChance <= 0f || !CanHug(initiator, recipient) || !HasStrongBond(initiator, recipient) || HugMemoryExists(initiator, recipient))
    {
      return 0f;
    }
    Pawn_PsycheTracker psyche = PsycheHelper.Comp(initiator).Psyche;
    float warmth = 2f * (psyche.GetPersonalityRating(PersonalityNodeDefOf.Friendly) - 0.5f
      + psyche.GetPersonalityRating(PersonalityNodeDefOf.Empathetic) - 0.5f
      + psyche.GetPersonalityRating(PersonalityNodeDefOf.Emotional) - 0.5f);
    float warmthFactor = warmth > 0f ? 1f + warmth : 1f / (1f - warmth);
    float partnerFactor = LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient) ? PartnerWeightFactor : 1f;
    return PsychologySettings.hugChance * BaseWeight * partnerFactor * warmthFactor;
  }

  public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
  {
    base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
    float chance = AcceptChance(recipient, initiator);
    bool accepted = Rand.Chance(chance);
    bool embraced = false;
    if (accepted)
    {
      extraSentencePacks.Add(RulePackDefOfPsychology.HugAccepted);
      initiator.needs?.mood?.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.Hug, recipient);
      recipient.needs?.mood?.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.Hug, initiator);
      embraced = JobDriver_Hug.TryStart(initiator, recipient);
    }
    else
    {
      extraSentencePacks.Add(RulePackDefOfPsychology.HugDeclined);
      initiator.needs?.mood?.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.HugDeclined, recipient);
    }
    PsychologyDebug.Log($"Hug: {initiator} and {recipient}: {(accepted ? embraced ? "accepted, walking over" : "accepted, too busy to stop" : "turned down")} (chance {chance:P0})");
  }

  // 60%, moved by the recipient's Friendly and Empathetic and by their opinion of the initiator; 40% to 97%.
  public static float AcceptChance(Pawn recipient, Pawn initiator)
  {
    Pawn_PsycheTracker psyche = PsycheHelper.Comp(recipient).Psyche;
    float chance = 0.6f
      + psyche.GetPersonalityRating(PersonalityNodeDefOf.Friendly) - 0.5f
      + psyche.GetPersonalityRating(PersonalityNodeDefOf.Empathetic) - 0.5f
      + recipient.relations.OpinionOf(initiator) / 200f;
    return Mathf.Clamp(chance, 0.4f, 0.97f);
  }

  private static bool CanHug(Pawn initiator, Pawn recipient)
  {
    return PsycheHelper.PsychologyEnabled(initiator) && PsycheHelper.PsychologyEnabled(recipient)
      && initiator.Faction == recipient.Faction
      && !initiator.DevelopmentalStage.Baby() && !recipient.DevelopmentalStage.Baby()
      && !recipient.InMentalState
      && !initiator.story.traits.HasTrait(TraitDefOf.Psychopath);
  }

  // Lovers, fiances and spouses at 10+ opinion each way; close family (parents, children, siblings, grandparents and
  // grandchildren) at 20+; anyone else at 40+. Children only hug family.
  public static bool HasStrongBond(Pawn a, Pawn b)
  {
    if (a.relations == null || b.relations == null)
    {
      return false;
    }
    float opinion = Mathf.Min(a.relations.OpinionOf(b), b.relations.OpinionOf(a));
    if (opinion < MinOpinionPartner)
    {
      return false;
    }
    if (LovePartnerRelationUtility.LovePartnerRelationExists(a, b))
    {
      return true;
    }
    if (opinion < MinOpinionFamily)
    {
      return false;
    }
    if (a.GetRelations(b).Any(IsCloseFamily))
    {
      return true;
    }
    return opinion >= MinOpinionFriend && a.DevelopmentalStage.Adult() && b.DevelopmentalStage.Adult();
  }

  private static bool IsCloseFamily(PawnRelationDef relation)
  {
    if (closeFamily.Count == 0)
    {
      closeFamily.Add(PawnRelationDefOf.Parent);
      closeFamily.Add(PawnRelationDefOf.Child);
      closeFamily.Add(PawnRelationDefOf.Sibling);
      closeFamily.Add(PawnRelationDefOf.HalfSibling);
      closeFamily.Add(PawnRelationDefOf.Grandparent);
      closeFamily.Add(PawnRelationDefOf.Grandchild);
    }
    return closeFamily.Contains(relation);
  }

  // The cooldown: either's memory of a hug with the other, or the initiator's of being turned down.
  private static bool HugMemoryExists(Pawn initiator, Pawn recipient)
  {
    return HasMemory(initiator, recipient) || HasMemory(recipient, initiator);
  }

  private static bool HasMemory(Pawn pawn, Pawn other)
  {
    List<Thought_Memory> memories = pawn.needs?.mood?.thoughts.memories.Memories;
    if (memories == null)
    {
      return false;
    }
    for (int i = 0; i < memories.Count; i++)
    {
      Thought_Memory memory = memories[i];
      if (memory.otherPawn == other && (memory.def == ThoughtDefOfPsychology.Hug || memory.def == ThoughtDefOfPsychology.HugDeclined))
      {
        return true;
      }
    }
    return false;
  }
}
