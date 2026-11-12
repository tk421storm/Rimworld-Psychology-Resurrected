using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology;

// XML entry in PersonalityNodeDef.incapableModifiers: applied when the pawn can't do this work type (backstory/traits).
public class PersonalityNodeIncapableModifier
{
    //The work tag that affects this node.
    public WorkTypeDef type;
    //How much having this trait adds or subtracts from this node.
    public float modifier;
}
