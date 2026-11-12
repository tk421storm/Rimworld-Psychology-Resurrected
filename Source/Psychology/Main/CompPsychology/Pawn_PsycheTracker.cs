using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using UnityEngine;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Psychology;

/// <summary>
/// A pawn's personality ("psyche"): one PersonalityNode per PersonalityNodeDef (37 in vanilla Psychology, e.g.
/// Aggressive, Romantic, Polite), each with a rating in [0, 1] where 0.5 is average.
///
/// Every node stores two ratings:
///   - rawRating:      the pawn's innate, independent roll. Uniform on [0, 1], seeded from the pawn's ID and the world
///                     seed (so the same pawn always rolls the same personality), and the only thing that is saved.
///                     Editing a psyche (Dialog_EditPsyche) or indoctrination (BoostRatingsTowardsIdeo) changes these.
///   - AdjustedRating: what the rest of the mod actually uses. Derived from all raw ratings by
///                     CalculateAdjustedRatings(), which layers in the pawn's circumstances (gender, traits, skills,
///                     incapable work types, beauty, anxiety) and the correlations between nodes. Never saved; it is
///                     recalculated on demand, so changing a trait or skill shifts the personality automatically.
///
/// The tracker also caches conversation-related opinions and, with Ideology, the per-day certainty change each meme
/// and precept of the pawn's ideoligion causes through their personality.
/// </summary>
public class Pawn_PsycheTracker : IExposable
{
  // Saved but no longer read anywhere; kept for save compatibility.
  public int upbringing = 0;
  // When this pawn's last planned date is (absolute ticks, set by RendezvousUtility.PlanDate), with anyone and with each
  // partner (keyed by their thingIDNumber); InteractionWorker_DateBase uses them for the date cooldowns. Entries over
  // a month old are dropped on save.
  public int lastDateTick = 0;
  private Dictionary<int, int> lastDateTickWith = new Dictionary<int, int>();
  private Pawn pawn;
  private HashSet<PersonalityNode> nodes;

  public Dictionary<PersonalityNodeDef, PersonalityNode> nodeDict = new Dictionary<PersonalityNodeDef, PersonalityNode>();
  // Sum of opinion offsets from all conversation memories about another pawn, keyed by that pawn's ThingID.
  // The bool dictionary is the matching "dirty" flag, set by Thought_MemorySocialDynamic when a memory changes.
  private Dictionary<string, float> cachedOpinions = new Dictionary<string, float>();
  private Dictionary<string, bool> recalcCachedOpinions = new Dictionary<string, bool>();

  // Per (other pawn ThingID, topic defName): how much a Polite pawn avoids a topic they've already argued about.
  public Dictionary<Pair<string, string>, float> cachedDisagreementWeights = new Dictionary<Pair<string, string>, float>();
  public Dictionary<Pair<string, string>, bool> recalcNodeDisagreement = new Dictionary<Pair<string, string>, bool>();
  // Adjusted-rating cache counter. It counts READS, not ticks: CalculateAdjustedRatings() sets it to 500 and every
  // GetPersonalityRating() call decrements it, recomputing once it goes negative. Set to -1 to force a recompute.
  public int AdjustedRatingTicker = -1;
  // Breakdown of the daily certainty change, for the social-card tooltip: meme/precept -> node -> certainty per day.
  public Dictionary<MemeDef, Dictionary<PersonalityNodeDef, float>> dailyCertaintyFromMemesAndNodes = new Dictionary<MemeDef, Dictionary<PersonalityNodeDef, float>>();
  public Dictionary<PreceptDef, Dictionary<PersonalityNodeDef, float>> dailyCertaintyFromPerceptsAndNodes = new Dictionary<PreceptDef, Dictionary<PersonalityNodeDef, float>>();

  public HashSet<PersonalityNode> PersonalityNodes
  {
    get
    {
      return nodes;
    }
    set
    {
      nodes = value;
    }
  }

  public Dictionary<string, bool> OpinionCacheDirty
  {
    get
    {
      return recalcCachedOpinions;
    }
    set
    {
      recalcCachedOpinions = value;
    }
  }

