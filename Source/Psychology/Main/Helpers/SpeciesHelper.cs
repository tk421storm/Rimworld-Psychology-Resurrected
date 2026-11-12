using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;
using System.Xml.Linq;
using System.Runtime;
using System.Security.Cryptography;

namespace Psychology;

/// <summary>
/// Decides which races get a psyche and with which romance rules, and wires Psychology into those race defs.
///
/// At startup (static constructor) every pawn ThingDef with humanlike intelligence gets a SpeciesSettings entry in
/// PsychologySettings.speciesDict (unless the player already saved one) and has Psychology added to its def:
/// CompPsychology, the Psyche inspector tab (also on its corpse), the treatment operations, and the
/// "OrganicPsychology" hediff givers (Anxiety etc.) next to OrganicStandard.
///
/// Default settings per species (DefaultSettingsForSpeciesDef): an explicit SpeciesSettingsDef if a mod provides one;
/// otherwise name heuristics ("Robot"/"AIPawn"/zombies -> no psyche, "Android" -> no age rules, "Elf" -> no age gap),
/// HAR hints via AlienRaceHooks.HeuristicSettings, and minimum dating/lovin ages interpolated from the race's
/// teenage/adult life stages so that they match human 14/16 on the human life-stage scale.
/// </summary>
[StaticConstructorOnStartup]
public class SpeciesHelper
{
  public static HashSet<ThingDef> registeredSpecies = new HashSet<ThingDef>();
  public static List<string> mindlessList = new List<string>() { "ChjDroid", "ChjBattleDroid", "Android1Tier", "M7Mech", "M8Mech" };
  public static List<string> androidLikeList = new List<string>() { "ChjAndroid", "Android2Tier", "Android3Tier", "Android4Tier", "Android5Tier" };
  public static List<string> elfLikeList = new List<string>() { };
  public static List<string> mindlessSubstringList = new List<string> { "Robot", "AIPawn" };
  public static List<string> androidLikeSubstringList = new List<string> { "Android" };
  public static List<string> elfLikeSubstringList = new List<string>() { "Elf" };

  // Presets: (enablePsyche, enableAgeGap, minDatingAge, minLovinAge). -1 = never, 0 = no age limit.
  public static SpeciesSettings mindlessSettings = new SpeciesSettings(false, false, -1f, -1f);
  public static SpeciesSettings androidLikeSettings = new SpeciesSettings(true, false, 0f, 0f);
  public static SpeciesSettings elfLikeSettings = new SpeciesSettings(EnablePsyche: true, EnableAgeGap: false);
  public static SpeciesSettings animalLikeSettings = new SpeciesSettings(true, true, -1f, -1f);

  public static ThinkTreeDef zombieThinkTree;
  public static bool zombieNotNull;

  static SpeciesHelper()
  {
    zombieThinkTree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Zombie");
    zombieNotNull = zombieThinkTree != null;
    foreach (ThingDef t in DefDatabase<ThingDef>.AllDefs)
    {
      if (CheckIntelligenceAndAddEverythingToSpeciesDef(t, checkIntelligence: true, register: false, allowAddComp: true) != true)
      {
        continue;
      }
      if (registeredSpecies.Add(t))
      {
        Log.Message("Psychology, registered species: " + t);
      }
    }
    SettingsWindowUtility.SaveBackupOfSettings();
    SettingsWindowUtility.Initialize();
  }

  // Things that need to be done by these methods:
  // - Check intelligence
  // - Add everything except comp psychology to ThingDef
  // - Add comp psychology to ThingDef
  // - Create settings for ThingDef
  // - Add ThingDef to registered species
  // Principles:
  // - If we register a thingdef, we want to create settings for it
  // True for races whose intelligence is Humanlike (Animal/ToolUser races never get a psyche).
  public static bool IsHumanlikeIntelligence(ThingDef thingDef)
  {
    Intelligence? intelligence = thingDef?.race?.intelligence;
    if (intelligence == null)
    {
      return false;
    }
    if ((int)intelligence < (int)Intelligence.Humanlike)
    {
      return false;
    }
    return true;
  }

