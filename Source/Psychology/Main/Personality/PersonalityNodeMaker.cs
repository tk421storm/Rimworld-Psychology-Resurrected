using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Psychology;

// Factory for PersonalityNode; ratings are filled in by Pawn_PsycheTracker.RandomizeRatings.
public static class PersonalityNodeMaker
{
    public static PersonalityNode MakeNode(PersonalityNodeDef def, Pawn pawn)
    {
        PersonalityNode node = new PersonalityNode(pawn);
        node.def = def;
        return node;
    }
}
