using Verse;
using RimWorld;
using System;

namespace Psychology;

// Sedentary trait: happy after staying in roughly the same spot for an hour (more so on a comfortable seat).
public class ThoughtWorker_Sedentary : ThoughtWorker
{
    private class LastMove
    {
        public IntVec3 position;
        public int tick;
    }

    // One ThoughtWorker serves every pawn, so where each pawn last moved is kept per pawn.
    private readonly PawnStateCache<LastMove> lastMoves = new PawnStateCache<LastMove>();

    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!p.Spawned)
        {
            return ThoughtState.Inactive;
        }
        if (!p.RaceProps.Humanlike)
        {
            return ThoughtState.Inactive;
        }
        if (!p.Awake())
        {
            return ThoughtState.Inactive;
        }  
        if (!p.story.traits.HasTrait(TraitDefOfPsychology.Sedentary))
        {
            return ThoughtState.Inactive;
        }
            
        LastMove lastMove = lastMoves.For(p, out bool created);
        if (created || (lastMove.position - p.Position).LengthHorizontalSquared > 80f)
        {
            lastMove.position = p.Position;
            lastMove.tick = Find.TickManager.TicksGame;
        }
        if ((Find.TickManager.TicksGame - lastMove.tick) > GenDate.TicksPerHour)
        {
            Building edifice = GridsUtility.GetEdifice(p.Position, p.Map);
            if (edifice != null && edifice.GetStatValue(StatDefOf.Comfort, true) >= 0.75f)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            return ThoughtState.ActiveAtStage(0);
        }
        return ThoughtState.Inactive;
    }
}