  // Registers a pawn race (if humanlike) and adds Psychology's comps/tab/recipes/hediff givers to its def. Also called
  // lazily from PsycheHelper.HasLatentPsyche for races that appear after startup.
  public static bool CheckIntelligenceAndAddEverythingToSpeciesDef(ThingDef thingDef, bool checkIntelligence = true, bool register = true, bool allowAddComp = true)
  {
    if (thingDef.category != ThingCategory.Pawn)
    {
      return false;
    }
    if (checkIntelligence && IsHumanlikeIntelligence(thingDef) != true)
    {
      return false;
    }
    string defName = thingDef.defName;
    if (PsychologySettings.speciesDict.ContainsKey(defName) != true)
    {
      PsychologySettings.speciesDict[defName] = DefaultSettingsForSpeciesDef(thingDef);
    }
    if (register)
    {
      if (registeredSpecies.Add(thingDef))
      {
        AddEverythingExceptCompPsychology(thingDef);
        if (allowAddComp)
        {
          AddCompPsychologyToHumanlikeDef(thingDef);
        }
        SettingsWindowUtility.SaveBackupOfSettings();
        SettingsWindowUtility.Initialize();
      }
    }
    else
    {
      AddEverythingExceptCompPsychology(thingDef);
      if (allowAddComp)
      {
        AddCompPsychologyToHumanlikeDef(thingDef);
      }
    }
    return true;
  }

  // To be used after game (save file) is loaded
  // Settings for a race, creating default ones (and registering the race) on first use.
  public static SpeciesSettings GetOrMakeSpeciesSettingsFromThingDef(ThingDef pawnDef)
  {
    if (PsychologySettings.speciesDict.TryGetValue(pawnDef.defName, out SpeciesSettings settings) != true)
    {
      settings = DefaultSettingsForSpeciesDef(pawnDef);
      PsychologySettings.speciesDict[pawnDef.defName] = settings;
    }
    if (registeredSpecies.Add(pawnDef))
    {
      AddEverythingExceptCompPsychology(pawnDef);
      if (IsHumanlikeIntelligence(pawnDef) == true)
      {
        AddCompPsychologyToHumanlikeDef(pawnDef);
      }
      SettingsWindowUtility.SaveBackupOfSettings();
      SettingsWindowUtility.Initialize();
    }
    return settings;
  }

  public static void AddCompPsychologyToHumanlikeDef(ThingDef humanlikeDef)
  {
    if (humanlikeDef.comps == null)
    {
      humanlikeDef.comps = new List<CompProperties>(1);
    }
    humanlikeDef.comps.AddDistinct(new CompProperties_Psychology());
  }

  // Psyche tab, treatment operations (therapy for pyromania, chemical interest/fascination, depression, insomnia, and
  // the anxiety cure) and, for organic races, Psychology's hediff givers.
  public static void AddEverythingExceptCompPsychology(ThingDef pawnDef)
  {
    AddInspectorTabToDefAndCorpseDef(pawnDef);
    if (pawnDef.recipes == null)
    {
      pawnDef.recipes = new List<RecipeDef>(6);
    }
    // AllRecipes is cached on first use, so add to it as well in case this runs after it was built (e.g. a race first
    // registered mid-game). Every TreatmentRecipeDef, so compat ones (Common/Mods/VanillaTraitsExpanded) are included.
    foreach (RecipeDef recipe in DefDatabase<TreatmentRecipeDef>.AllDefs.Cast<RecipeDef>().Prepend(RecipeDefOfPsychology.CureAnxiety))
    {
      pawnDef.recipes.AddDistinct(recipe);
      pawnDef.AllRecipes.AddDistinct(recipe);
    }

    if (!pawnDef.race.hediffGiverSets.NullOrEmpty())
    {
      if (pawnDef.race.hediffGiverSets.Contains(DefDatabase<HediffGiverSetDef>.GetNamed("OrganicStandard")))
      {
        pawnDef.race.hediffGiverSets.AddDistinct(DefDatabase<HediffGiverSetDef>.GetNamed("OrganicPsychology"));
      }
    }
  }

