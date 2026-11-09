using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime;
using System.Security.Cryptography;

namespace Psychology.Harmony;

// How likely a pawn is to hit on someone. The prefix replaces vanilla's value outright (and HarmonyInitialize removes
// other mods' patches on this method).
// Hard zeros: already lovers, tutorial, a Codependent who already has a partner, species/age doesn't allow dating, a
// recipient in a mental state (unless the initiator is Experimental 0.8+ or a Lecher), low attraction
// (SecondaryRomanceChanceFactor < 0.15), or opinion below the "romance opinion threshold" setting (except Lechers).
// Otherwise 1.15 x setting x the product of:
//   - attraction above the 0.15 cut-off, rescaled to 0..1
//   - opinion factor: steeper for Romantic pawns (who need to really like someone); Psychopaths need less
//   - existing partner: unless their ideoligion allows another partner (or they're Polygamous, or no partner would
//     mind), the cheat chance on the partner they'd feel worst about cheating on (RomanceUtility.CheatFactor)
//   - sexuality: 0.05 if they already know the recipient isn't into their gender; without Kinsey, 0.15 when the Gay
//     traits don't line up (Bisexual isn't considered here)
//   - "straight woman" factor (women initiate less in vanilla; 1.5/1.6 made it 1.15-2x with Kinsey)
//   - confidence: 2 x Confident + Aggressive (1.5/1.6 change)
// Then the router (RomanceAttemptRouter) decides what the attempt becomes; it gets no weight if that can't happen now.
// Routed to courtship, it also gets the courtship weight (InteractionWorker_AskOutOnDate.AskWeight), as the romance
// attempt is the one random way to ask someone out. Partners only get a weight on a date together, where the attempt
// is an invitation to lovin' (RomanceAttemptRouter.DateLovinWeight).
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight))]
public static class InteractionWorker_RomanceAttempt_SelectionWeightPatch
{
  [HarmonyPrefix]
  public static bool RandomSelectionWeightPrefix(ref float __result, Pawn initiator, Pawn recipient)
  {
    if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
    {
      // Pawns without a psyche (either side) romance as in vanilla.
      return true;
    }

    // From vanilla, no romance in these cases
    if (TutorSystem.TutorialMode)
    {
      __result = 0f;
      return false;
    }
    // Partners (an invitation to lovin' on a date together) are handled by PartnerWeightPrefix, which runs first.

    // Codependents won't romance anyone if they are in a relationship
    if (LovePartnerRelationUtility.HasAnyLovePartner(initiator) && initiator.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
    {
      __result = 0f;
      return false;
    }

    if (!SpeciesHelper.RomanceEnabled(initiator, true) || !SpeciesHelper.RomanceEnabled(recipient, true))
    {
      // No dating for non-teens, no exceptions
      __result = 0f;
      return false;
    }

    //Don't hit on people in mental breaks... unless you're really freaky.
    float initiatorExperimental = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
    bool initiatorLecher = initiator.story.traits.HasTrait(TraitDefOfPsychology.Lecher);
    bool initiatorPsychopath = initiator.story.traits.HasTrait(TraitDefOf.Psychopath);

    if (recipient.InMentalState && initiatorExperimental < 0.8f && !initiatorLecher)
    {
      __result = 0f;
      return false;
    }
    /* ROMANCE CHANCE FACTOR INCLUDES THE FOLLOWING: */
    /* - SEXUAL PREFERENCE FACTOR */
    /* - AGE FACTOR */
    /* - OTHER PAWN BEAUTY FACTOR */
    /* - PAWN SEX AND ROMANCE DRIVE FACTORS */
    /* - INCEST FACTOR */
    /* - PSYCHIC LOVE SPELL FACTOR */

    float romChance = initiator.relations.SecondaryRomanceChanceFactor(recipient);
    if (romChance < 0.15f)
    {
      __result = 0f;
      return false;
    }
    float romChanceMult = (romChance - 0.15f) / 0.85f;

    // Assertive pawns are more likely to make a move on attractive mates
    float initiatorAggressive = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive);
    float initiatorConfident = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Confident);
    // 1.4: confidence only stretched the upper half of romChanceMult. The 1.5/1.6 builds replaced that with a plain
    // multiplier (applied to __result below) that weights Confident twice as much as Aggressive.
    //float chanceCutOff = 0.5f;
    //float confidenceFactor = initiatorConfident + initiatorAggressive;
    //float adjResult = romChanceMult < chanceCutOff ? romChanceMult : chanceCutOff + confidenceFactor * (romChanceMult - chanceCutOff);
    //romChanceMult = adjResult;
    float confidenceFactor = 2f * initiatorConfident + initiatorAggressive;