  public Dictionary<Pair<string, string>, bool> DisagreementCacheDirty
  {
    get
    {
      return recalcNodeDisagreement;
    }
    set
    {
      recalcNodeDisagreement = value;
    }
  }

  public Pawn_PsycheTracker(Pawn p)
  {
    this.pawn = p;
  }

  // When this pawn's last planned date with partner is (absolute ticks), or 0 if none is remembered.
  public int LastDateTickWith(Pawn partner)
  {
    return lastDateTickWith.TryGetValue(partner.thingIDNumber, out int tick) ? tick : 0;
  }

  public void SetLastDateTick(Pawn partner, int tick)
  {
    lastDateTick = tick;
    lastDateTickWith[partner.thingIDNumber] = tick;
  }

  public void ExposeData()
  {
    Scribe_Values.Look(ref this.upbringing, "upbringing", 0, false);
    Scribe_Values.Look(ref this.lastDateTick, "lastDateTick", 0, false);
    if (Scribe.mode == LoadSaveMode.Saving)
    {
      int stale = Find.TickManager.TicksAbs - 30 * GenDate.TicksPerDay;
      lastDateTickWith.RemoveAll(e => e.Value < stale);
    }
    Scribe_Collections.Look(ref this.lastDateTickWith, "lastDateTickWith", LookMode.Value, LookMode.Value);
    this.lastDateTickWith ??= new Dictionary<int, int>();
    PsycheHelper.Look(ref this.nodes, "nodes", LookMode.Deep, new object[] { this.pawn });
    foreach (PersonalityNode n in this.nodes)
    {
      nodeDict[n.def] = n;
    }
  }

  /// <summary>Creates one node per PersonalityNodeDef and rolls their raw ratings. inputSeed lets callers re-roll.</summary>
  public void Initialize(int inputSeed = 0)
  {
    this.nodes = new HashSet<PersonalityNode>();
    foreach (PersonalityNodeDef def in PersonalityNodeMatrix.DefList)
    {
      nodes.Add(PersonalityNodeMaker.MakeNode(def, this.pawn));
    }
    RandomizeRatings(inputSeed);
    foreach (PersonalityNode n in this.nodes)
    {
      nodeDict[n.def] = n;
    }
  }

  /// <summary>
  /// Rolls every raw rating independently and uniformly in [0, 1]. The seed combines the pawn's ID, the world seed and
  /// the node, so a given pawn in a given world always gets the same personality.
  /// Correlations between nodes are NOT applied here; they come from CalculateAdjustedRatings().
  /// </summary>
  public void RandomizeRatings(int inputSeed = 0)
  {
    int pawnSeed = PsycheHelper.PawnSeed(this.pawn);
    int defSeed;
    foreach (PersonalityNode node in nodes)
    {
      defSeed = node.def.GetHashCode();
      node.rawRating = Rand.ValueSeeded(Gen.HashCombineInt(pawnSeed, defSeed, inputSeed, 37));
    }
    AdjustedRatingTicker = -1;
  }

  /// <summary>
  /// The pawn's adjusted rating for a node, in [0, 1] (0.5 = average). This is THE accessor the rest of the mod uses
  /// to ask "how Romantic/Aggressive/... is this pawn". Recomputes all adjusted ratings every 500 calls.
  /// </summary>
  public float GetPersonalityRating(PersonalityNodeDef def)
  {
    if (AdjustedRatingTicker < 0)
    {
      CalculateAdjustedRatings();
    }
    AdjustedRatingTicker--;
    return nodeDict[def].AdjustedRating;
  }

  public PersonalityNode GetPersonalityNodeOfDef(PersonalityNodeDef def)
  {
    return nodeDict[def];
  }