  public static void AddInspectorTabToDefAndCorpseDef(ThingDef t)
  {
    // Add to both lists: vanilla rebuilds inspectorTabsResolved from inspectorTabs when the def is resolved, and the
    // resolved list is what's shown if the def was already resolved.
    if (t.inspectorTabs == null)
    {
      t.inspectorTabs = new List<Type>(1);
    }
    t.inspectorTabs.AddDistinct(typeof(ITab_Pawn_Psyche));
    if (t.inspectorTabsResolved == null)
    {
      t.inspectorTabsResolved = new List<InspectTabBase>(1);
    }
    t.inspectorTabsResolved.AddDistinct(InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_Psyche)));
    if (t.race?.corpseDef == null)
    {
      Log.Warning("thingDef.race?.corpseDef == null for thingDef = " + t.defName);
      return;
    }
    if (t.race.corpseDef.inspectorTabsResolved == null)
    {
      t.race.corpseDef.inspectorTabsResolved = new List<InspectTabBase>(1);
    }
    t.race.corpseDef.inspectorTabsResolved.AddDistinct(InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_Psyche)));
  }

  public static void ResetSpeciesDict(Dictionary<string, SpeciesSettings> speciesDict)
  {
    foreach (ThingDef def in registeredSpecies)
    {
      speciesDict[def.defName] = DefaultSettingsForSpeciesDef(def);
    }
    SettingsWindowUtility.Initialize();
  }

  // See the class summary for the heuristics. Humans always get the plain defaults (psyche on, age gap on, 14 / 16).
  public static SpeciesSettings DefaultSettingsForSpeciesDef(ThingDef def)
  {
    if (def == null)
    {
      Log.Error("DefaultSettingsForSpeciesDef, ThingDef was null");
      return new SpeciesSettings();
    }
    // Allow XML override
    string name = def.defName;
    SpeciesSettingsDef speciesDef = DefDatabase<SpeciesSettingsDef>.GetNamedSilentFail(name);
    if (speciesDef != null)
    {
      return new SpeciesSettings(speciesDef.enablePsyche, speciesDef.enableAgeGap, speciesDef.minDatingAge, speciesDef.minLovinAge);
    }
    // Start with default
    SpeciesSettings settings = new SpeciesSettings();
    // Set human to default
    if (name == "Human")
    {
      return settings;
    }
    // No beastiality please
    if (IsHumanlikeIntelligence(def) != true)
    {
      return new SpeciesSettings(animalLikeSettings);
    }
    // Use explicit defNames from well-known mods
    if (mindlessList.Contains(name))
    {
      return new SpeciesSettings(mindlessSettings);
    }
    if (androidLikeList.Contains(name))
    {
      return new SpeciesSettings(androidLikeSettings);
    }
    if (elfLikeList.Contains(name))
    {
      return new SpeciesSettings(elfLikeSettings);
    }
    // Use heuristics based on defNames
    if (mindlessSubstringList.Exists(entry => name.Contains(entry)) || (zombieNotNull && def.race.thinkTreeMain == zombieThinkTree))
    {
      settings.enablePsyche = false;
    }
    if (androidLikeSubstringList.Exists(entry => name.Contains(entry)))
    {
      settings.enableAgeGap = false;
      settings.minDatingAge = 0f;
      settings.minLovinAge = 0f;
    }
    if (elfLikeSubstringList.Exists(entry => name.Contains(entry)))
    {
      settings.enableAgeGap = false;
    }
    if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces"))
    {
      AlienRaceHooks.HeuristicSettings(settings, def);
    }
    if (def.race?.lifeStageAges != null)
    {

      bool foundTeen = false;
      bool foundAdult = false;
      float ageTeen = -1f;
      float ageAdult = -1f;
      foreach (LifeStageAge lifeStageAge in def.race.lifeStageAges)
      {
        if (lifeStageAge?.def?.defName == null || lifeStageAge?.minAge == null)
        {
          continue;
        }
        if (lifeStageAge.def.defName.Contains("Teenage"))
        {
          ageTeen = Mathf.Max(ageTeen, lifeStageAge.minAge);
          foundTeen = true;
        }
        if (lifeStageAge.def.defName.Contains("Adult"))
        {
          ageAdult = Mathf.Max(ageAdult, lifeStageAge.minAge);
          foundAdult = true;
        }
      }
      if (foundAdult)
      {
        if (foundTeen)
        {
          settings.minDatingAge = ageTeen + (ageAdult - ageTeen) * (14f - 13f) / (18f - 13f);
          settings.minLovinAge = ageTeen + (ageAdult - ageTeen) * (16f - 13f) / (18f - 13f);
        }
        else
        {
          settings.minDatingAge = ageAdult * 14f / 18f;
          settings.minLovinAge = ageAdult * 16f / 18f;
        }
      }
      else
      {
        settings.minDatingAge = -1f;
        settings.minLovinAge = -1f;
      }
    }
    return settings;
  }

  // Earliest age at which the race's life stages allow romance: the teenage stage's start (scaled 12/13) for dating, or
  // 15/18 of the adult stage's start. False if the race has no recognisable teenage/adult stages.
  public static bool MinLifestageAge(ThingDef def, bool isTeen, out float age)
  {
    age = -1f;
    if (def?.race?.lifeStageAges == null)
    {
      return false;
    }
    bool foundTeen = false;

    if (isTeen)
    {
      foreach (LifeStageAge lifeStageAge in def.race.lifeStageAges)
      {
        if (lifeStageAge?.minAge != null && lifeStageAge.def.defName.Contains("Teenage"))
        {
          age = Mathf.Max(age, lifeStageAge.minAge) * 12f / 13f;
          foundTeen = true;
        }
      }
      if (foundTeen)
      {
        return true;
      }
    }
    if (def.race.lifeStageAges.Exists(x => x?.minAge != null && x.def.defName.Contains("Adult")))
    {
      age = def.race.lifeStageAges.First(x => x?.minAge != null && x.def.defName.Contains("Adult")).minAge * 15f / 18f;
      return true;
    }
    return false;
  }

  /// <summary>
  /// Uses both lifestage and settings info to determine whether <paramref name="pawn"/> can participate in dating if <paramref name="isDating"/> is true, or lovin if false.
  /// </summary>
  /// <param name="pawn">The pawn in question</param>
  /// <param name="isDating">Checks dating if true, and lovin if false.</param>
  /// <returns>True or False, with True meaning that the <paramref name="pawn"/> can participate in dating/lovin.</returns>
  // Master age/species gate for romance (isDating = dating/romance attempts, false = lovin): the race's life stages and
  // the player's species settings both have to allow it.
  public static bool RomanceEnabled(Pawn pawn, bool isDating)
  {
    return RomanceLifestageAgeCheck(pawn, isDating) && RomanceSettingsAgeCheck(pawn, isDating);
  }

  /// <summary>
  /// Uses lifestage info to determine whether <paramref name="pawn"/> can participate in dating if <paramref name="isDating"/> is true, or lovin if false.
  /// </summary>
  /// <param name="pawn">The pawn in question</param>
  /// <param name="isDating">Checks dating if true, and lovin if false.</param>
  /// <returns>True or False, with True meaning that the <paramref name="pawn"/> can participate in dating/lovin.</returns>
  // Life-stage half of RomanceEnabled: no psyche / juvenile / below the stage age -> false.
  public static bool RomanceLifestageAgeCheck(Pawn pawn, bool isDating)
  {
    if (!PsycheHelper.PsychologyEnabled(pawn))
    {
      return false;
    }
    if (pawn.DevelopmentalStage.Juvenile())
    {
      return false;
    }
    // Allow teenagers to date but not do lovin
    if (!MinLifestageAge(pawn.def, isDating, out float minLifestageAge))
    {
      return false;
    }
    if (pawn?.ageTracker?.AgeBiologicalYearsFloat is float bioAge && bioAge < minLifestageAge)
    {
      return false;
    }
    if (pawn?.ageTracker?.CurLifeStage is LifeStageDef lifeStageDef && lifeStageDef == LifeStageDefOf.HumanlikeAdult)
    {
      return true;
    }
    if (!isDating && pawn?.ageTracker?.Adult != true)
    {
      return false;
    }
    return true;
  }

  /// <summary>
  /// Uses settings info to determine whether <paramref name="pawn"/> can participate in dating if <paramref name="isDating"/> is true, or lovin if false.
  /// </summary>
  /// <param name="pawn">The pawn in question</param>
  /// <param name="isDating">Checks dating if true, and lovin if false.</param>
  /// <returns>True or False, with True meaning that the <paramref name="pawn"/> can participate in dating/lovin.</returns>
  private static bool RomanceSettingsAgeCheck(Pawn pawn, bool isDating)
  {
    if (!PsycheHelper.PsychologyEnabled(pawn))
    {
      return false;
    }
    SpeciesSettings settings = GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
    if (!settings.enablePsyche)
    {
      return false;
    }
    float minSettingsAge = isDating ? settings.minDatingAge : settings.minLovinAge;
    if (minSettingsAge < 0f)
    {
      return false;
    }
    if (minSettingsAge == 0f)
    {
      return true;
    }
    return pawn.ageTracker != null && minSettingsAge < pawn.ageTracker.AgeBiologicalYearsFloat && 0f < minSettingsAge;
  }


}
