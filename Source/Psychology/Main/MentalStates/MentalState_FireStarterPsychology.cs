using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Psychology;

// Vanilla fire-starting spree, but treated pyromaniacs (RecoveringPyromaniac) snap out of it sooner the further along
// their recovery is.
public class MentalState_FireStartingSpreePsychology : MentalState_FireStartingSpree
{
    Hediff hediff = null;

    public override void PreStart()
    {
        hediff = this.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.RecoveringPyromaniac);
        base.PreStart();
    }

    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);
        if (hediff == null || this.pawn.IsHashIntervalTick(30) != true)
        {
            return;
        }
        float severity = hediff.Severity;
        if (severity == 0f)
        {
            return;
        }
        float maxSeverity = hediff.def.maxSeverity;
        // This ensures that the effective overall mtbDays is recoveryMtbDays * (1 - severity / maxSeverity)
        float mtbDays = this.def.recoveryMtbDays * (maxSeverity / severity - 1f);
        if (mtbDays == 0f || Rand.MTBEventOccurs(mtbDays, 60000, 30))
        {
            base.RecoverFromState();
        }
    }
}
