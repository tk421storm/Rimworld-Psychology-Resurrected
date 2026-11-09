using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Psychology;

// Requires Anxiety and a pawn on a map or in a caravan; tended anxiety blocks the attack with probability 1 - e^-tendQuality.
public class MentalStateWorker_PanicAttack : MentalStateWorker
{
    public override bool StateCanOccur(Pawn pawn)
    {
        // Not for world pawns (off every map and not in a caravan).
        if (!pawn.Spawned && !pawn.IsCaravanMember())
        {
            return false;
        }
        Hediff_Anxiety hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) as Hediff_Anxiety;
        if (hediff == null)
        {
            return false;
        }
        if (hediff.IsTended() != true)
        {
            return base.StateCanOccur(pawn);
        }
        HediffComp_TendDuration tendDuration = hediff.TryGetComp<HediffComp_TendDuration>();
        float tendQuality = tendDuration.tendQuality;
        if (Rand.Value > Mathf.Exp(-tendQuality))
        {
            return false;
        }
        return base.StateCanOccur(pawn);
    }
}

