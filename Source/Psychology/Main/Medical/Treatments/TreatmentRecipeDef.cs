using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.Diagnostics;
using UnityEngine;

namespace Psychology;

// RecipeDef for Recipe_Treatment: the hediff granted on success, a tale, the illness name for messages, and which
// trait (+ degree) each treatment applies to with how easy it is.
public class TreatmentRecipeDef : RecipeDef
{
    public HediffDef hediff;
    public TaleDef tale;
    public string illnessName;
    public List<Treatment> treatments;
}

public class Treatment
{
    public TraitDef trait;
    public int degree;
    public float easeOfTreatment;
}