  /// <summary>
  /// Relative weight for picking <paramref name="def"/> as the topic of a conversation with <paramref name="otherPawn"/>
  /// (see InteractionWorker_Conversation). Controversial topics (high def.controversiality) are strongly avoided with
  /// strangers: weight = 1 / (32^controversiality + 64 t^2), where t grows with how much the pawns already like each
  /// other from past conversations and with Outspoken minus Polite. Polite pawns additionally avoid topics they have
  /// already disagreed about with this person.
  /// </summary>
  public float GetConversationTopicWeight(PersonalityNodeDef def, Pawn otherPawn)
  {
    /* Pawns will avoid controversial topics until they know someone better.
     * This isn't a perfect system, but the weights will be closer together the higher totalOpinionModifiers is.
     */
    float totalOpinion = this.TotalThoughtOpinion(otherPawn, out IEnumerable<Thought_MemorySocialDynamic> convoMemories);
    float t = Mathf.Max(0f, totalOpinion / 30f + GetPersonalityRating(PersonalityNodeDefOf.Outspoken) - GetPersonalityRating(PersonalityNodeDefOf.Polite));
    float c = def.controversiality;
    float weight = 1f / (Mathf.Pow(32f, c) + 64f * t * t);

    /* Polite pawns will avoid topics they already know are contentious. */
    Pair<string, string> disagreementKey = new Pair<string, string>(otherPawn.ThingID, def.defName);
    if (cachedDisagreementWeights.TryGetValue(disagreementKey, out float cachedWeight) && !recalcNodeDisagreement[disagreementKey])
    {
      weight *= cachedWeight;
    }
    else
    {
      float knownDisagreements = (from m in convoMemories
                                  where m.opinionOffset < 0f && m.CurStage.label == def.defName
                                  select Math.Abs(m.opinionOffset)).Sum();
      float disagree = Mathf.Lerp(1f, 0.25f, 0.1f * knownDisagreements * GetPersonalityRating(PersonalityNodeDefOf.Polite) - 0.25f);
      cachedDisagreementWeights[disagreementKey] = disagree;
      recalcNodeDisagreement[disagreementKey] = false;
      weight *= disagree;
    }
    return weight;
  }

  /// <summary>Sum of the opinion offsets from all of this pawn's conversation memories about <paramref name="other"/> (cached).</summary>
  public float TotalThoughtOpinion(Pawn other, out IEnumerable<Thought_MemorySocialDynamic> convoMemories)
  {
    convoMemories = from m in this.pawn.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySocialDynamic>()
                    where m.def.defName.Contains("Conversation") && m.otherPawn == other
                    select m;
    if (cachedOpinions.ContainsKey(other.ThingID) && !recalcCachedOpinions[other.ThingID])
    {
      return cachedOpinions[other.ThingID];
    }
    float knownThoughtOpinion = 0f;
    convoMemories.Do(m => knownThoughtOpinion += m.opinionOffset);

    cachedOpinions[other.ThingID] = knownThoughtOpinion;
    recalcCachedOpinions[other.ThingID] = false;
    return knownThoughtOpinion;
  }

