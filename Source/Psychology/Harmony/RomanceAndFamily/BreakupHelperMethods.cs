using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;
using System.Xml;
using System.Security.Cryptography;

namespace Psychology.Harmony;

/// <summary>
/// Personality-scaled versions of vanilla's relationship-ending thoughts (broke up with me, divorced me, rejected my
/// proposal, cheated on me, I rejected their proposal). InterdictTryGainAndRemoveMemories is a reusable transpiler:
/// it finds vanilla's `memories.TryGainMemory(ThoughtDefOf.X, otherPawn)` / `RemoveMemoriesOfDefWhereOtherPawnIs(...)`
/// calls for those five defs and redirects each to the matching Gain*/Remove* method below.
///
/// The strength: each def's stages give a min..max range (extended by one step); GetOffsetFromMinMax picks a point in
/// that range from the victim's Romantic rating and their opinion of the other pawn, so a romantic pawn who loved
/// their partner is hurt much more than an unromantic one who didn't. The value is stored as the thought's
/// opinionOffset/moodOffset on the closest stage. Mood versions ("...Mood" defs) are added alongside the opinions, and
/// Codependent pawns get extra Codependent thoughts.
/// </summary>
[StaticConstructorOnStartup]
public static class RomanceHelperMethods
{
    public static ThoughtDef BrokeUpWithMeMoodDef = DefDatabase<ThoughtDef>.GetNamed("BrokeUpWithMeMood");
    public static ThoughtDef DivorcedMeMoodDef = DefDatabase<ThoughtDef>.GetNamed("DivorcedMeMood");
    public static ThoughtDef CheatedOnMeMoodDef = DefDatabase<ThoughtDef>.GetNamed("CheatedOnMeMood");

    public static Dictionary<ThoughtDef, string[]> PsychologyMethodNameDict = new Dictionary<ThoughtDef, string[]>()
    {
        { ThoughtDefOf.BrokeUpWithMe          , new string[] { nameof(GainBrokeUpWithMe) , nameof(RemoveBrokeUpWithMe) } },
        { ThoughtDefOf.DivorcedMe             , new string[] { nameof(GainDivorcedMe) , nameof(RemoveDivorcedMe) } },
        { ThoughtDefOf.RejectedMyProposal     , new string[] { nameof(GainRejectedMyProposal) , nameof(RemoveRejectedMyProposal) } },
        { ThoughtDefOf.CheatedOnMe            , new string[] { nameof(GainCheatedOnMe) , nameof(RemoveCheatedOnMe) } },
        { ThoughtDefOf.IRejectedTheirProposal , new string[] { nameof(GainIRejectedTheirProposal) , nameof(RemoveIRejectedTheirProposal) } }
    };

    public static List<ThoughtDef> OpinionThoughtDefs;
    public static Dictionary<ThoughtDef, Pair<float, float>> MinMaxOpinionDict = new Dictionary<ThoughtDef, Pair<float, float>>();

    public static List<ThoughtDef> MoodThoughtDefs = new List<ThoughtDef> { BrokeUpWithMeMoodDef, DivorcedMeMoodDef, ThoughtDefOf.RejectedMyProposalMood, CheatedOnMeMoodDef, ThoughtDefOfPsychology.IBrokeUpWithThemMood, ThoughtDefOfPsychology.IDivorcedThemMood, ThoughtDefOfPsychology.IRejectedTheirProposalMood };
    public static Dictionary<ThoughtDef, Pair<float, float>> MinMaxMoodDict = new Dictionary<ThoughtDef, Pair<float, float>>();

    static RomanceHelperMethods()
    {
        OpinionThoughtDefs = (from kvp in PsychologyMethodNameDict
                              select kvp.Key).ToList();
        List<float> list;
        foreach (ThoughtDef thoughtDef in OpinionThoughtDefs)
        {
            list = (from ThoughtStage s in thoughtDef.stages
                    select s.baseOpinionOffset).ToList();
            MinMaxOpinionDict[thoughtDef] = GetMinMaxPair(list);
        }
        foreach (ThoughtDef thoughtDef in MoodThoughtDefs)
        {
            list = (from ThoughtStage s in thoughtDef.stages
                    select s.baseMoodEffect).ToList();
            MinMaxMoodDict[thoughtDef] = GetMinMaxPair(list);
        }
    }

