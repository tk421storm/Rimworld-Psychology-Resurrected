using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.Diagnostics;
using UnityEngine;

namespace Psychology;

// Brain "surgery" that removes Anxiety outright on success (uses normal surgery success rules).
public class Recipe_CureAnxiety : Recipe_Surgery
{
    public Recipe_CureAnxiety() : base()
    {
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        if (!CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
        {
            TaleRecorder.RecordTale(TaleDefOfPsychology.CuredAnxiety, new object[]
            {
                billDoer,
                pawn
            });
            Hediff anxiety = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety);
            if (anxiety != null)
            {
                pawn.health.RemoveHediff(anxiety);
            }
            if (PawnUtility.ShouldSendNotificationAbout(pawn) || PawnUtility.ShouldSendNotificationAbout(billDoer))
            {
                Messages.Message("MessageCuredAnxiety".Translate(billDoer.Named("SURGEON"), pawn.Named("PATIENT")), pawn, MessageTypeDefOf.PositiveEvent);
            }
            return;
        }
    }

    [DebuggerHidden]
    public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
    {
        if (pawn.RaceProps.Humanlike && pawn.health.hediffSet.HasHediff(HediffDefOfPsychology.Anxiety))
        {
            List<BodyPartRecord> brain = new List<BodyPartRecord>();
            brain.Add(pawn.health.hediffSet.GetBrain());
            return brain;
        }
        return new List<BodyPartRecord>();
    }
}
