using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// Properties for CompPsychology. Added to humanlike race defs in code by SpeciesHelper, not in XML.
public class CompProperties_Psychology : CompProperties
{
    public CompProperties_Psychology()
    {
        this.compClass = typeof(CompPsychology);
    }
}