  /// <summary>
  /// Rebuilds every node's AdjustedRating from the raw ratings. The pipeline is:
  ///   1. Start from the independent, uniform raw ratings.
  ///   2. AdjustForCircumstance(applyTwice: true): first half of the gender/trait/skill/etc. nudges.
  ///   3. Map each rating to a standard normal (probit), multiply the vector by C^(1/2)
  ///      (PersonalityNodeMatrix.parentTransformMatrix), and map back to [0, 1]. This turns independent rolls into
  ///      correlated ones, so e.g. an Aggressive pawn tends to be less Empathetic, following the correlation matrix C
  ///      that PersonalityNodeMatrix builds from the XML.
  ///   4. AdjustForCircumstance again: the second half of the nudges. Splitting it means the circumstances both
  ///      shape and are shaped by the correlations, and applying each half twice composes to the full modifier.
  ///   5. PersonalityNodeMatrix.ApplyBigFiveProjections: optionally pushes the pawn away from the average along the
  ///      Big Five axes ("personality extremeness" setting).
  /// </summary>
  public void CalculateAdjustedRatings()
  {
    Gender gender = pawn.gender;

    List<Trait> traits = pawn.story.traits.allTraits;

    List<SkillRecord> skills = pawn.skills.skills;

    List<WorkTypeDef> incapables = pawn.GetDisabledWorkTypes();

    int index;
    float[] adjustedRatingList = new float[PersonalityNodeMatrix.order];
    foreach (PersonalityNode node in this.nodes)
    {
      adjustedRatingList[PersonalityNodeMatrix.indexDict[node.def]] = node.rawRating;
    }
    AdjustForCircumstance(adjustedRatingList, true, gender, traits, skills, incapables);
    for (index = 0; index < PersonalityNodeMatrix.order; index++)
    {
      adjustedRatingList[index] = PsycheHelper.NormalCDFInv(adjustedRatingList[index]);
    }
    adjustedRatingList = PersonalityNodeMatrix.MatrixVectorProduct(PersonalityNodeMatrix.parentTransformMatrix, adjustedRatingList);
    for (index = 0; index < PersonalityNodeMatrix.order; index++)
    {
      adjustedRatingList[index] = PsycheHelper.NormalCDF(adjustedRatingList[index]);
    }
    AdjustForCircumstance(adjustedRatingList, true, gender, traits, skills, incapables);
    PersonalityNodeMatrix.ApplyBigFiveProjections(adjustedRatingList);
    foreach (PersonalityNode node in this.nodes)
    {
      node.AdjustedRating = adjustedRatingList[PersonalityNodeMatrix.indexDict[node.def]];
    }
    AdjustedRatingTicker = 500;
  }

