using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Psychology;

// "Fell plotting" break: picks the colonist they hate most (opinion below -20), mutters threats about them, and after
// 15000 ticks may attack them (JobGiver_EnactPlot). Killing the target ends the break with a satisfied memory.
public class MentalState_FellPlotting : MentalState
{
    public Pawn target;
    public bool enactingPlot = false;

    public override RandomSocialMode SocialModeMax()
    {
        return RandomSocialMode.Off;
    }


    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);
        if (target == null)
        {
            IEnumerable<Pawn> rivals = (from c in pawn.Map.mapPawns.FreeColonistsSpawned
                                        where pawn.relations.OpinionOf(c) < -20
                                        orderby pawn.relations.OpinionOf(c) descending
                                        select c);
            if (rivals.Count() == 0)
            {
                Log.ErrorOnce(pawn.LabelShort + " was in Fell Plotting mental break but has no rivals.", 100 + pawn.GetHashCode());
                this.RecoverFromState();
                return;
            }
            target = rivals.First();
        }
        else if (enactingPlot && target.Dead)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.SuccessfulPlot, target);
            this.RecoverFromState();
            return;
        }
        if (pawn.IsHashIntervalTick(500) && !enactingPlot)
        {
            if (Rand.Value < 0.25f)
            {
                Vector3 pos = pawn.DrawPos + pawn.Drawer.renderer.BaseHeadOffsetAt(pawn.Rotation);
                MoteMaker.ThrowText(pos, pawn.Map, ("FellMurmuring" + Rand.RangeInclusive(1, 36)).Translate(target), Color.grey, 2.5f);
            }
        }
    }
}