    // Range of a def's stage values (including 0), widened by the smallest step between stages so personality can push a
    // thought slightly beyond the strongest/weakest vanilla stage.
    public static Pair<float, float> GetMinMaxPair(List<float> list)
    {
        list.Add(0f);
        list.Sort();
        float minDiff = Mathf.Abs(list[1] - list[0]);
        for (int i = 2; i < list.Count(); i++)
        {
            float diff = Mathf.Abs(list[i] - list[i - 1]);
            minDiff = diff > 0f ? Mathf.Min(diff, minDiff) : minDiff;
        }
        float min = list.Min();
        float max = list.Max();
        min = min == 0f ? 0f : min - minDiff;
        max = max == 0f ? 0f : max + minDiff;
        return new Pair<float, float>(min, max);
    }

    // ToDo: might need to change lerpOpinionToZeroAfterAgeDuration
    // ToDo: look for TryGainThought of things like BrokeUpWithMe, DivorcedMe, etc
    // TryGainMemory and RemoveMemoriesOfDefWhereOtherPawnIs occur in the following methods:
    // - InteractionWorker_Breakup.Interacted                                     for TryGain of BrokeUpWithMe
    // - InteractionWorker_MarriageProposal.Interacted                            for TryGain and RemoveMemories of RejectedMyProposal
    // - InteractionWorker_RomanceAttempt.RemoveBrokeUpAndFailedRomanceThoughts   for RemoveMemories of BrokeUpWithMe
    // - InteractionWorker_RomanceAttempt.TryAddCheaterThought                    for TryGain of CheatedOnMe
    // - MarriageCeremonyUtility.Married                                          for RemoveMemories of DivorcedMe
    // - SpouseRelationUtility.RemoveGotMarriedThoughts                           for TryGain of DivorcedMe

