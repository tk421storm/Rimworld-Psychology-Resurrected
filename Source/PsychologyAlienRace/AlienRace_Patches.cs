using UnityEngine;
using RimWorld;
using Verse;
using System;
using Verse.Sound;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

namespace Psychology;

/// <summary>
/// Humanoid Alien Races compatibility (loaded only with HAR, see LoadFolders.xml). Fills in the two empty hooks in the
/// main assembly's AlienRaceHooks: HeuristicSettings (psyche on/off from HAR's IsSentient; no age-gap preferences for
/// ageless or non-flesh races) and LovinAgeCurve (the race's own lovin-interval-by-age curve).
/// The patches are applied from a Mod constructor: SpeciesHelper works out every race's default settings in a
/// [StaticConstructorOnStartup], and those run for the main assembly before this one, so a static constructor here would
/// be too late. AlienRaceHooks has no static state, so patching it this early doesn't run any other class constructor.
/// </summary>
public class AlienRaceCompatibilityMod : Mod
{
    public AlienRaceCompatibilityMod(ModContentPack content) : base(content)
    {
        HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate.AlienRace");
        harmonyInstance.Patch(AccessTools.Method(typeof(AlienRaceHooks), nameof(AlienRaceHooks.HeuristicSettings)),
            postfix: new HarmonyMethod(typeof(AlienRace_Patches), nameof(AlienRace_Patches.AlienRaceHeuristicSettingsPostfix)));
        harmonyInstance.Patch(AccessTools.Method(typeof(AlienRaceHooks), nameof(AlienRaceHooks.LovinAgeCurve)),
            postfix: new HarmonyMethod(typeof(AlienRace_Patches), nameof(AlienRace_Patches.AlienRaceAgeCurvePostfix)));
        Log.Message("Psychology: applied Humanoid Alien Races patches");
    }
}

public static class AlienRace_Patches
{
    public static void AlienRaceHeuristicSettingsPostfix(SpeciesSettings settings, ThingDef def)
    {
        if (def is AlienRace.ThingDef_AlienRace alienDef)
        {
            if (alienDef?.alienRace?.compatibility?.IsSentient is bool isSentient)
            {
                settings.enablePsyche = isSentient;
            }

            bool? immuneToAge = alienDef?.alienRace?.generalSettings?.immuneToAge;
            bool? notFlesh = !alienDef?.alienRace?.compatibility?.IsFlesh;
            if (immuneToAge == true || notFlesh == true)
            {
                settings.enableAgeGap = false;
            }
        }
    }

    // The race's own lovin interval curve, or null to fall back to vanilla's.
    public static void AlienRaceAgeCurvePostfix(ref SimpleCurve __result, Pawn pawn)
    {
        if (pawn?.def is AlienRace.ThingDef_AlienRace alienDef && alienDef?.alienRace?.generalSettings?.lovinIntervalHoursFromAge is SimpleCurve curve)
        {
            __result = curve;
        }
    }

}