    /* INITIATOR OPINION FACTOR */
    float initiatorRomantic = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic);
    float initiatorOpinion = (float)initiator.relations.OpinionOf(recipient);
    float initiatorOpinMult;

    //Only lechers will romance someone that has less than base opinion of them
    if (!initiatorLecher)
    {
      if (initiatorOpinion < PsychologySettings.romanceOpinionThreshold)
      {
        __result = 0f;
        return false;
      }
      // Romantic pawns are more responsive to their opinion of the recipient
      float x = Mathf.InverseLerp(PsychologySettings.romanceOpinionThreshold, 100f, initiatorOpinion);
      initiatorOpinMult = 0.5f * Mathf.Pow(2f * x, 2f * initiatorRomantic + 1e-5f);
      if (initiatorPsychopath)
      {
        // Psychopaths have lower opinion standards
        initiatorOpinMult = 0.3f + 0.7f * initiatorOpinMult;
      }
    }
    else
    {
      // Lechers are more frisky
      initiatorOpinMult = 1f;
    }

    /* INITIATOR EXISTING LOVE PARTNER FACTOR */
    // Romancing someone else while their ideoligion allows no more partners is cheating (a successful attempt makes both
    // pawns leave their partners, who get "cheated on me"), so it's weighted the same way as dates and casual hookups
    // (RomanceUtility.CheatFactor: the cheat chance on the partner they'd feel worst about cheating on). Polygamous
    // pawns aren't held back, nor are pawns whose partners are all Polygamous.
    float existingPartnerMult = RomanceUtility.CheatFactor(initiator, recipient, out _);

    /* INITIATOR KNOWN SEXUALITY FACTOR */
    float knownSexFactor;
    float straightWomanFactor;
    if (PsychologySettings.enableKinsey)
    {
      //People who have hit on someone in the past and been rejected because of their sexuality will rarely attempt to hit on them again.
      knownSexFactor = (PsycheHelper.Comp(initiator).Sexuality.IncompatibleSexualityKnown(recipient) && !initiatorLecher) ? 0.05f : 1f;
      // Not sure whether to keep this mechanic...
      float kinseyFactor = PsycheHelper.Comp(initiator).Sexuality.kinseyRating / 6f;
      // The 1.5/1.6 builds added +1 here, so women range 1.15-2x instead of 0.15-1x.
      straightWomanFactor = initiator.gender == Gender.Female ? Mathf.Lerp(0.15f, 1f, kinseyFactor) + 1f : 1f;
    }
    else
    {
      bool initiatorIsGay = initiator.story.traits.HasTrait(TraitDefOf.Gay);
      bool recipientIsGay = recipient.story.traits.HasTrait(TraitDefOf.Gay);
      if (initiator.gender == recipient.gender)
      {
        knownSexFactor = (!initiatorIsGay || !recipientIsGay) ? 0.15f : 1f;
      }
      else
      {
        knownSexFactor = (initiatorIsGay || recipientIsGay) ? 0.15f : 1f;
      }
      straightWomanFactor = initiatorIsGay ? 1f : initiator.gender == Gender.Female ? 0.15f : 1f;
    }

    // Include chance multiplier from settings
    __result = 1.15f * PsychologySettings.romanceChanceMultiplier * romChanceMult * initiatorOpinMult * existingPartnerMult * knownSexFactor * straightWomanFactor * confidenceFactor;

    // The attempt is routed to a date night, courtship or hookup (RomanceAttemptRouter); none if that can't happen now.
    RomanceAttemptType type = RomanceAttemptRouter.GetAttemptType(initiator, recipient, out _, out _);
    if (type == RomanceAttemptType.Direct || RomanceAttemptRouter.BlockReason(type, initiator, recipient) != null)
    {
      __result = 0f;
      return false;
    }
    if (type == RomanceAttemptType.Courtship)
    {
      __result += InteractionWorker_AskOutOnDate.AskWeight(initiator, recipient, false, out _);
    }
    return false;
  }

  // Partners: the romance attempt is only an invitation to lovin' on a date together (RomanceAttemptRouter.DateLovinWeight).
  // Runs before other mods' prefixes, so mods that gate new romances (e.g. No Hopeless Romance, which blocks anyone who
  // has a lover or spouse) don't also block couples' date-night invitations; returning false skips the later prefixes.
  [HarmonyPrefix]
  [HarmonyPriority(Priority.First)]
  public static bool PartnerWeightPrefix(ref float __result, Pawn initiator, Pawn recipient)
  {
    if (TutorSystem.TutorialMode || !PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient)
        || !LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient))
    {
      return true;
    }
    __result = RomanceAttemptRouter.DateLovinWeight(initiator, recipient);
    return false;
  }

  // Runs after other mods' postfixes.
  //   - Couples' date-night invitations to lovin' keep Psychology's weight: mods that scale romance attempts (e.g. Romance
  //     Tweaks' chance sliders, which also count a polyamorous pawn's second partner as cheating) are about new romances.
  //   - The age/species gate always wins, also for pawns without a psyche (who otherwise get vanilla's weight).
  [HarmonyPostfix]
  [HarmonyPriority(Priority.Last)]
  public static void RandomSelectionWeightPostfix(ref float __result, Pawn initiator, Pawn recipient)
  {
    if (!TutorSystem.TutorialMode && PsycheHelper.PsychologyEnabled(initiator) && PsycheHelper.PsychologyEnabled(recipient)
        && LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient))
    {
      __result = RomanceAttemptRouter.DateLovinWeight(initiator, recipient);
    }
    // Just to be extra sure, add a postfix lifestage check
    if (!SpeciesHelper.RomanceEnabled(initiator, true) || !SpeciesHelper.RomanceEnabled(recipient, true))
    {
      // No dating for non-teens, no exceptions
      __result = 0f;
    }
  }

}