  /// <summary>
  /// Nudges each rating (in place) according to the pawn's circumstances.
  ///
  /// Gender: each node's femaleModifier (negated for men, scaled down by how non-straight the pawn is) warps the rating
  ///   distribution: the rating becomes the x solving gM*x^2 + (1-gM)*x = rating, which shifts the distribution up for
  ///   gM &gt; 0 and down for gM &lt; 0 while keeping it inside [0, 1].
  ///
  /// Everything else accumulates a modifier t in (-1, 1) per node, combined with RelativisticAddition (u+v)/(1+uv) so
  ///   stacking never escapes (-1, 1):
  ///     - traits (PersonalityNodeDef.traitModifiers, matched on trait AND degree)
  ///     - skills (skillModifiers): Lerp(-0.20, +0.85, level / 20) per linked skill
  ///     - incapable work types (incapableModifiers)
  ///     - Cool: -0.1 if disfigured, +0.3 x PawnBeauty
  ///     - LaidBack: -0.5 to -0.999 with Anxiety severity
  ///   Then rating' = (1 - |t|) * rating + max(0, t): a positive t pulls the rating toward 1, a negative t toward 0,
  ///   by the fraction |t|.
  ///
  /// With applyTwice, t is first converted to t' = 1 - sqrt(1 - |t|) so that applying it twice (once before and once
  /// after the correlation step in CalculateAdjustedRatings) gives the same total pull as applying t once.
  /// </summary>
  public void AdjustForCircumstance(float[] ratings, bool applyTwice, Gender gender, List<Trait> traits, List<SkillRecord> skills, List<WorkTypeDef> incapables)
  {

    float[] tM = new float[PersonalityNodeMatrix.order];
    float s;
    float m;
    int index;
    Dictionary<int, float> dict;

    float gM;
    float gMm1;
    float kinseyFactor = PsychologySettings.enableKinsey ? 1f - PsycheHelper.Comp(pawn).Sexuality.kinseyRating / 6f : pawn.story.traits.HasTrait(TraitDefOf.Gay) ? 0f : 1f;

    if (PsycheHelper.GenderModifierNodeDefDict.TryGetValue(gender, out dict) && kinseyFactor != 0f)
    {
      foreach (KeyValuePair<int, float> kvp in dict)
      {
        gM = kvp.Value * kinseyFactor;
        if (Mathf.Abs(gM) > 0.001f)
        {
          gMm1 = gM - 1f;
          ratings[kvp.Key] = (gMm1 + Mathf.Sqrt(gMm1 * gMm1 + 4f * gM * ratings[kvp.Key])) / (2f * gM);
        }
      }
    }

    Pair<TraitDef, int> traitPair = new Pair<TraitDef, int>();
    bool trygetvalue;
    if (traits.NullOrEmpty() != true)
    {
      foreach (Trait trait in traits)
      {
        traitPair = new Pair<TraitDef, int>(trait.def, trait.Degree);
        trygetvalue = PsycheHelper.TraitModifierNodeDefDict.TryGetValue(traitPair, out dict);

        if (trygetvalue != true)
        {
          continue;
        }

        foreach (KeyValuePair<int, float> kvp in dict)
        {
          index = kvp.Key;
          tM[index] = PsycheHelper.RelativisticAddition(tM[index], kvp.Value);

        }
      }
    }

    float skillWeight;
    if (skills.NullOrEmpty() != true)
    {
      foreach (SkillRecord skill in skills)
      {
        trygetvalue = PsycheHelper.SkillModifierNodeDefDict.TryGetValue(skill.def, out HashSet<int> indices);

        if (trygetvalue != true)
        {
          continue;
        }

        foreach (int i in indices)
        {
          skillWeight = Mathf.Lerp(-0.20f, 0.85f, skill.levelInt / 20f);

          tM[i] = PsycheHelper.RelativisticAddition(tM[i], skillWeight);
        }

      }
    }

    if (incapables.NullOrEmpty() != true)
    {
      foreach (WorkTypeDef incapable in incapables)
      {
        if (PsycheHelper.IncapableModifierNodeDefDict.TryGetValue(incapable, out dict) != true)
        {
          continue;
        }
        foreach (KeyValuePair<int, float> kvp in dict)
        {
          index = kvp.Key;
          tM[index] = PsycheHelper.RelativisticAddition(tM[index], kvp.Value);
        }
      }
    }

    index = PersonalityNodeMatrix.indexDict[PersonalityNodeDefOf.Cool];
    tM[index] = RelationsUtility.IsDisfigured(this.pawn) ? PsycheHelper.RelativisticAddition(tM[index], -0.1f) : tM[index];
    tM[index] = PsycheHelper.RelativisticAddition(tM[index], 0.3f * this.pawn.GetStatValue(StatDefOf.PawnBeauty));

    index = PersonalityNodeMatrix.indexDict[PersonalityNodeDefOf.LaidBack];
    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
    {
      if (hediff.def == HediffDefOfPsychology.Anxiety)
      {
        float anxietyWeight = Mathf.Lerp(-0.5f, -0.999f, hediff.Severity);
        tM[index] = PsycheHelper.RelativisticAddition(tM[index], anxietyWeight);
        break;
      }
    }

    bool flag;
    if (applyTwice)
    {
      for (index = 0; index < PersonalityNodeMatrix.order; index++)
      {
        s = Mathf.Sign(tM[index]);
        m = Mathf.Abs(tM[index]);
        flag = m < 1f;
        tM[index] = flag ? s * (1f - Mathf.Sqrt(1f - m)) : s;
        // Log removed in the 1.5/1.6 builds (it fired for legitimately saturated modifiers and spammed the log).
        //if (flag != true)
        //{
        //  Log.Error("Psychology: AdjustForCircumstance gave modifier with magnitude greater than or equal to 1.");
        //}

      }
    }

    for (index = 0; index < PersonalityNodeMatrix.order; index++)
    {
      ratings[index] = (1f - Mathf.Abs(tM[index])) * ratings[index] + Mathf.Max(0f, tM[index]);
    }
  }

