using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace Psychology;

public class Hediff_Anxiety : HediffWithComps
{
    /// <summary>
    /// Anxiety (the mod's PTSD-like condition, usually gained from mental breaks, see MentalBreaker_AnxietyPatch). Its
    /// severity also drags the LaidBack node down (Pawn_PsycheTracker.AdjustForCircumstance).
    /// Awake pawns get panic attacks from the stages' vanilla mentalStateGivers. Sleeping pawns get this roll instead: 20
    /// times a day (outside a 1-day cooldown after each attack) it rolls the stage's panic attack MTB (PanicAttackMtbDays);
    /// on a hit while the pawn is ASLEEP they get a nightmare and wake into a panic attack. Treated by the CureAnxiety operation (Recipe_CureAnxiety); tending reduces how often attacks happen.
    /// </summary>
    public const int intervalsPerDay = 20;
    public const int ticksPerInterval = GenDate.TicksPerDay / intervalsPerDay;
    public int ticksPerIntervalTracker = Mathf.CeilToInt(ticksPerInterval * Rand.Value);
    public int cooldownIntervalTracker = 0;
    // When the last panic attack started (TicksGame), for the debug log; -1 if none yet.
    public int lastAttackTick = -1;

    // The current stage's panic attack MTB (its PanicAttack mentalStateGiver), or -1 if this stage has none.
    public float PanicAttackMtbDays
    {
        get
        {
            List<MentalStateGiver> givers = CurStage?.mentalStateGivers;
            if (givers != null)
            {
                foreach (MentalStateGiver giver in givers)
                {
                    if (giver.mentalState == MentalStateDefOfPsychology.PanicAttack)
                    {
                        return giver.mtbDays;
                    }
                }
            }
            return -1f;
        }
    }

    public override void Tick()
    {
        base.Tick();
        if (!pawn.Spawned && !pawn.IsCaravanMember())
        {
            return;
        }
        if (ticksPerIntervalTracker > 0)
        {
            ticksPerIntervalTracker--;
            return;
        }
        ticksPerIntervalTracker = ticksPerInterval;
        if (cooldownIntervalTracker > 0)
        {
            cooldownIntervalTracker--;
            return;
        }
        if (pawn.InMentalState == true)
        {
            return;
        }
        // Vanilla's mentalStateGivers can't start a mental state in a sleeping pawn, so this roll covers sleep.
        float mtbDays = PanicAttackMtbDays;
        if (mtbDays <= 0f)
        {
            return;
        }
        if (Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, ticksPerInterval) != true)
        {
            return;
        }
        if (pawn.jobs?.curDriver?.asleep == true)
        {
            // MentalState_PanicAttack.PreStart gives the nightmare memory
            pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOfPsychology.PanicAttack, "nightmare", forceWake: true);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref cooldownIntervalTracker, "cooldownIntervalTracker", 0);
        Scribe_Values.Look(ref lastAttackTick, "lastAttackTick", -1);
    }
}
