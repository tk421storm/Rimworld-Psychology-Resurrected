using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Diagnostics;

namespace Psychology;

/// <summary>
/// One facet of a pawn's personality (e.g. "Aggressive") for one pawn. Only def and rawRating are saved;
/// AdjustedRating is filled in by Pawn_PsycheTracker.CalculateAdjustedRatings(). Read ratings through
/// Pawn_PsycheTracker.GetPersonalityRating(), which keeps AdjustedRating up to date.
/// </summary>
public class PersonalityNode : IExposable
{
    public Pawn pawn;
    public PersonalityNodeDef def;
    // Innate, independent roll in [0, 1]; see Pawn_PsycheTracker.RandomizeRatings.
    public float rawRating;
    private float cachedRating = -1f;

    public bool HasConvoTopics => this.def.conversationTopics.NullOrEmpty() != true;
    public float AdjustedRating
    {
        get
        {
            return cachedRating;
        }
        set
        {
            cachedRating = value;
        }
    }

    public PersonalityNode()
    {
    }

    public PersonalityNode(Pawn pawn)
    {
        this.pawn = pawn;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref this.def, "def");
        Scribe_Values.Look(ref this.rawRating, "rawRating", -1f, false);
    }

    public override int GetHashCode()
    {
        return this.pawn.GetHashCode() + GenText.StableStringHash(this.def.defName);
    }

    // Old per-node version of Pawn_PsycheTracker.AdjustForCircumstance, replaced by the array-based one for speed.
    //public float AdjustForCircumstance(float rating, bool applyingTwice = false)
    //{
    //    Stopwatch stopwatch = new Stopwatch();

}
