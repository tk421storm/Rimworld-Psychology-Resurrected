using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology;

// "Treatment for X failed" memory; remembers which treatment so Recipe_Treatment can block retrying it while it lasts.
public class Thought_TreatmentFailed : Thought_Memory
{
    public TreatmentRecipeDef treatmentRecipe;

    string IllnessNameTranslated => treatmentRecipe == null ? "MentalIllness".Translate() : treatmentRecipe.illnessName;
    public override string LabelCap => string.Format(base.CurStage.label, IllnessNameTranslated).CapitalizeFirst();
    public override string Description => string.Format(base.Description, IllnessNameTranslated).CapitalizeFirst();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref treatmentRecipe, "treatment");
    }

}