  /// <summary>Rebuilds this tracker's nodes with another tracker's raw ratings and caches (see CompPsychology.DeepCopyFromOtherComp).</summary>
  public void DeepCopyFromOtherTracker(Pawn_PsycheTracker trackerToCopy)
  {
    this.upbringing = trackerToCopy.upbringing;
    this.lastDateTick = trackerToCopy.lastDateTick;
    this.lastDateTickWith = new Dictionary<int, int>(trackerToCopy.lastDateTickWith);

    this.nodes = new HashSet<PersonalityNode>();
    foreach (PersonalityNodeDef def in PersonalityNodeMatrix.DefList)
    {
      this.nodes.Add(PersonalityNodeMaker.MakeNode(def, this.pawn));
    }
    foreach (PersonalityNode node in this.nodes)
    {
      node.rawRating = trackerToCopy.GetPersonalityNodeOfDef(node.def).rawRating;
    }
    foreach (PersonalityNode n in this.nodes)
    {
      this.nodeDict[n.def] = n;
    }
    foreach (KeyValuePair<string, float> kvp in trackerToCopy.cachedOpinions)
    {
      this.cachedOpinions[kvp.Key] = kvp.Value;
    }
    foreach (KeyValuePair<string, bool> kvp in trackerToCopy.recalcCachedOpinions)
    {
      this.recalcCachedOpinions[kvp.Key] = kvp.Value;
    }
    foreach (KeyValuePair<Pair<string, string>, float> kvp in trackerToCopy.cachedDisagreementWeights)
    {
      this.cachedDisagreementWeights[new Pair<string, string>(kvp.Key.First, kvp.Key.Second)] = kvp.Value;
    }
    foreach (KeyValuePair<Pair<string, string>, bool> kvp in trackerToCopy.recalcNodeDisagreement)
    {
      this.recalcNodeDisagreement[new Pair<string, string>(kvp.Key.First, kvp.Key.Second)] = kvp.Value;
    }
    foreach (KeyValuePair<MemeDef, Dictionary<PersonalityNodeDef, float>> kvp0 in trackerToCopy.dailyCertaintyFromMemesAndNodes)
    {
      this.dailyCertaintyFromMemesAndNodes[kvp0.Key] = new Dictionary<PersonalityNodeDef, float>();
      foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
      {
        this.dailyCertaintyFromMemesAndNodes[kvp0.Key][kvp1.Key] = kvp1.Value;
      }
    }
    foreach (KeyValuePair<PreceptDef, Dictionary<PersonalityNodeDef, float>> kvp0 in trackerToCopy.dailyCertaintyFromPerceptsAndNodes)
    {
      this.dailyCertaintyFromPerceptsAndNodes[kvp0.Key] = new Dictionary<PersonalityNodeDef, float>();
      foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
      {
        this.dailyCertaintyFromPerceptsAndNodes[kvp0.Key][kvp1.Key] = kvp1.Value;
      }
    }
  }

