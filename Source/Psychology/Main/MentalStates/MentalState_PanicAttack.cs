using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Psychology;

// Panic attack (from Anxiety): the pawn cowers in place (JobGiver_PanicAttack, from the PsychologyMentalStates think
// tree; flees first if danger is close). Every 250 ticks Anxiety severity creeps up a little;
// if the anxiety is tended, the attack can end early (likelier the better the tending). Ending it starts a one-day
// cooldown before the next attack.
public class MentalState_PanicAttack : MentalState
{
    public int mentalStateTicker = 0;
    public const int mentalStateTicksPerInterval = 250;

    public override void PreStart()
    {
        if (pawn.jobs?.curDriver?.asleep == true)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamNightmare);
        }
        base.PreStart();
    }

    public override void PostStart(string reason)
    {
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.PanicAttack);
        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) is Hediff_Anxiety anxiety)
        {
            if (PsychologyDebug.Enabled)
            {
                string since = anxiety.lastAttackTick < 0 ? "first logged attack"
                    : $"{(Find.TickManager.TicksGame - anxiety.lastAttackTick) / (float)GenDate.TicksPerDay:0.00} days since the last";
                PsychologyDebug.Log($"{pawn} panic attack ({reason ?? "no reason"}): anxiety {anxiety.CurStage.label} {anxiety.Severity:0.00}, "
                    + $"stage MTB {anxiety.PanicAttackMtbDays} days, tended {anxiety.IsTended()}, {since}");
            }
            anxiety.lastAttackTick = Find.TickManager.TicksGame;
        }
        base.PostStart(reason);
    }

    public override void PostEnd()
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOfPsychology.PanicAttack);
        PsychologyDebug.Log($"{pawn} panic attack ended after {age / (float)GenDate.TicksPerHour:0.0} hours");
        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) is Hediff_Anxiety anxiety)
        {
            // Give at least one day between anxiety attacks
            anxiety.cooldownIntervalTracker = Hediff_Anxiety.intervalsPerDay;
        }
        base.PostEnd();
    }

    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);
        if (mentalStateTicker > 0)
        {
            mentalStateTicker -= delta;
            return;
        }
        mentalStateTicker = mentalStateTicksPerInterval;
        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) is Hediff_Anxiety anxiety)
        {
            anxiety.Severity += 0.00000002f * mentalStateTicksPerInterval;
            // Tended anxiety: the attack ends with an MTB of the stage's panic attack MTB, shortened by tend quality.
            // A stage with no panic attacks (managed) ends it right away.
            if (anxiety.IsTended() == true)
            {
                HediffComp_TendDuration tendDuration = anxiety.TryGetComp<HediffComp_TendDuration>();
                float tendQuality = tendDuration.tendQuality;
                float stageMtbDays = anxiety.PanicAttackMtbDays;
                float mtbDays = stageMtbDays > 0f ? 1f / Mathf.Max(0.01f, Mathf.Exp(tendQuality) - 1f) * stageMtbDays : 0f;
                if (Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, mentalStateTicksPerInterval))
                {
                    RecoverFromState();
                }
            }
        }
    }
}