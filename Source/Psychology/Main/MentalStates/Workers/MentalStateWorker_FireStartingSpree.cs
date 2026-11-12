using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// Treated pyromaniacs are less and less likely to go on a spree as RecoveringPyromaniac progresses.
public class MentalStateWorker_FireStartingSpree : MentalStateWorker
{
    public override bool StateCanOccur(Pawn pawn)
    {
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.RecoveringPyromaniac);
        if (hediff == null)
        {
            return true;
        }
        float severity = hediff.Severity;
        float maxSeverity = hediff.def.maxSeverity;
        return Rand.Chance(1f - severity / maxSeverity);
    }
}