// Chance the recipient says yes. Prefix: no psyche on either side -> vanilla's chance, untouched; age/species gate -> 0;
// Codependent recipients refuse while they have a partner and always accept an ex back. Otherwise vanilla computes
// the chance (using our patched OpinionFactor/PartnerFactor), and the postfix scales it by the romance setting and maps
// it through 1 - e^-x so it saturates below 1.
// (Before #30, pawns without a psyche got a flat weight of 1 but a success chance of 0, so they always got rejected.)
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance))]
public static class InteractionWorker_RomanceAttempt_SuccessChancePatch
{
  [HarmonyPrefix]
  public static bool SuccessChancePrefix(Pawn initiator, Pawn recipient, float baseChance, ref float __result)
  {

    if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
    {
      // No psyche on either side: vanilla's chance (the postfix only applies the age/species gate).
      return true;
    }
    if (!SpeciesHelper.RomanceEnabled(initiator, true) || !SpeciesHelper.RomanceEnabled(recipient, true))
    {
      // No underage dating
      __result = 0f;
      return false;
    }
    if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
    {
      // Codependents won't romance anyone if they are in a relationship
      if (LovePartnerRelationUtility.HasAnyLovePartner(recipient))
      {
        __result = 0f;
        return false;
      }
      // Codependents will always get back together with their ex if they don't have a love partner
      if (LovePartnerRelationUtility.ExLovePartnerRelationExists(initiator, recipient))
      {
        __result = 1f;
        return false;
      }
    }
    return true;
  }

  [HarmonyPostfix]
  public static void SuccessChancePostfix(Pawn initiator, Pawn recipient, ref float __result)
  {
    if (!SpeciesHelper.RomanceEnabled(initiator, true) || !SpeciesHelper.RomanceEnabled(recipient, true))
    {
      // No underage dating
      __result = 0f;
    }
    if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
    {
      return;
    }
    if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
    {
      // Codependents won't romance anyone if they are in a relationship
      if (LovePartnerRelationUtility.HasAnyLovePartner(recipient))
      {
        __result = 0f;
      }
      // Codependents will always get back together with their ex if they don't have a love partner
      if (LovePartnerRelationUtility.ExLovePartnerRelationExists(initiator, recipient))
      {
        __result = 1f;
      }
    }
    __result *= PsychologySettings.romanceChanceMultiplier;
    // Always prevent 100% chance, not sure whether to do this
    __result = 1f - Mathf.Exp(-__result);
  }