    // Scans backwards for (TryGainMemory | RemoveMemoriesOfDefWhereOtherPawnIs) <- ldfld memories <- ldsfld ThoughtDefOf.X,
    // then re-emits the method with each match replaced by `call Gain/RemoveX(pawn, otherPawn)`. pawn is assumed to be
    // loaded 4 instructions before `memories`, otherPawn right after the def, so this is tied to vanilla's IL shape.
    public static IEnumerable<CodeInstruction> InterdictTryGainAndRemoveMemories(IEnumerable<CodeInstruction> codes)
    {
        List<CodeInstruction> clist = codes.ToList();

        FieldInfo fieldInfoMemories = AccessTools.Field(typeof(ThoughtHandler), nameof(ThoughtHandler.memories));
        MethodInfo methodInfoTryGainMemory = AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new Type[] { typeof(ThoughtDef), typeof(Pawn), typeof(Precept) });
        MethodInfo methodInfoRemoveMemories = AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.RemoveMemoriesOfDefWhereOtherPawnIs));
        List<Pair<int, FieldInfo>> fieldInfos = (from ThoughtDef def in OpinionThoughtDefs
                                                 select new Pair<int, FieldInfo>(OpinionThoughtDefs.IndexOf(def), AccessTools.Field(typeof(ThoughtDefOf), def.defName))).ToList();

        List<int> listTryGainOrRemoveIndexInClist = new List<int>();
        List<int> listGainVsRemoveBit = new List<int>();
        List<int> listOtherPawnIndexInClist = new List<int>();
        List<int> listThoughtDefIndexInClist = new List<int>();
        List<int> listThoughtDefIndexInOpinionThoughtDefs = new List<int>();
        List<int> listMemoriesIndexInClist = new List<int>();
        List<int> listPawnIndexInClist = new List<int>();

        int searchMode = 0;
        for (int i = clist.Count() - 1; i >= 0; i--)
        {
            switch (searchMode)
            {
                case 0:
                    int gainVsRemoveBit = clist[i].Calls(methodInfoTryGainMemory) ? 0 : clist[i].Calls(methodInfoRemoveMemories) ? 1 : -1;
                    if (gainVsRemoveBit != -1)
                    {
                        searchMode++;
                        listTryGainOrRemoveIndexInClist.Add(i);
                        listGainVsRemoveBit.Add(gainVsRemoveBit);
                    }
                    break;
                case 1:
                    if (clist[i].LoadsField(fieldInfoMemories))
                    {
                        searchMode++;
                        listMemoriesIndexInClist.Add(i);
                        listPawnIndexInClist.Add(i - 4);
                    }
                    break;
                default:
                    bool foundDef = false;
                    for (int j = i; j < listTryGainOrRemoveIndexInClist.Last(); j++)
                    {
                        foreach (Pair<int, FieldInfo> pair in fieldInfos)
                        {
                            if (clist[j].LoadsField(pair.Second))
                            {
                                foundDef = true;
                                listOtherPawnIndexInClist.Add(j + 1);
                                listThoughtDefIndexInClist.Add(j);
                                listThoughtDefIndexInOpinionThoughtDefs.Add(pair.First);
                                break;
                            }
                        }
                        if (foundDef)
                        {
                            break;
                        }
                    }
                    if (!foundDef)
                    {
                        listTryGainOrRemoveIndexInClist.RemoveLast();
                        listGainVsRemoveBit.RemoveLast();
                        listMemoriesIndexInClist.RemoveLast();
                        listPawnIndexInClist.RemoveLast();
                    }
                    searchMode = 0;
                    break;
            }
        }

        if (searchMode != 0)
        {
            Log.Error("InterdictTryGainThoughtAndRemoveMemories failed, stuck at searchMode = " + searchMode);
            foreach (CodeInstruction c in codes)
            {
                yield return c;
            }
            yield break;
        }
        for (int i = 0; i < clist.Count(); i++)
        {
            yield return clist[i];
            int matchNum = listPawnIndexInClist.IndexOf(i);
            if (matchNum >= 0)
            {
                yield return clist[listOtherPawnIndexInClist[matchNum]];
                ThoughtDef thoughtDef = OpinionThoughtDefs[listThoughtDefIndexInOpinionThoughtDefs[matchNum]];
                yield return CodeInstruction.Call(typeof(RomanceHelperMethods), PsychologyMethodNameDict[thoughtDef][listGainVsRemoveBit[matchNum]]);
                i = listTryGainOrRemoveIndexInClist[matchNum];

            }
        }

    }

    /* Gain memories */
    public static void GainBrokeUpWithMe(Pawn pawn, Pawn otherPawn)
    {
        TryGainOpinion(pawn, otherPawn, ThoughtDefOf.BrokeUpWithMe);
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, otherPawn);
        TryGainMood(pawn, otherPawn, BrokeUpWithMeMoodDef);
        TryGainMood(otherPawn, pawn, ThoughtDefOfPsychology.IBrokeUpWithThemMood);
    }

    public static void GainDivorcedMe(Pawn pawn, Pawn otherPawn)
    {
        TryGainOpinion(pawn, otherPawn, ThoughtDefOf.DivorcedMe);
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DivorcedMeCodependent, otherPawn);
        TryGainMood(pawn, otherPawn, DivorcedMeMoodDef);
        TryGainMood(otherPawn, pawn, ThoughtDefOfPsychology.IDivorcedThemMood);
    }

    public static void GainRejectedMyProposal(Pawn pawn, Pawn otherPawn)
    {
        TryGainOpinion(pawn, otherPawn, ThoughtDefOf.RejectedMyProposal);
        // The released 1.4-1.6 builds dropped the Codependent memory here and give the mood thought to the rejected pawn
        // (the source gave it to the one who rejected).
        //pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RejectedMyProposalCodependent, otherPawn);
        TryGainMood(pawn, otherPawn, ThoughtDefOf.RejectedMyProposalMood);
        TryGainMood(otherPawn, pawn, ThoughtDefOfPsychology.IRejectedTheirProposalMood);
    }

    public static void GainCheatedOnMe(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.CheatedOnMe, otherPawn);
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.CheatedOnMeCodependent, otherPawn);
        TryGainMood(pawn, otherPawn, CheatedOnMeMoodDef);
        //ToDo: make ICheatedOnThemMood ?
        //TryGainMood(pawn, otherPawn, ThoughtDefOfPsychology.ICheatedOnThemMood);
    }

    public static void GainIRejectedTheirProposal(Pawn pawn, Pawn otherPawn)
    {
        // The source had the CheatedOnMe body copy-pasted here; the released 1.4-1.6 builds emptied this method, so
        // the vanilla "I rejected their proposal" thought is simply suppressed (the mood part comes from
        // GainRejectedMyProposal for the other pawn).
        //TryGainOpinion(pawn, otherPawn, ThoughtDefOf.CheatedOnMe);
        //pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.CheatedOnMe, otherPawn);
        //pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.CheatedOnMeCodependent, otherPawn);
        //TryGainMood(pawn, otherPawn, CheatedOnMeMoodDef);
        //ToDo: make ICheatedOnThemMood ?
        //TryGainMood(pawn, otherPawn, ThoughtDefOfPsychology.ICheatedOnThemMood);
    }

    public static void TryGainOpinion(Pawn pawn, Pawn otherPawn, ThoughtDef thoughtDef)
    {
        float opinionOffset = GetOffsetFromMinMax(pawn, otherPawn, MinMaxOpinionDict[thoughtDef]);
        List<Pair<int, float>> baseOpinionOffsetList = (from int k in Enumerable.Range(0, thoughtDef.stages.Count())
                                                        select new Pair<int, float>(k, thoughtDef.stages[k].baseOpinionOffset)).ToList();
        int index = GetStageIndex(opinionOffset, baseOpinionOffsetList);
        if (ThoughtMaker.MakeThought(thoughtDef, index) is Thought_MemorySocial thought)
        {
            thought.opinionOffset = opinionOffset;
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought, otherPawn);
            return;
        }
        Log.Error("Thought as Thought_MemorySocial was null");
        pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, otherPawn);
    }

    public static void TryGainMood(Pawn pawn, Pawn otherPawn, ThoughtDef thoughtDef)
    {
        float moodOffset = GetOffsetFromMinMax(pawn, otherPawn, MinMaxMoodDict[thoughtDef]);
        List<Pair<int, float>> baseMoodEffectList = (from int k in Enumerable.Range(0, thoughtDef.stages.Count())
                                                     select new Pair<int, float>(k, thoughtDef.stages[k].baseMoodEffect)).ToList();
        int index = GetStageIndex(moodOffset, baseMoodEffectList);
        if (index == -1)
        {
            return;
        }
        Thought_Memory thought = ThoughtMaker.MakeThought(thoughtDef, index);
        thought.moodOffset = Mathf.RoundToInt(moodOffset);
        pawn.needs.mood.thoughts.memories.TryGainMemory(thought, otherPawn);
    }

    // Lerp(min, max, 0.5 - r - o) with r = 0.75 (Romantic - 0.5) and o = 1.25 (opinion mapped from [-5, 100] to [0, 1] - 0.5),
    // rounded away from zero. For negative thoughts (min < 0 = max) higher Romantic/opinion means a stronger negative.
    public static float GetOffsetFromMinMax(Pawn pawn, Pawn otherPawn, Pair<float, float> pair)
    {
        float r = 0.75f * (PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic) - 0.5f);
        float o = 1.25f * (Mathf.InverseLerp(-5f, 100f, pawn.relations.OpinionOf(otherPawn)) - 0.5f);
        float moodOffset = Mathf.Lerp(pair.First, pair.Second, 0.5f - r - o);
        return Mathf.Sign(moodOffset) * Mathf.Ceil(1e-5f + Mathf.Abs(moodOffset));
    }

    public static int GetStageIndex(float offset, List<Pair<int, float>> baseEffectList)
    {
        List<int> allowedStageIndices = (from pair in baseEffectList
                                         where Math.Sign(offset) == Math.Sign(pair.Second)
                                         select pair.First).ToList();
        if (allowedStageIndices.NullOrEmpty())
        {
            // Log removed in the released builds: an empty list just means no stage matches the sign of the offset.
            //Log.Error("allowedStageIndices is null or empty.");
            return -1;
        }
        return allowedStageIndices.MinBy(k => Mathf.Abs(offset - baseEffectList[k].Second));
    }

    /* Remove memories */
    public static void RemoveBrokeUpWithMe(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.BrokeUpWithMe, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(BrokeUpWithMeMoodDef, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependentMood, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.IBrokeUpWithThemMood, otherPawn);
    }

    public static void RemoveDivorcedMe(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.DivorcedMe, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(DivorcedMeMoodDef, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.DivorcedMeCodependent, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.DivorcedMeCodependentMood, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.IDivorcedThemMood, otherPawn);
    }

    public static void RemoveRejectedMyProposal(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.RejectedMyProposal, otherPawn);
        // This one is redundant in practice, but it's good to be safe
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.RejectedMyProposalMood, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.RejectedMyProposalCodependent, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.RejectedMyProposalCodependentMood, otherPawn);
    }

    public static void RemoveCheatedOnMe(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.CheatedOnMe, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(CheatedOnMeMoodDef, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.CheatedOnMeCodependent, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.CheatedOnMeCodependentMood, otherPawn);
    }

    public static void RemoveIRejectedTheirProposal(Pawn pawn, Pawn otherPawn)
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.IRejectedTheirProposal, otherPawn);
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.IRejectedTheirProposalMood, otherPawn);
    }

}
