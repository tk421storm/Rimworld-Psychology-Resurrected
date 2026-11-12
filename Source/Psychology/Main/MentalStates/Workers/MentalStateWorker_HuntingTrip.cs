using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    // Only for violent-capable, non-prisoner pawns who have Hunting enabled. Pawns that never work (visitors, traders,
    // raiders) have no work priorities, so asking about Hunting would log "did not have work settings initialized".
    public class MentalStateWorker_HuntingTrip : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            return pawn.Map != null && !pawn.IsPrisoner && !pawn.WorkTagIsDisabled(WorkTags.Violent) && pawn.workSettings?.EverWork == true && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hunting);
        }
    }
}