  //    /* ROMANCE CHANCE FACTOR INCLUDES THE FOLLOWING: */
  //    /* SEXUAL PREFERENCE FACTOR */
  //    /* AGE FACTOR */
  //    /* OTHER PAWN BEAUTY FACTOR */
  //    /* PAWN SEX AND ROMANCE DRIVE FACTORS */
  //    /* DISABILITY FACTOR */
  //    /* PSYCHIC LOVE SPELL FACTOR */
  //    bool recipientLecher = recipient.story.traits.HasTrait(TraitDefOfPsychology.Lecher);
  //    float romChanceFactor = recipient.relations.SecondaryRomanceChanceFactor(initiator);
  //    if (romChanceFactor < 0.15f && !recipientLecher)
  //    {
  //        __result = 0f;
  //        return false;
  //    }

  //    // Account for user setting of romance chance multiplier
  //    __result = 0.6f * PsychologySettings.romanceChanceMultiplier * existingLovePartnerMult * recipientOpinionFactor * romChanceFactor;
  //    // Always prevent 100% chance, not sure whether to do this
  //    __result = 1f - Mathf.Exp(-__result);

}

// Recipient's opinion factor: vanilla's "5 opinion" cut-off is replaced by the romance opinion threshold setting,
// Lecher recipients are much more open, Psychopaths less picky.
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "OpinionFactor")]
public static class InteractionWorker_RomanceAttempt_OpinionFactor_Patches
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> OpinionFactorTranspiler(IEnumerable<CodeInstruction> codes)
  {
    foreach (CodeInstruction c in codes)
    {
      if (c.OperandIs(5f))
      {
        yield return CodeInstruction.LoadField(typeof(PsychologySettings), nameof(PsychologySettings.romanceOpinionThreshold));
        continue;
      }
      yield return c;
    }
  }

  [HarmonyPostfix]
  public static void OpinionFactorPostfix(Pawn initiator, Pawn recipient, ref float __result)
  {
    if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
    {
      return;
    }
    if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
    {
      // Lechers will romance someone that they have a low opinion of
      __result = 0.5f + Mathf.Sqrt(__result);
    }
    if (recipient.story.traits.HasTrait(TraitDefOf.Psychopath))
    {
      // Psychopaths have lower opinion standards
      __result = 0.3f + 0.7f * __result;
    }
  }

}

// Replaces vanilla's "recipient already has a partner" factor: Polygamous recipients don't care; otherwise lover 0.6,
// fiance 0.1, spouse 0.3, times how unhappy they are with that partner (Romantic pawns are loyal to a partner they like
// but leave a disliked one faster) and how little that partner still attracts them.
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "PartnerFactor")]
public static class InteractionWorker_RomanceAttempt_PartnerFactor_Patches
{
  public static Pawn pawn;

  [HarmonyPrefix]
  public static bool PartnerFactorPrefix(Pawn initiator, Pawn recipient, ref float __result)
  {
    if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
    {
      // No psyche on either side: vanilla's partner factor.
      return true;
    }

    if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
    {
      __result = 1f;
      return false;
    }

    float recipientRomantic = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic);

