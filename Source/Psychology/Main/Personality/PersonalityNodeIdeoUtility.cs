using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology;

/// <summary>
/// (Ideology) Inverts the per-node meme/precept certainty modifiers into meme -> {node: modifier} and
/// precept -> {node: modifier} lookups at startup, and computes the personality direction an ideoligion favours.
/// </summary>
[StaticConstructorOnStartup]
public static class PersonalityNodeIdeoUtility
{
    public static Dictionary<MemeDef, Dictionary<PersonalityNodeDef, float>> memesAffectedByNodes = new Dictionary<MemeDef, Dictionary<PersonalityNodeDef, float>>();
    public static Dictionary<PreceptDef, Dictionary<PersonalityNodeDef, float>> preceptsAffectedByNodes = new Dictionary<PreceptDef, Dictionary<PersonalityNodeDef, float>>();

    static PersonalityNodeIdeoUtility()
    {
        foreach (PersonalityNodeDef def in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
        {
            if (def.memeCertaintyModifiers != null && def.memeCertaintyModifiers.Count > 0)
            {
                foreach (PersonalityNodeMemeCertaintyModifier mcm in def.memeCertaintyModifiers)
                {
                    if (!memesAffectedByNodes.ContainsKey(mcm.meme))
                    {
                        memesAffectedByNodes.Add(mcm.meme, new Dictionary<PersonalityNodeDef, float>());
                    }
                    memesAffectedByNodes[mcm.meme][def] = mcm.modifier;
                }
            }
            if (def.preceptCertaintyModifiers != null && def.preceptCertaintyModifiers.Count > 0)
            {
                
                foreach (PersonalityNodePreceptCertaintyModifier pcm in def.preceptCertaintyModifiers)
                {
                    if (!preceptsAffectedByNodes.ContainsKey(pcm.precept))
                    {
                        preceptsAffectedByNodes.Add(pcm.precept, new Dictionary<PersonalityNodeDef, float>());
                    }
                    preceptsAffectedByNodes[pcm.precept][def] = pcm.modifier;
                }
            }
        }
    }

    /// <summary>
    /// Sum, over the ideoligion's memes and precepts, of each node's modifier: the direction in personality space that the
    /// ideoligion rewards. Normalised to a unit vector by default; with randomize, a random direction of equal length is
    /// added first, so different rounds of indoctrination push along slightly different lines.
    /// </summary>
    public static float[] FavoredDisplacmentForIdeo(Ideo ideo, bool randomize = false, bool normalize = true)
    {
        float[] favoredVector = new float[PersonalityNodeMatrix.order];
        int i;
        bool noContributions = true;
        foreach (KeyValuePair<MemeDef, Dictionary<PersonalityNodeDef, float>> kvp0 in memesAffectedByNodes)
        {
            if (ideo.HasMeme(kvp0.Key) != true)
            {
                continue;
            }
            foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
            {
                favoredVector[PersonalityNodeMatrix.indexDict[kvp1.Key]] += kvp1.Value;
                noContributions = false;
            }
        }
        foreach (KeyValuePair<PreceptDef, Dictionary<PersonalityNodeDef, float>> kvp0 in preceptsAffectedByNodes)
        {
            if (ideo.HasPrecept(kvp0.Key) != true)
            {
                continue;
            }
            foreach (KeyValuePair<PersonalityNodeDef, float> kvp1 in kvp0.Value)
            {
                favoredVector[PersonalityNodeMatrix.indexDict[kvp1.Key]] += kvp1.Value;
                noContributions = false;
            }
        }
        if (noContributions == true)
        {
            return favoredVector;
        }
        float favoredNorm = PersonalityNodeMatrix.DotProduct(favoredVector, favoredVector);
        if (randomize)
        {
            // Pick out a random unit vector
            float randomNorm = 0f;
            float[] randomVector = new float[PersonalityNodeMatrix.order];
            while (randomNorm == 0f)
            {
                float rand;
                for (i = 0; i < randomVector.Count(); i++)
                {
                    rand = Rand.Gaussian();
                    randomVector[i] = rand;
                    randomNorm += rand * rand;
                }
                randomNorm = Mathf.Sqrt(randomNorm);
            }
            float alpha = 1f;
            for (i = 0; i < randomVector.Count(); i++)
            {
                favoredVector[i] += alpha * favoredNorm * randomVector[i] / randomNorm;
            }
        }
        if (!normalize)
        {
            return favoredVector;
        }
        // Norm could still be zero by coincidental cancellations between modifiers
        if (favoredNorm > 0f)
        {
            favoredNorm = Mathf.Sqrt(favoredNorm);
            for (i = 0; i < PersonalityNodeMatrix.order; i++)
            {
                favoredVector[i] /= favoredNorm;
            }
        }
        return favoredVector;
    }

}
