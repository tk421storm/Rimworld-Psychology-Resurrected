using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;
using System.Diagnostics;

namespace Psychology;

/// <summary>
/// Psychology's "Conversation" social interaction (InteractionDef in Interactions_Psychology.xml). It replaces vanilla
/// deep talk between psyche pawns (InteractionWorker_DeepTalk patch); vanilla chitchat stays, relabelled "smalltalk"
/// by Common/Patches/InteractionPatches.xml.
///
/// Starting one does not immediately change opinions: Interacted() picks a topic and gives BOTH pawns a
/// Hediff_Conversation. The talk then lasts until the hediff ends (pawns walk apart, get interrupted, or the random
/// end check fires), and only then is the opinion change worked out (Hediff_Conversation.PostRemoved).
/// </summary>
public class InteractionWorker_Conversation : InteractionWorker
{

    /// <summary>
    /// 0.35 x a personality multiplier. The multiplier comes from 4 x the sum of: initiator Extroverted - 0.5, initiator
    /// Outspoken - 0.5, recipient Friendly - 0.5, +0.5 if the initiator is Chatty and +0.5 if both are on the same
    /// hang-out/date. A positive sum s gives 1 + s, a negative one 1 / (1 - s), so it never drops to zero.
    /// </summary>
    public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
    {
        if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
        {
            return 0f;
        }
        if (!initiator.health.capacities.CapableOf(PawnCapacityDefOf.Talking) || !recipient.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
        {
            return 0f;
        }
        float chanceFactor = 0.0f;
        Lord lord = LordUtility.GetLord(initiator);
        if (lord != null && (lord.LordJob is LordJob_HangOut || lord.LordJob is LordJob_Date) && LordUtility.GetLord(recipient) == lord)
        {
            chanceFactor += 0.5f;
        }
        if (initiator.story.traits.HasTrait(TraitDefOfPsychology.Chatty))
        {
            chanceFactor += 0.5f;
        }
        chanceFactor += -0.5f + PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Extroverted);
        chanceFactor += -0.5f + PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Outspoken);
        chanceFactor += -0.5f + PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Friendly);

        chanceFactor *= 4f;
        float multiplicativeFactor = chanceFactor > 0 ? 1f + chanceFactor : 1f / (1f - chanceFactor);
        return 0.35f * multiplicativeFactor;
    }

    // Topic: one of the initiator's nodes, weighted by Pawn_PsycheTracker.GetConversationTopicWeight (controversial topics
    // are avoided with people they don't know well), then a random phrase from that node's conversationTopics.
    // The initiator's hediff has waveGoodbye = true: it owns the log entry and the goodbye bubble.
    public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
    {
        // The base call clears the outs and lets other mods' hooks on InteractionWorker.Interacted see this interaction
        // (Intimacy only raises its need there).
        base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        PersonalityNode topic = (from node in PsycheHelper.Comp(initiator).Psyche.PersonalityNodes
                                 where node.HasConvoTopics
                                 select node).RandomElementByWeight(node => PsycheHelper.Comp(initiator).Psyche.GetConversationTopicWeight(node.def, recipient));
        string convoTopic = topic.def.conversationTopics.RandomElement();
        Hediff_Conversation initiatorHediff = (Hediff_Conversation)HediffMaker.MakeHediff(HediffDefOfPsychology.HoldingConversation, initiator);
        initiatorHediff.otherPawn = recipient;
        initiatorHediff.topic = topic.def;
        initiatorHediff.waveGoodbye = true;
        initiatorHediff.convoTopic = convoTopic;
        initiator.health.AddHediff(initiatorHediff);
        Hediff_Conversation recipientHediff = (Hediff_Conversation)HediffMaker.MakeHediff(HediffDefOfPsychology.HoldingConversation, recipient);
        recipientHediff.otherPawn = initiator;
        recipientHediff.topic = topic.def;
        recipientHediff.waveGoodbye = false;
        recipientHediff.convoTopic = convoTopic;
        recipient.health.AddHediff(recipientHediff);
    }

}
