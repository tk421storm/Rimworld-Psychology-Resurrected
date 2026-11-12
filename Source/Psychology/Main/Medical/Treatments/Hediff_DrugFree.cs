using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// DrugFree hediff (result of treating chemical interest/fascination). New drug binges are blocked by
// MentalStateWorker_BingingDrugPatch; this also ends one that's already running.
public class Hediff_DrugFree : HediffWithComps
{
    // Tooltip text naming the pawn's drug desire (chemical interest or fascination).
    public override string Description
    {
        get
        {
            Trait drugDesire = pawn?.story?.traits?.GetTrait(TraitDefOf.DrugDesire);
            if (drugDesire == null || drugDesire.Degree <= 0)
            {
                return "DrugFreeDescriptionNoTrait".Translate(pawn.Named("PAWN"));
            }
            return "DrugFreeDescription".Translate(pawn.Named("PAWN"), drugDesire.Label.Named("TRAIT"));
        }
    }

    public override void PostTickInterval(int delta)
    {
        base.PostTickInterval(delta);
        if (pawn.IsHashIntervalTick(250, delta) && pawn.MentalState is MentalState_BingingDrug binge)
        {
            binge.RecoverFromState();
        }
    }
}
