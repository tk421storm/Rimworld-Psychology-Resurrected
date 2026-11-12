using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using Verse.Grammar;
using System.Reflection;
using HarmonyLib;

namespace Psychology;

/// <summary>
/// "Holding conversation" hediff. Each participant gets one pointing at the other. While it exists the pawns are in a
/// conversation; when either copy is removed, the other is removed too and the outcome is applied:
///   - an opinion memory (a runtime Thought_MemorySocialDynamic) whose size depends on how much the two agree on the
///     topic, its controversiality, both personalities, how long they talked, and a seeded random factor
///   - possibly a social fight, if the outcome was negative and the initiator is Aggressive
///   - a play-log entry describing the talk (short/normal/long/epic phrasing from the Keyed Conversations strings)
///   - with Intimacy - Friends n' Lovers loaded, extra social intimacy for both pawns that grows with the length
/// </summary>
public class Hediff_Conversation : HediffWithComps
{
  public Pawn otherPawn;
  public PersonalityNodeDef topic;
  public string convoTopic;
  // True only on the initiator's copy: that copy writes the log entry and shows the goodbye bubble.
  public bool waveGoodbye;
  public bool startedFight = false;
  public PlayLogEntry_InteractionConversation convoLog;

  public override void PostMake()
  {
    base.PostMake();
    if (!PsycheHelper.PsychologyEnabled(this.pawn))
    {
      this.pawn.health.RemoveHediff(this);
    }
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_References.Look(ref this.otherPawn, "otherPawn");
    Scribe_Defs.Look(ref this.topic, "topic");
    Scribe_Values.Look(ref this.waveGoodbye, "waveGoodbye");
    Scribe_Values.Look(ref this.convoTopic, "convoTopic", "something");
  }

  // Every 200 ticks: end the conversation if the partner is gone, can't talk, is downed/dead/berserk, or the two are more
  // than ~7 cells apart or out of line of sight; otherwise end it at random with a mean time that shrinks as it goes on
  // (3 h -> 2 h -> 1 h -> 0.5 h; doubled for each Chatty participant).
  public override void Tick()
  {
    base.Tick();
    if (this.pawn.IsHashIntervalTick(200))
    {
      if (this.otherPawn == null)
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      if (!this.otherPawn.Spawned || !this.pawn.Spawned || !SocialInteractionUtility.CanReceiveInteraction(this.pawn) || !SocialInteractionUtility.CanReceiveInteraction(this.otherPawn))
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      if (this.otherPawn.Dead || this.otherPawn.Downed || this.otherPawn.InAggroMentalState)
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      if (this.pawn.Map != null && this.otherPawn.Map != null && ((this.pawn.Position - this.otherPawn.Position).LengthHorizontalSquared >= 54f || !GenSight.LineOfSight(this.pawn.Position, this.otherPawn.Position, this.pawn.Map, true)))
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      if ((this.pawn.Position - this.otherPawn.Position).LengthHorizontalSquared >= 54f || !GenSight.LineOfSight(this.pawn.Position, this.otherPawn.Position, this.pawn.Map, true))
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      /* When a conversation first starts, the mean time for it to last is 3 hours.
       * When it reaches half an hour, the mean time for it to continue is 2 hours.
       * When it reaches an hour, the mean time for it to continue is 1 hour.
       * When it surpasses 2 hours, it will on average last for half an hour more.
       * Conversations will thus usually not surpass 2 hours, and very rarely surpass 2 and a half hours, but are very likely to last up to an hour.
       */
      //float mtb = Mathf.Max(0f, 3f - this.ageTicks / GenDate.TicksPerHour);
      float mtb = 3f * PsychologySettings.convoMeanHours / PsychologySettings.convoMeanHoursDefault;

      if (this.ageTicks > 2f * GenDate.TicksPerHour)
      {
        mtb = 0.5f;
      }
      else if (this.ageTicks > GenDate.TicksPerHour)
      {
        mtb = 1f;
      }
      else if (this.ageTicks > 0.5f * GenDate.TicksPerHour)
      {
        mtb = 2f;
      }
      if (this.pawn.story.traits.HasTrait(TraitDefOfPsychology.Chatty))
      {
        mtb *= 2f;
      }
      if (this.otherPawn.story.traits.HasTrait(TraitDefOfPsychology.Chatty))
      {
        mtb *= 2f;
      }
      if (Rand.MTBEventOccurs(mtb, GenDate.TicksPerHour, 200))
      {
        this.pawn.health.RemoveHediff(this);
        return;
      }
      else if (Rand.Value < 0.2f && this.pawn.Map != null)
      {
        MoteMaker.MakeInteractionBubble(this.pawn, otherPawn, InteractionDefOfPsychology.EndConversation.interactionMote, InteractionDefOfPsychology.EndConversation.GetSymbol()); // TEST in 1.3
      }
    }
  }

