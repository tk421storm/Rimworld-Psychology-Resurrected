using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.Diagnostics;
using UnityEngine;

namespace Psychology;

/// <summary>
/// Generic psychiatric treatment operation (TreatmentRecipeDefs in Recipes_Psychology.xml): pyromania, chemical
/// interest/fascination, depression (Natural Mood -1/-2, Tortured Artist) and insomnia. On success the patient gets the
/// recipe's permanent "treated" hediff (RecoveringPyromaniac, DrugFree, Antidepressants, SleepingPills), which the
/// relevant code checks to suppress the trait's effects; the trait itself stays.
/// Success = 1 - e^-(min(2 x surgery success, 1) x doctor's Social Impact x patient's comfort x easeOfTreatment).
/// Patients without a comfort need (guests, the Ideology "comfort ignored" precept) count the comfort of their bed.
/// A failed attempt gives a Thought_TreatmentFailed that blocks retrying that recipe while it lasts.
/// </summary>
public class Recipe_Treatment : Recipe_Surgery
{
    public TreatmentRecipeDef TreatmentRecipe => recipe as TreatmentRecipeDef;
    public List<Treatment> Treatments => TreatmentRecipe == null ? null : TreatmentRecipe.treatments;

    public bool CheckTreatmentFail(Pawn surgeon, Pawn patient)
    {
        Treatment treatment = GetAppropriateTreatment(patient);
        if (treatment == null)
        {
            Log.Error("Psychology: CheckTreatmentFail, treatment was null");
            return true;
        }
        float num = 1f;
        float num2 = surgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true);
        float num3 = surgeon.GetStatValue(StatDefOf.SocialImpact, true);
        float num4 = Comfort(patient);
        num *= Mathf.Min(2f * num2, 1f);
        num *= num3;
        num *= num4;
        num *= treatment.easeOfTreatment;
        num = 1f - Mathf.Exp(-num);
        return num < Rand.Value;
    }

    // The patient's comfort, or for one without the need, the comfort of the bed they lie in (0.5 if none).
    private static float Comfort(Pawn patient)
    {
        if (patient.needs?.comfort != null)
        {
            return patient.needs.comfort.CurLevel;
        }
        Building_Bed bed = patient.CurrentBed();
        return bed != null ? Mathf.Clamp01(bed.GetStatValue(StatDefOf.Comfort)) : 0.5f;
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        Treatment treatment = GetAppropriateTreatment(pawn);
        if (treatment == null)
        {
            Log.Error("Recipe_Treatment.ApplyOnPawn, treatmentDef is null");
            return;
        }
        HediffDef hediffDef = TreatmentRecipe.hediff;
        string illnessName = TreatmentRecipe.illnessName;

        if (CheckTreatmentFail(billDoer, pawn) == false)
        {
            TaleRecorder.RecordTale(TreatmentRecipe.tale, new object[]
            {
                billDoer,
                pawn
            });
            if (PawnUtility.ShouldSendNotificationAbout(pawn) || PawnUtility.ShouldSendNotificationAbout(billDoer))
            {
                Messages.Message("TreatedTrait".Translate(pawn, illnessName), pawn, MessageTypeDefOf.PositiveEvent);
            }
            Hediff recover = HediffMaker.MakeHediff(hediffDef, pawn, pawn.health.hediffSet.GetBrain());
            recover.Tended(1f, 1f); // TEST IN 1.3
            pawn.health.AddHediff(recover);
            return;
        }
        // TreatmentFailed's thoughtClass is Thought_TreatmentFailed; the recipe it stores blocks retrying while it lasts.
        if (pawn.needs?.mood != null && ThoughtMaker.MakeThought(ThoughtDefOfPsychology.TreatmentFailed, null) is Thought_TreatmentFailed mem)
        {
            mem.treatmentRecipe = TreatmentRecipe;
            pawn.needs.mood.thoughts.memories.TryGainMemory(mem);
        }
        if (PawnUtility.ShouldSendNotificationAbout(pawn) || PawnUtility.ShouldSendNotificationAbout(billDoer))
        {
            string retryIn = ThoughtDefOfPsychology.TreatmentFailed.DurationTicks.ToStringTicksToPeriod();
            Messages.Message("TreatmentFailedMessage".Translate(pawn, illnessName, retryIn), pawn, MessageTypeDefOf.NegativeEvent);
        }

    }

    [DebuggerHidden]
    public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
    {
        List<BodyPartRecord> brain = new List<BodyPartRecord>();
        Treatment treatment = GetAppropriateTreatment(pawn);
        if (treatment == null)
        {
            return brain;
        }
        if (pawn.health.hediffSet.HasHediff(TreatmentRecipe.hediff) == true)
        {
            return brain;
        }

        foreach (Thought_Memory m in pawn.needs?.mood?.thoughts.memories.Memories ?? Enumerable.Empty<Thought_Memory>())
        {
            if (m.def != ThoughtDefOfPsychology.TreatmentFailed)
            {
                continue;
            }
            if ((m is Thought_TreatmentFailed failed) != true)
            {
                continue;
            }
            if (failed.treatmentRecipe == TreatmentRecipe)
            {
                return brain;
            }
            if (failed.treatmentRecipe == null)
            {
                return brain;
            }
        }
        if (pawn.story.traits.HasTrait(treatment.trait, treatment.degree))
        {
            brain.Add(pawn.health.hediffSet.GetBrain());
            return brain;
        }
        return brain;
    }

    // The hardest-to-treat (lowest easeOfTreatment) entry matching one of the pawn's traits, or null.
    public Treatment GetAppropriateTreatment(Pawn pawn)
    {
        if (Treatments.NullOrEmpty() || pawn?.story?.traits == null)
        {
            return null;
        }
        List<Treatment> list = new List<Treatment>();
        bool foundOne = false;
        Treatment leastEasyTreatment = null;
        foreach (Treatment def in Treatments)
        {
            if (pawn.story.traits.HasTrait(def.trait, def.degree) != true)
            {
                continue;
            }
            if (foundOne == false)
            {
                leastEasyTreatment = def;
                foundOne = true;
                continue;
            }
            if (def.easeOfTreatment < leastEasyTreatment.easeOfTreatment)
            {
                leastEasyTreatment = def;
            }
        }
        return leastEasyTreatment;

    }

}