  /// <summary>
  /// (Ideology) How much the pawn's personality raises or lowers their ideoligion certainty per day.
  /// For every meme/precept of <paramref name="ideo"/> that some node lists in memeCertaintyModifiers /
  /// preceptCertaintyModifiers, adds DailyCertaintyChangeScale * (2 * rating - 1) * modifier: a pawn above average in
  /// a node that "likes" the meme gains certainty, one below average loses it. Added on top of vanilla's
  /// Pawn_IdeoTracker.CertaintyChangePerDay (Harmony/Pawn/Pawn_IdeoTracker.cs), cached in PsychologyGameComponent.
  /// With addToDicts, also records the per-meme/precept breakdown for the tooltip.
  /// </summary>
  public float CalculateCertaintyChangePerDay(Ideo ideo, bool addToDicts = true)
  {
    CalculateAdjustedRatings();
    if (addToDicts)
    {
      dailyCertaintyFromMemesAndNodes.Clear();
      dailyCertaintyFromPerceptsAndNodes.Clear();
    }
    if (ideo == null)
    {
      return 0f;
    }
    float certaintyChange = 0f;
    float rating;
    float adjustment;
    MemeDef memeDef;
    PreceptDef preceptDef;
    PersonalityNodeDef nodeDef;
    foreach (KeyValuePair<MemeDef, Dictionary<PersonalityNodeDef, float>> kvp0 in PersonalityNodeIdeoUtility.memesAffectedByNodes)
    {
      memeDef = kvp0.Key;
      if (ideo.HasMeme(memeDef) != true)
      {
        continue;
      }
      if (addToDicts && dailyCertaintyFromMemesAndNodes.ContainsKey(memeDef) != true)
      {
        dailyCertaintyFromMemesAndNodes[memeDef] = new Dictionary<PersonalityNodeDef, float>();
      }
      foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
      {
        nodeDef = kvp1.Key;
        rating = GetPersonalityRating(nodeDef);
        adjustment = PsycheHelper.DailyCertaintyChangeScale * (2f * rating - 1f) * kvp1.Value;
        certaintyChange += adjustment;
        if (addToDicts)
        {
          dailyCertaintyFromMemesAndNodes[memeDef][nodeDef] = adjustment;
        }
      }
    }
    foreach (KeyValuePair<PreceptDef, Dictionary<PersonalityNodeDef, float>> kvp0 in PersonalityNodeIdeoUtility.preceptsAffectedByNodes)
    {
      preceptDef = kvp0.Key;
      if (ideo.HasPrecept(preceptDef) != true)
      {
        continue;
      }
      if (addToDicts && dailyCertaintyFromPerceptsAndNodes.ContainsKey(preceptDef) != true)
      {
        dailyCertaintyFromPerceptsAndNodes[preceptDef] = new Dictionary<PersonalityNodeDef, float>();
      }
      foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
      {
        nodeDef = kvp1.Key;
        rating = GetPersonalityRating(nodeDef);
        adjustment = PsycheHelper.DailyCertaintyChangeScale * (2f * rating - 1f) * kvp1.Value;
        certaintyChange += adjustment;
        if (addToDicts)
        {
          dailyCertaintyFromPerceptsAndNodes[preceptDef][nodeDef] = adjustment;
        }
      }
    }
    foreach (KeyValuePair<MemeDef, Dictionary<PersonalityNodeDef, float>> kvp0 in dailyCertaintyFromMemesAndNodes)
    {
      foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
      {
      }
    }
    return certaintyChange;
  }

  // Raw compatibility scores between random pawns average about -0.033 with a standard deviation of about 0.09.
  // Centring on the mean and dividing by three standard deviations puts a typical pair at 0 and the most / least
  // compatible 1% of pairs at about +/-0.77.
  public const float PersonalityCompatibilityMean = -0.033f;
  public const float PersonalityCompatibilitySpread = 0.27f;

  /// <summary>
  /// How well this pawn's personality fits another pawn's, in [-1, 1] (0 = a typical pair). Every node is scored with
  /// the same agree/disagree curve conversations use (SaddleShapeFunction, sharper for more controversial topics) and
  /// weighted by its controversiality. 0 if the other pawn has no psyche. Used by Thought_RapportBuilt.
  /// </summary>
  public float PersonalityCompatibilityWith(Pawn other)
  {
    if (!PsycheHelper.PsychologyEnabled(other))
    {
      return 0f;
    }
    Pawn_PsycheTracker otherPsyche = PsycheHelper.Comp(other).Psyche;
    float total = 0f;
    float weight = 0f;
    foreach (PersonalityNode node in this.nodes)
    {
      float controversiality = node.def.controversiality;
      if (controversiality <= 0f)
      {
        continue;
      }
      float agreement = PsycheHelper.SaddleShapeFunction(GetPersonalityRating(node.def), otherPsyche.GetPersonalityRating(node.def), 1f - 0.5f * controversiality, 4f * controversiality * controversiality);
      total += controversiality * agreement;
      weight += controversiality;
    }
    if (weight <= 0f)
    {
      return 0f;
    }
    return Mathf.Clamp((total / weight - PersonalityCompatibilityMean) / PersonalityCompatibilitySpread, -1f, 1f);
  }