  // Conversation over: remove the partner's copy, pick the talk description by length, create and apply the opinion
  // thought, maybe start a social fight, and (initiator side only) write the play-log entry.
  public override void PostRemoved()
  {

    base.PostRemoved();
    if (this.pawn == null && this.otherPawn == null)
    {
      Log.Warning("Hediff_Conversation.PostRemoved(), pawns were null");
      return;
    }
    if (this.pawn.Dead || this.otherPawn.Dead || !PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.PsychologyEnabled(otherPawn))
    {
      Log.Warning("Hediff_Conversation.PostRemoved(), psychology not enabled or pawn dead");
      return;
    }
    Hediff_Conversation otherConvo = otherPawn.health.hediffSet.hediffs.Find(h => h is Hediff_Conversation && ((Hediff_Conversation)h).otherPawn == this.pawn) as Hediff_Conversation;
    if (otherConvo != null)
    {
      this.otherPawn.health.RemoveHediff(otherConvo);
      this.startedFight = otherConvo.startedFight;
    }
    string talkDesc;
    if (this.ageTicks < 500)
    {
      int numShortTalks = int.Parse("NumberOfShortTalks".Translate());
      talkDesc = "ShortTalk" + Rand.RangeInclusive(1, numShortTalks);
    }
    else if (this.ageTicks < GenDate.TicksPerHour / 2)
    {
      int numNormalTalks = int.Parse("NumberOfNormalTalks".Translate());
      talkDesc = "NormalTalk" + Rand.RangeInclusive(1, numNormalTalks);
    }
    else if (this.ageTicks < GenDate.TicksPerHour * 2.5)
    {
      int numLongTalks = int.Parse("NumberOfLongTalks".Translate());
      talkDesc = "LongTalk" + Rand.RangeInclusive(1, numLongTalks);
    }
    else
    {
      int numEpicTalks = int.Parse("NumberOfEpicTalks".Translate());
      talkDesc = "EpicTalk" + Rand.RangeInclusive(1, numEpicTalks);
    }
    float opinionMod;
    ThoughtDef def = CreateSocialThought(out opinionMod);
    bool mattered = TryGainThought(def, Mathf.RoundToInt(opinionMod));
    if (this.waveGoodbye)
    {
      GiveIntimacyForLength();
    }
    InteractionDef endConversation = new InteractionDef();
    // The released 1.4-1.6 builds use 0.1 * ... / 2 (i.e. a quarter of the 0.2 base that was in source).
    endConversation.socialFightBaseChance = 0.1f * PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive) * PopulationModifier / 2f * Mathf.InverseLerp(0f, -80f, opinionMod);
    endConversation.defName = "EndConversation";
    endConversation.label = def.label;
    List<RulePackDef> socialFightPacks = new List<RulePackDef>();
    // A conversation also ends when either pawn despawns (e.g. leaves the map), and despawning clears pawn.interactions,
    // so only roll for a fight while both are still spawned.
    bool canFight = this.pawn.Spawned && this.otherPawn.Spawned && this.pawn.interactions != null;
    if (otherConvo != null && (startedFight || (mattered && canFight && this.pawn.interactions.CheckSocialFightStart(endConversation, otherPawn))))
    {
      if (startedFight)
      {
        socialFightPacks.Add(RulePackDefOfPsychology.Sentence_SocialFightConvoRecipientStarted);
      }
      else
      {
        socialFightPacks.Add(RulePackDefOfPsychology.Sentence_SocialFightConvoInitiatorStarted);
      }
      this.startedFight = true;
      if (!this.waveGoodbye && otherConvo.convoLog != null && !otherConvo.startedFight)
      {
        //The main conversation hediff was the other conversation, and didn't start a fight, so we have to add the extra sentence in after the fact.
        Traverse.Create(otherConvo.convoLog).Field("extraSentencePacks").GetValue<List<RulePackDef>>().AddRange(socialFightPacks);
      }
    }
    if (this.waveGoodbye && this.pawn.Map != null)
    {
      RulePack goodbyeText = new RulePack();
      FieldInfo RuleStrings = typeof(RulePack).GetField("rulesStrings", BindingFlags.Instance | BindingFlags.NonPublic);
      List<string> text = new List<string>(1);
      text.Add("r_logentry->" + talkDesc.Translate(convoTopic, pawn.Named("INITIATOR"), otherPawn.Named("RECIPIENT")));
      RuleStrings.SetValue(goodbyeText, text);
      endConversation.logRulesInitiator = goodbyeText;
      FieldInfo Symbol = typeof(InteractionDef).GetField("symbol", BindingFlags.Instance | BindingFlags.NonPublic);
      Symbol.SetValue(endConversation, Symbol.GetValue(InteractionDefOfPsychology.EndConversation));
      PlayLogEntry_InteractionConversation log = new PlayLogEntry_InteractionConversation(endConversation, pawn, this.otherPawn, socialFightPacks);
      Find.PlayLog.Add(log);
      convoLog = log;
      MoteMaker.MakeInteractionBubble(this.pawn, this.otherPawn, InteractionDefOf.Chitchat.interactionMote, InteractionDefOf.Chitchat.GetSymbol()); // 1.3
    }
  }

  /// <summary>
  /// Builds a one-off ThoughtDef holding the opinion change from this conversation. In outline:
  ///   1. Agreement in [-1, 1] = SaddleShapeFunction(my rating, their rating) on the topic node. Close ratings agree, opposite
  ///      ratings disagree; the more controversial the topic, the faster agreement turns into disagreement.
  ///   2. A personality multiplier from Judgmental, Passionate (other), and depending on the sign Friendly/Trusting/Cool
  ///      (positive) or Friendly/Aggressive/Polite (negative). Codependent pawns are extra sensitive to their lover.
  ///   3. The size grows with |agreement| x multiplier x controversiality, the conversation length (relative to the
  ///      "conversation time scale" setting) and a seeded random term, saturating at "max opinion change".
  ///   4. Negative results are scaled down in colonies with fewer than 8 colonists (PopulationModifier).
  /// </summary>
  private ThoughtDef CreateSocialThought(out float opinionMod)
  {
    //We create a dynamic def to hold this thought so that the game won't worry about it being used anywhere else.
    ThoughtDef def = new ThoughtDef();
    def.defName = this.pawn.GetHashCode() + "Conversation" + topic.defName;
    def.label = topic.defName;
    def.durationDays = PsychologySettings.conversationDuration;
    def.nullifyingTraits = new List<TraitDef>();
    def.nullifyingTraits.Add(TraitDefOf.Psychopath);
    def.thoughtClass = typeof(Thought_MemorySocialDynamic);
    ThoughtStage stage = new ThoughtStage();

    // NEW PSYCHOLOGY FORMULA
    float opin1 = Mathf.Clamp01(PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(topic));
    float opin2 = Mathf.Clamp01(PsycheHelper.Comp(otherPawn).Psyche.GetPersonalityRating(topic));

    float controversiality = topic.controversiality;
    // Baseline opinion modifier ranges from -1 to +1. It's expected value should be positive for low controversiality (less than 1), and negative otherwise
    float opinionModRaw = PsycheHelper.SaddleShapeFunction(opin1, opin2, 1f - 0.5f * controversiality, 4f * controversiality * controversiality);

    Pawn_PsycheTracker pawnPT = PsycheHelper.Comp(pawn).Psyche;
    float pawnJudgmental = -0.5f + pawnPT.GetPersonalityRating(PersonalityNodeDefOf.Judgmental);
    float pawnFriendly = -0.5f + pawnPT.GetPersonalityRating(PersonalityNodeDefOf.Friendly);
    float pawnTrusting = -0.5f + pawnPT.GetPersonalityRating(PersonalityNodeDefOf.Trusting);
    float pawnAggressive = -0.5f + pawnPT.GetPersonalityRating(PersonalityNodeDefOf.Aggressive);

    Pawn_PsycheTracker otherPawnPT = PsycheHelper.Comp(otherPawn).Psyche;
    float otherPawnPassionate = -0.5f + otherPawnPT.GetPersonalityRating(PersonalityNodeDefOf.Passionate);
    float otherPawnFriendly = -0.5f + otherPawnPT.GetPersonalityRating(PersonalityNodeDefOf.Friendly);
    float otherPawnCool = -0.5f + otherPawnPT.GetPersonalityRating(PersonalityNodeDefOf.Cool);
    float otherPawnPolite = -0.5f + otherPawnPT.GetPersonalityRating(PersonalityNodeDefOf.Polite);

    // Initialize personalityModifier, which can be positive or negative. Represents % increase or decrease in opinionMod
    float personalityModifier = 0f;
    // All opinions are enhanced by how judgmental the pawn is
    personalityModifier += pawnJudgmental;
    // All conversations are affected by how passionate the other pawn is
    personalityModifier += 0.5f * otherPawnPassionate;

    bool opinionModRawPositive = opinionModRaw > 0f;
    // Positive opinions
    if (opinionModRawPositive)
    {
      // Positive opinions are enhanced by how Friendly the pawn is
      personalityModifier += pawnFriendly;
      // Positive opinions are enhanced by how Trusting the pawn is
      personalityModifier += pawnTrusting;
      // Positive opinions are enhanced by how Freindly the other pawn is
      personalityModifier += otherPawnFriendly;
      // Positive opinions are enhanced by how Cool the other pawn is
      personalityModifier += otherPawnCool;
      if (LovePartnerRelationUtility.LovePartnerRelationExists(this.pawn, this.otherPawn) && this.pawn.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
      {
        //If it's a positive thought about their lover, Codependent pawns are always more affected by it.
        personalityModifier += 0.5f;
      }
    }
    // Negative opinions
    else
    {
      // Negative opinions are damped by how Friendly the pawn is
      personalityModifier -= pawnFriendly;
      // Negative opinions are enhanced by how Aggressive the pawn is
      personalityModifier += pawnAggressive;
      // Negative opinions are damped by how Friendly the other pawn is
      personalityModifier -= otherPawnFriendly;
      // Negative opinions are damped by how Polite the other pawn is
      personalityModifier -= otherPawnPolite;
    }

    // Controls how much personality affects the opinion. Should be a positive number.
    personalityModifier *= 0.5f * PsychologySettings.convoPersonalityEffectMultiplier;

    // Multiply by 1 + personalityModifier for positive personalityModifier
    // Divide by 1 - personalityModifier for negative personalityModifier. This ensures opinionMod will always remain positive.
    // For example, if personalityModifier = -2, i.e. a 200% decrease, opinionMod will be 1/3 of its original value. Note that a 200% increase of 1/3 is 1.
    float personalityMultiplier = personalityModifier > 0f ? 1f + personalityModifier : 1f / (1f - personalityModifier);

    float x = personalityMultiplier * (0.25f + controversiality * controversiality) * Mathf.Abs(opinionModRaw);
    float t = ((float)ageTicks) / ((float)GenDate.TicksPerHour);
    float r = Rand.ValueSeeded(PsycheHelper.PawnSeed(pawn) + PsycheHelper.PawnSeed(otherPawn) + topic.GetHashCode());

    t /= PsychologySettings.convoTimeScaleHours;

    float yr = 5f;
    float y0x = 10f;
    float y0t = 5f;
    float y1 = 10f;
    float y2 = 10f;

    // Added time minimum to baseline to opinionMod because conversations often end very quickly
    float y = yr * r + y0x * Func(x) + y0t * Func(t) + y1 * Func(x) * Func(t) + y2 * Func(x * (1f + t));
    y *= PsychologySettings.convoMaxOpinionChange / (yr + y0x + y0t + y1 + y2);

    // In low-population colonies, pawns will put aside their differences.
    opinionMod = opinionModRawPositive ? Mathf.Ceil(y) : -Mathf.Ceil(y * PopulationModifier);

    stage.label = "ConversationStage".Translate() + " " + convoTopic;
    stage.baseOpinionOffset = opinionMod;
    def.stages.Add(stage);
    return def;
  }

  // Saturating curve 1 - e^-x: 0 at 0, approaches 1 for large x.
  public static float Func(float x)
  {
    return 1f - Mathf.Exp(-x);
  }

  // Most intimacy a conversation can add when it ends, on top of Intimacy's flat +1% when it starts (same as nuzzling).
  public const float MaxIntimacyForLength = 0.04f;

  // Intimacy - Friends n' Lovers only counts the Conversation interaction when it starts, so on its own a long talk is
  // worth no more than a short one. When the talk ends (initiator's copy only, so it's given once), both pawns get up to
  // MaxIntimacyForLength more, on the same length curve as the opinion: with the default 1 h time scale, a short talk
  // adds under 1%, half an hour about 1.6%, an epic 2.5 h talk about 3.7%.
  private void GiveIntimacyForLength()
  {
    if (!IntimacyCompat.Active)
    {
      return;
    }
    float t = ((float)ageTicks) / ((float)GenDate.TicksPerHour) / PsychologySettings.convoTimeScaleHours;
    float amount = MaxIntimacyForLength * Func(t);
    IntimacyCompat.GainSocialIntimacy(this.pawn, amount);
    IntimacyCompat.GainSocialIntimacy(this.otherPawn, amount);
    PsychologyDebug.Log($"{this.pawn} and {this.otherPawn} talked for {ageTicks / (float)GenDate.TicksPerHour:0.00} h: +{amount:P1} intimacy each");
  }

  // Keeps at most ~10 conversation memories per pair: once full, a new one is only added if it's stronger than the
  // strongest of the overflow, and the overflow memories are expired; the 9 strongest are kept.
  private bool TryGainThought(ThoughtDef def, int opinionOffset)
  {
    ThoughtStage stage = def.stages.First();
    IEnumerable<Thought_MemorySocialDynamic> convoMemories;
    /* The more they know about someone, the less likely small thoughts are to have an impact on their opinion.
     * This helps declutter the Social card without preventing pawns from having conversations.
     * They just won't change their mind about the colonist as a result.
     */

    float totalThoughtOpinion = PsycheHelper.Comp(pawn).Psyche.TotalThoughtOpinion(this.otherPawn, out convoMemories);
    int maxConvoOpinions = 10;

    if (convoMemories.EnumerableCount() < maxConvoOpinions)
    {
      this.pawn.needs.mood.thoughts.memories.TryGainMemory(def, this.otherPawn);
      return true;
    }
    List<Thought_MemorySocialDynamic> removedMemories = convoMemories.OrderByDescending(m => Mathf.Abs(m.OpinionOffset())).Skip(maxConvoOpinions - 1).ToList();

    Thought_MemorySocialDynamic lastMemory = removedMemories.MaxBy(m => Mathf.Abs(m.OpinionOffset()));
    if (Mathf.Abs(opinionOffset) < Mathf.Abs(lastMemory.OpinionOffset()))
    {
      return false;
    }
    this.pawn.needs.mood.thoughts.memories.TryGainMemory(def, this.otherPawn);

    foreach (Thought_MemorySocialDynamic m in removedMemories)
    {
      // Remove these memories by making them old
      m.age = m.DurationTicks + 300;
    }
    return true;
  }

  // 0..1: in small colonies (under 8 free colonists) pawns put aside their differences, so negative outcomes and fight
  // chances are scaled down.
  public float PopulationModifier
  {
    get
    {
      if (this.pawn.IsColonist && this.pawn.Map != null)
      {
        return Mathf.Clamp01(this.pawn.Map.mapPawns.FreeColonistsCount / 8f);
      }
      return 1f;
    }
  }

}
