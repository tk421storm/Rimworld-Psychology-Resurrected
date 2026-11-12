using RimWorld;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using System.Linq;

namespace Psychology.Harmony;

// Vanilla Psycasts Expanded compatibility: VPE's Word of Love checks the vanilla Gay/Bisexual/Asexual traits, which
// Psychology turns into Kinsey ratings when Kinsey is enabled (so every pawn would count as straight and nobody as
// asexual). With Kinsey on, these patches check the Kinsey rating instead, the same way the patches on vanilla's Word of
// Love do (CompAbilityEffect_WordOfLove_KinseyEnabledPatches).
[StaticConstructorOnStartup]
public static class VanillaPsycastsExpanded_Patches
{
    static VanillaPsycastsExpanded_Patches()
    {
        HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate.VanillaPsycastsExpanded");
        harmonyInstance.PatchAll();
        Log.Message("Psychology: completed patches for compatibility with Vanilla Psycasts Expanded.");
    }

    // Whether a pawn with this Kinsey rating can be attracted to the other pawn's gender.
    public static bool AttractedTo(Pawn pawn, Pawn other)
    {
        int kinsey = PsycheHelper.Comp(pawn).Sexuality.kinseyRating;
        return pawn.gender == other.gender ? kinsey > 0 : kinsey < 6;
    }
}

// The first target is the pawn who falls in love, the second the pawn they fall for.
[HarmonyPatch(typeof(VanillaPsycastsExpanded.AbilityExtension_WordOfLove), nameof(VanillaPsycastsExpanded.AbilityExtension_WordOfLove.ValidateTarget))]
public static class AbilityExtension_WordOfLove_ValidateTarget_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result, LocalTargetInfo target, VEF.Abilities.Ability ability, bool showMessages)
    {
        if (!PsychologySettings.enableKinsey)
        {
            return true;
        }
        Pawn inLovePawn = ability.currentTargets.FirstOrDefault(x => x.Thing != null).Thing as Pawn;
        Pawn lovedPawn = target.Pawn;
        if (inLovePawn == null || lovedPawn == null || inLovePawn == lovedPawn)
        {
            return true;
        }
        if (!PsycheHelper.PsychologyEnabled(inLovePawn) || !PsycheHelper.PsychologyEnabled(lovedPawn))
        {
            return true;
        }
        if (!SpeciesHelper.RomanceEnabled(inLovePawn, true) || !SpeciesHelper.RomanceEnabled(lovedPawn, true))
        {
            // No underage dating
            __result = false;
            return false;
        }
        if (!VanillaPsycastsExpanded_Patches.AttractedTo(inLovePawn, lovedPawn))
        {
            if (showMessages)
            {
                Messages.Message("AbilityCantApplyWrongAttractionGender".Translate(inLovePawn, lovedPawn), inLovePawn, MessageTypeDefOf.RejectInput, historical: false);
            }
            __result = false;
            return false;
        }
        if (showMessages && !VanillaPsycastsExpanded_Patches.AttractedTo(lovedPawn, inLovePawn))
        {
            // Allowed, but the loved pawn won't return the feelings
            Messages.Message("AbilityCantApplyWrongAttractionGender".Translate(lovedPawn, inLovePawn), lovedPawn, MessageTypeDefOf.CautionInput, historical: false);
        }
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(VanillaPsycastsExpanded.AbilityExtension_WordOfLove), nameof(VanillaPsycastsExpanded.AbilityExtension_WordOfLove.Valid))]
public static class AbilityExtension_WordOfLove_Valid_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result, GlobalTargetInfo[] targets, VEF.Abilities.Ability ability, bool throwMessages)
    {
        if (!__result || !PsychologySettings.enableKinsey)
        {
            return;
        }
        foreach (GlobalTargetInfo target in targets)
        {
            Pawn pawn = target.Thing as Pawn;
            if (!PsycheHelper.PsychologyEnabled(pawn) || !PsycheHelper.Comp(pawn).Sexuality.IsAsexual)
            {
                continue;
            }
            if (throwMessages)
            {
                Messages.Message("AbilityCantApplyOnAsexual".Translate(ability.def.label), pawn, MessageTypeDefOf.RejectInput, historical: false);
            }
            __result = false;
            return;
        }
    }
}
