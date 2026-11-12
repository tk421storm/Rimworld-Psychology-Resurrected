using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology
{
    // XML entry in PersonalityNodeDef.traitModifiers. modifier is in (-1, 1): the fraction by which the trait pulls the
    // node's rating toward 1 (positive) or 0 (negative); see Pawn_PsycheTracker.AdjustForCircumstance.
    public class PersonalityNodeTraitModifier
    {
        //The trait that affects this node.
        public TraitDef trait;
        //The required degree of the trait.
        public int degree;
        //How much having this trait adds or subtracts from this node.
        public float modifier;
    }
}
