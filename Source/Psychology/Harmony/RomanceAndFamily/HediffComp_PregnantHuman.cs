using HarmonyLib;
using RimWorld;
using Verse;

namespace Psychology.Harmony;

// Biotech's pregnancy attitude (happy or unhappy to be pregnant) drives the "new pregnancy" mood (+6 or -3, first
// trimester) and how a termination feels ("pregnancy terminated" or relief). Vanilla rolls it 50/50; here the chance
// of a happy pregnancy is the mother's family drive (FamilyUtility.FamilyDrive; about 50% for an average pawn), and the
// family traits decide it outright: a child-free pawn is never happy to be pregnant, a matriarch always is. The trait
// rule is applied when the attitude is read, so it also holds for pregnancies from before the trait or this version.
[HarmonyPatch(typeof(HediffComp_PregnantHuman), "SetupPregnancyAttitude")]
public static class HediffComp_PregnantHuman_SetupPregnancyAttitudePatch
{
    private static readonly AccessTools.FieldRef<HediffComp_PregnantHuman, PregnancyAttitude?> pregnancyAttitude =
        AccessTools.FieldRefAccess<HediffComp_PregnantHuman, PregnancyAttitude?>("pregnancyAttitude");

    [HarmonyPostfix]
    public static void FromFamilyDrive(HediffComp_PregnantHuman __instance)
    {
        Pawn mother = __instance.Pawn;
        if (mother == null || !PsycheHelper.PsychologyEnabled(mother))
        {
            return;
        }
        float chance = FamilyUtility.FamilyDrive(mother);
        pregnancyAttitude(__instance) = Rand.Chance(chance) ? PregnancyAttitude.Positive : PregnancyAttitude.Negative;
        PsychologyDebug.Log($"Pregnancy attitude of {mother}: {pregnancyAttitude(__instance)} (happy chance {chance:P0} from family drive)");
    }
}

[HarmonyPatch(typeof(HediffComp_PregnantHuman), nameof(HediffComp_PregnantHuman.Attitude), MethodType.Getter)]
public static class HediffComp_PregnantHuman_AttitudePatch
{
    [HarmonyPostfix]
    public static void FamilyTraits(HediffComp_PregnantHuman __instance, ref PregnancyAttitude? __result)
    {
        Pawn mother = __instance.Pawn;
        if (!__result.HasValue || mother == null)
        {
            return;
        }
        if (FamilyUtility.IsChildFree(mother))
        {
            __result = PregnancyAttitude.Negative;
        }
        else if (FamilyUtility.IsMatriarch(mother))
        {
            __result = PregnancyAttitude.Positive;
        }
    }
}
