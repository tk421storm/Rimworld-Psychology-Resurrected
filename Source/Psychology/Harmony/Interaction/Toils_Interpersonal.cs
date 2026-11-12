using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Psychology.Harmony;

// Prisoner conversion (JobDriver_ConvertPrisoner) warms up with three vanilla Chitchat talks, while recruitment uses
// BuildRapport. With "adjust rapport based on compatibility" on, conversion talks build rapport too (scaled by the two
// pawns' compatibility, see Thought_RapportBuilt), so both warden jobs work the same way. Off: vanilla Chitchat.
// Conversion is the only vanilla caller that passes Chitchat here; recruitment passes nothing (= BuildRapport).
[HarmonyPatch(typeof(Toils_Interpersonal), nameof(Toils_Interpersonal.ConvinceRecruitee))]
public static class Toils_Interpersonal_ConvinceRecruitee_Patch
{
    [HarmonyPrefix]
    public static void UseRapportForConversion(Pawn talkee, ref InteractionDef interactionDef)
    {
        if (PsychologySettings.adjustRapportByCompatibility && interactionDef == InteractionDefOf.Chitchat && PsycheHelper.PsychologyEnabled(talkee))
        {
            interactionDef = InteractionDefOf.BuildRapport;
        }
    }
}
