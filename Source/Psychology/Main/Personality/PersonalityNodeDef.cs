using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology;

/// <summary>
/// XML definition of one personality facet (Common/Defs/PersonalityDefs/PersonalityNodes.xml). Besides labels and
/// colours for the Psyche tab, it describes how the facet is generated (parents, tenAspects, gender/trait/skill/
/// incapable modifiers), how it is summarised (bigFiveModifiers) and what it does in play (conversation topics and
/// controversiality, preferred date hours, Ideology certainty modifiers).
/// </summary>
public class PersonalityNodeDef : Def
{

    /* Being a woman modifies this node by this amount, reduced by how gay she is. The opposite effect is applied to men.
     * This models the cultural impact traditional gender roles have on their personality. (Lesbians/bisexuals, obviously, tend to subvert them.)
     * Even in 55XX, the patriarchy has not been vanquished. /s
     */
    public float femaleModifier;
    //A list of the DefNames of the parents of this node.
    // Explicit correlations with other nodes. PersonalityNodeMatrix uses these (symmetrically) as entries of the
    // correlation matrix; pairs without an explicit parent link get a correlation derived from tenAspects instead.
    public List<PersonalityNodeParent> parents;
    //What pawns talk about when they talk about this node.
    public List<string> conversationTopics;
    //A list of the skills that modify this node.
    public List<PersonalityNodeSkillModifier> skillModifiers;
    //A list of the traits that modify this node.
    public List<PersonalityNodeTraitModifier> traitModifiers;
    // Growth moments only: leans the passions offered to a child. See GrowthMomentWeights.PassionWeight.
    public List<PersonalityNodePassionAffinity> passionAffinities;
    //A list of the work types that being incapable of modify this node.
    public List<PersonalityNodeIncapableModifier> incapableModifiers;
    //How much a difference (or similarity) in this node affects what pawns think of each other after a conversation.
    // Also: high-controversiality topics are avoided with strangers (Pawn_PsycheTracker.GetConversationTopicWeight),
    // and both the size and the agree/disagree tipping point of the resulting opinion depend on it (Hediff_Conversation).
    public float controversiality;
    //The hours of the day that people with a high rating in this node will prefer to go on dates.
    public List<int> preferredDateHours;
    //Antonym Name
    public string antonymLabel;
    //Label for node in the description
    public string descriptionLabel;
    //Color for node label
    public Vector3 nodeHSV;
    //Color for antonym label
    public Vector3 antonymHSV;
    //Modifiers to the Big Five summary
    // Five loadings, in the order used by the Psyche tab radar: [0] Openness, [1] Conscientiousness, [2] Extraversion,
    // [3] Agreeableness, [4] Neuroticism (check PsycheCardUtility if the order matters to you).
    public List<float> bigFiveModifiers;
    //Weights on the 10 Aspects
    // DeYoung's "Big Five Aspects": two aspects per Big Five domain. Used to infer correlations between nodes that
    // have no explicit parent link (PersonalityNodeMatrix).
    public List<PersonalityNodeTenAspects> tenAspects;
    //List of certainty modifiers for each meme
    // (Ideology) Positive modifier: pawns high in this node like the meme and gain certainty; low pawns lose it.
    public List<PersonalityNodeMemeCertaintyModifier> memeCertaintyModifiers;
    //List of certainty modifiers for each precept
    public List<PersonalityNodePreceptCertaintyModifier> preceptCertaintyModifiers;

    //A list of the actual parent Defs of this node.
    //[Unsaved]
    private Dictionary<PersonalityNodeDef, PersonalityNodeParent> parentDict;
    private Dictionary<int, float> tenAspectsDict;

    public float GetModifier(PersonalityNodeDef parentDef)
    {
        return ParentNodes[parentDef].modifier;
    }

    public Dictionary<PersonalityNodeDef, PersonalityNodeParent> ParentNodes
    {
        get
        {
            if (this.parentDict == null)
            {
                this.parentDict = new Dictionary<PersonalityNodeDef, PersonalityNodeParent>();
                if (this.parents != null && this.parents.Count > 0)
                {
                    foreach (PersonalityNodeParent parent in this.parents)
                    {
                        this.parentDict.Add(parent.node, parent);
                    }
                }
            }
            return parentDict;
        }
    }

    public Dictionary<int, float> TenAspects
    {
        get
        {
            if (this.tenAspectsDict == null)
            {
                this.tenAspectsDict = new Dictionary<int, float>();
                List<string> tenAspectsList = new List<string>(){ "Openness", "Intellect", "Industriousness", "Orderliness", "Enthusiasm", "Assertiveness", "Compassion", "Politeness", "Volatility", "Withdrawal" };
                for (int t = 0; t < 10; t++)
                {
                    this.tenAspectsDict.Add(t, 0f);
                }
                if (this.tenAspects != null && this.tenAspects.Count > 0)
                {
                    foreach (PersonalityNodeTenAspects aspect in this.tenAspects)
                    {
                        int t = tenAspectsList.IndexOf(aspect.aspect);
                        this.tenAspectsDict[t] = aspect.weight;
                    }
                }
            }
            return this.tenAspectsDict;
        }
    }

    // Stable across sessions, which the seeded rating rolls depend on.
    public override int GetHashCode()
    {
        return GenText.StableStringHash(this.defName);
    }
}