  /// <summary>
  /// (Ideology) Cosine similarity, in [-1, 1], between the pawn's personality (adjusted ratings minus 0.5) and the
  /// direction the ideoligion "favours" (PersonalityNodeIdeoUtility.FavoredDisplacmentForIdeo). Scales conversion
  /// power in the ConversionUtility.ConversionPowerFactor_MemesVsTraits patch. calcAdjusted is unused.
  /// </summary>
  public float CompatibilityWithIdeo(Ideo ideo, bool calcAdjusted = true)
  {
    if (ideo == null)
    {
      return 0f;
    }
    CalculateAdjustedRatings();
    int index;
    float[] adjDisplacementUnitVector = new float[PersonalityNodeMatrix.order];
    float norm = 0f;
    float adjDisplacement;
    foreach (PersonalityNode node in this.nodes)
    {
      index = PersonalityNodeMatrix.indexDict[node.def];
      adjDisplacement = node.AdjustedRating - 0.5f;
      adjDisplacementUnitVector[index] = adjDisplacement;
      norm += adjDisplacement * adjDisplacement;
    }
    norm = Mathf.Sqrt(norm);
    if (norm != 0f)
    {
      for (index = 0; index < adjDisplacementUnitVector.Count(); index++)
      {
        adjDisplacementUnitVector[index] /= norm;
      }
    }
    float[] favoredDisplacementUnitVector = PersonalityNodeIdeoUtility.FavoredDisplacmentForIdeo(ideo, randomize: false, normalize: true);
    float compat = PersonalityNodeMatrix.DotProduct(adjDisplacementUnitVector, favoredDisplacementUnitVector);
    if (Mathf.Abs(compat) > 1f)
    {
      Log.Error("Compatibility exceeded bounds of -1 to +1, compat = " + compat + ", for pawn " + this.pawn);
    }
    return compat;
  }

  /// <summary>
  /// (Ideology) Permanently shifts the pawn's RAW ratings toward what <paramref name="ideo"/> favours, then refreshes the
  /// cached daily certainty change. This is the effect of the Moral Guide's Indoctrinate ability
  /// (CompAbilityEffect_Indoctrinate).
  /// </summary>
  public void BoostRatingsTowardsIdeo(Ideo ideo, float alpha, bool randomize)
  {
    CalculateAdjustedRatings();
    float[] newRawRatings = RatingsAfterBoostTowardsIdeo(ideo, alpha, randomize);
    foreach (PersonalityNode node in this.nodes)
    {
      node.rawRating = newRawRatings[PersonalityNodeMatrix.indexDict[node.def]];
    }
    int idnumber = pawn.thingIDNumber;
    float certaintyChange = CalculateCertaintyChangePerDay(pawn.Ideo, true);
    PsycheHelper.GameComp.CachedCertaintyChangePerDayDict[idnumber] = certaintyChange;
  }

  /// <summary>
  /// Computes the raw ratings after one round of indoctrination, without applying them. Each node with a favoured
  /// direction m moves in normal (probit) space by 2 * alpha * |m| * probit(0.5 + 0.5 * (target - r)), where target is 1
  /// or 0 depending on the sign of m. The step shrinks as the rating approaches its target, which gives the diminishing
  /// returns described on the Workshop page. randomize adds a random component of the same size to the direction.
  /// </summary>
  public float[] RatingsAfterBoostTowardsIdeo(Ideo ideo, float alpha, bool randomize = false)
  {
    float[] newRawRatings = new float[PersonalityNodeMatrix.order];
    // alpha controls the scale of the maximum increase in certainty
    float alphaTimesTwo = 2f * alpha;
    float[] mvec = PersonalityNodeIdeoUtility.FavoredDisplacmentForIdeo(ideo, randomize);
    float r;
    int index;
    float m;
    float u;
    float x;
    foreach (PersonalityNode node in this.nodes)
    {
      r = node.rawRating;
      index = PersonalityNodeMatrix.indexDict[node.def];
      m = mvec[index];
      if (m == 0f)
      {
        newRawRatings[index] = r;
        continue;
      }
      u = m > 0f ? 1f : 0f;
      x = alphaTimesTwo * Mathf.Abs(m) * PsycheHelper.NormalCDFInv(0.5f + 0.5f * (u - r));
      newRawRatings[index] = PsycheHelper.NormalCDF(PsycheHelper.NormalCDFInv(r) + x);
    }
    return newRawRatings;
  }

}