    float existingLovePartnerFactor = 1f;
    if (!new HistoryEvent(recipient.GetHistoryEventForLoveRelationCountPlusOne(), recipient.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
    {
      Pawn pawn = null;
      if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (Pawn x) => !x.Dead) != null)
      {
        pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
        existingLovePartnerFactor = 0.6f;
      }
      else if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (Pawn x) => !x.Dead) != null)
      {
        pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
        existingLovePartnerFactor = 0.1f;
      }
      else if (recipient.GetSpouseCount(includeDead: false) > 0)
      {
        pawn = recipient.GetMostLikedSpouseRelation().otherPawn;
        existingLovePartnerFactor = 0.3f;
      }
      if (pawn != null)
      {
        float opinionOfPartner = (float)recipient.relations.OpinionOf(pawn);
        float romDisp = 2f * recipientRomantic - 1f;
        // More romantic pawns stay loyal for longer
        float opinionOfPartnerMult = romDisp > 0 ? Mathf.InverseLerp(100f - 75f * romDisp, 0, opinionOfPartner) : Mathf.InverseLerp(100f, 75f * romDisp, opinionOfPartner);
        float romChanceMult = Mathf.Clamp01(1f - recipient.relations.SecondaryRomanceChanceFactor(pawn));
        // Modified the formula so that opinion still matters even with maximum romance chance factor
        // Being romantically compatible will keep pawns more faithful, but this weakens with lower opinion
        existingLovePartnerFactor *= 0.5f * (opinionOfPartnerMult + romChanceMult);
      }
      // Lechers won't care as much about their partners and are more promiscuous in general
      if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
      {
        existingLovePartnerFactor = 0.5f + Mathf.Sqrt(existingLovePartnerFactor);
      }

    }
    __result = existingLovePartnerFactor;
    return false;
  }
}

// A romance attempt: the initiator learns the recipient's Kinsey rating, then RomanceAttemptRouter decides what it is.
// Date night, courtship, hookup and cheating attempts are carried out by Psychology instead of vanilla's lovers roll;
// direct ones (after a good date, player-ordered, or no other intent) run vanilla. Either way the log entry gets a
// sentence naming the type before the result, and "Print debug info" logs the attempt with its calculations.
// After vanilla's roll: on success old break-up thoughts between them are cleared; a rejected Lecher gets a special
// (mild) rebuffed thought.
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.Interacted))]
public static class InteractionWorker_RomanceAttempt_Interacted_Patches
{
  [HarmonyPriority(Priority.High)]
  [HarmonyPrefix]
  public static bool LearnSexuality(Pawn initiator, Pawn recipient)
  {
    if (PsycheHelper.PsychologyEnabled(initiator) && PsycheHelper.PsychologyEnabled(recipient) && PsychologySettings.enableKinsey)
    {
      PsycheHelper.Comp(initiator).Sexuality.LearnSexuality(recipient);
    }
    return true;
  }

  // __state: the start of the debug line for a direct attempt, finished by LogDirectAttempt once vanilla has rolled.
  [HarmonyPrefix]
  public static bool RouteAttempt(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, ref string letterText, ref string letterLabel, ref LetterDef letterDef, ref LookTargets lookTargets, out string __state)
  {
    __state = null;
    RomanceAttemptType type = RomanceAttemptRouter.GetAttemptType(initiator, recipient, out _, out string reason);
    extraSentencePacks.Add(RomanceAttemptRouter.TypeSentence(type));
    if (type == RomanceAttemptType.Direct)
    {
      if (PsychologyDebug.Enabled)
      {
        __state = $"{initiator} attempted to romance {recipient}: {type} ({reason}), ";
        __state += $"success chance {InteractionWorker_RomanceAttempt.SuccessChance(initiator, recipient):P0}";
      }
      return true;
    }
    letterText = null;
    letterLabel = null;
    letterDef = null;
    lookTargets = null;
    string result = RomanceAttemptRouter.Resolve(type, initiator, recipient, extraSentencePacks);
    if (PsychologyDebug.Enabled)
    {
      PsychologyDebug.Log($"{initiator} attempted to romance {recipient}: {type} ({reason}), {result}{AttractionBothWays(initiator, recipient)}");
    }
    return false;
  }

  // For the debug log: each pawn's attraction to the other, with its parts.
  private static string AttractionBothWays(Pawn initiator, Pawn recipient)
  {
    return $"; attraction {initiator.LabelShort} -> {recipient.LabelShort} {RomanceUtility.AttractionBreakdown(initiator, recipient)}, "
      + $"{recipient.LabelShort} -> {initiator.LabelShort} {RomanceUtility.AttractionBreakdown(recipient, initiator)}";
  }

  [HarmonyPostfix]
  public static void LogDirectAttempt(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, string __state)
  {
    if (__state != null)
    {
      bool accepted = extraSentencePacks.Contains(RulePackDefOf.Sentence_RomanceAttemptAccepted);
      PsychologyDebug.Log($"{__state} -> {(accepted ? "now lovers" : "rejected")}{AttractionBothWays(initiator, recipient)}");
    }
  }

  [HarmonyPostfix]
  public static void HandleNewThoughts(InteractionWorker_RomanceAttempt __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, string letterText, string letterLabel, LetterDef letterDef)
  {
    if (extraSentencePacks.Contains(RulePackDefOf.Sentence_RomanceAttemptAccepted))
    {
      foreach (ThoughtDef d in (from tgt in initiator.needs.mood.thoughts.memories.Memories
                                where tgt.def.defName.Contains("BrokeUpWithMe")
                                select tgt.def))
      {
        initiator.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(d, recipient);
      }
      foreach (ThoughtDef d in (from tgt in recipient.needs.mood.thoughts.memories.Memories
                                where tgt.def.defName.Contains("BrokeUpWithMe")
                                select tgt.def))
      {
        recipient.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(d, initiator);
      }
      initiator.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, recipient);
      recipient.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, initiator);
      return;
    }
    if (extraSentencePacks.Contains(RulePackDefOf.Sentence_RomanceAttemptRejected))
    {
      if (initiator.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
      {
        initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyRomanceAttemptLecher, recipient);
      }
      return;
    }
  }

}

// A Highmate's psychic bond waits for commitment: vanilla bonds as soon as a romance attempt makes them lovers, or at
// their first lovin; here only spouses bond, or lovers/fiances when either is Codependent. Married bonds them at the
// wedding (MarriageCeremonyUtility_MarriedPatch). CanCreatePsychicBondBetween is left alone, so a Highmate's romance
// attempts still succeed more easily (vanilla's sure success, then our SuccessChance postfix).
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.TryCreatePsychicBondBetween))]
public static class InteractionWorker_RomanceAttempt_TryCreatePsychicBondPatch
{
  [HarmonyPrefix]
  public static bool CommittedEnoughToBond(Pawn initiator, Pawn recipient, ref bool __result)
  {
    if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient)
      || (LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient)
        && (initiator.story?.traits?.HasTrait(TraitDefOfPsychology.Codependent) == true || recipient.story?.traits?.HasTrait(TraitDefOfPsychology.Codependent) == true)))
    {
      return true;
    }
    PsychologyDebug.Log($"Psychic bond between {initiator} and {recipient} waits: not married (and neither is Codependent)");
    __result = false;
    return false;
  }
}

// Polygamous pawns who start a new relationship don't break up with their existing lovers/fiances, and a Polygamous
// ex-partner isn't dumped either (the transpiler nulls them out of vanilla's loop). Methods are recognised by name.
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "BreakLoverAndFianceRelations")]
public static class InteractionWorker_RomanceAttempt_BreakLoverAndFianceRelationsPatch
{

  public static bool Prefix(Pawn pawn, ref List<Pawn> oldLoversAndFiances)
  {
    oldLoversAndFiances = new List<Pawn>();
    return pawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous) != true;
  }

  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
  {
    List<CodeInstruction> cList = codes.ToList();
    for (int i = 0; i < cList.Count(); i++)
    {
      if (cList[i].opcode == OpCodes.Brfalse_S)
      {
        yield return CodeInstruction.Call(typeof(InteractionWorker_RomanceAttempt_BreakLoverAndFianceRelationsPatch), nameof(MakeNullIfPolygamous));
      }
      yield return cList[i];
    }
  }

  public static Pawn MakeNullIfPolygamous(Pawn pawn)
  {
    if (pawn == null)
    {
      return null;
    }
    if (pawn.story?.traits?.HasTrait(TraitDefOfPsychology.Polygamous) == true)
    {
      return null;
    }
    return pawn;
  }
}

// Cheating and break-up thoughts go through RomanceHelperMethods so their strength depends on personality and opinion.
[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "TryAddCheaterThought")]
public static class InteractionWorker_RomanceAttempt_TryAddCheaterThoughtPatch
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
  {
    return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
  }
}

[HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "RemoveBrokeUpAndFailedRomanceThoughts")]
public static class InteractionWorker_RomanceAttempt_RemoveBrokeUpAndFailedRomanceThoughtsPatch
{
  [HarmonyTranspiler]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
  {
    return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
  }

}
