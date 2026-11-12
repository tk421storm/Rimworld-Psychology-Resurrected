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
using HarmonyLib;

namespace Psychology;

public enum KinseyMode
{
  Realistic,
  Uniform,
  Invisible,
  Gaypocalypse,
  Custom
};

// Things that a setting needs to do:
// Define a field in PsychologySettings
// Ref that field in ExposeData
// Define a default
// Define a backup
// Define a string buffer if the setting is a float
// Have an ability to iterate over all settings, at least those of a particular type
// Set cached and buffer from field
// Set backup from field
// Reset the field to the default
// Reset the field/cached to the backup
// Define a title and tooltip from a string name
/// <summary>
/// All mod settings as static fields, each with a matching *Default constant. Settings are saved/loaded by name via
/// reflection (ScribeValueAndAddToNameList), which also builds the name lists SettingsWindowUtility uses to draw and
/// reset them. speciesDict holds the per-race SpeciesSettings (see SpeciesHelper).
/// Gameplay meaning of the less obvious ones:
///   - conversationDuration: days a conversation opinion memory lasts
///   - convoMaxOpinionChange / convoMeanHours / convoTimeScaleHours / convoPersonalityEffectMultiplier: size, length and
///     personality sensitivity of conversation outcomes (Hediff_Conversation)
///   - imprisonedDebuff: strength of the "captured me" opinion recruited prisoners get
///   - traitOpinionMultiplier: scales vanilla trait-clash opinions (Teetotaler vs Chemical Interest etc.)
///   - personalityExtremeness: Big Five push away from average (PersonalityNodeMatrix.ApplyBigFiveProjections)
///   - ideoPsycheMultiplier: how strongly personality affects ideoligion certainty and conversion
/// </summary>
public class PsychologySettings : ModSettings
{
  public static HashSet<string> BoolSettingNameList = new HashSet<string>();
  public static HashSet<string> FloatSettingNameList = new HashSet<string>();
  public static HashSet<string> CombinedSettingNameList = new HashSet<string>();

  public const bool enableKinseyDefault = true;
  public static bool enableKinsey = true;

  public const KinseyMode kinseyFormulaDefault = KinseyMode.Realistic;
  public static KinseyMode kinseyFormula = KinseyMode.Realistic;

  public static readonly List<float> kinseyWeightCustomDefault = new List<float>() { 100f, 0f, 50f, 100f, 50f, 0f, 100f };
  public static List<float> kinseyWeightCustom = new List<float>() { 100f, 0f, 50f, 100f, 50f, 0f, 100f };

  public const bool enableEmpathyDefault = true;
  public static bool enableEmpathy = true;

  public const bool enableIndividualityDefault = true;
  public static bool enableIndividuality = true;

  // Writes Psychology's decisions and chances to the log (PsychologyDebug.Log).
  public const bool printDebugInfoDefault = false;
  public static bool printDebugInfo = false;

  public const bool enableDateLettersDefault = true;
  public static bool enableDateLetters = true;

  // Couples pick their own pregnancy approach from their family drives (FamilyUtility). Child-free pawns always avoid.
  public const bool pawnsChoosePregnancyApproachDefault = true;
  public static bool pawnsChoosePregnancyApproach = true;

  public const float mentalBreakAnxietyChanceDefault = 0.05f;
  public static float mentalBreakAnxietyChance = 0.05f;

  // Opinion penalty ("faction imprisoned me") a recruited prisoner gets towards each colonist. Slider range 0-40.
  public const float imprisonedDebuffDefault = 40f;
  public static float imprisonedDebuff = 40f;
  public const float imprisonedDebuffMax = 40f;

  // Base of the ArrestPeacefullyChance stat, in percent (plus 5% per Social level); see Pawn_GetAcceptArrestChance_Patch.
  public const float arrestBaseChanceDefault = 2f;
  public static float arrestBaseChance = 2f;
  public const float arrestBaseChanceMax = 50f;

  // Scale vanilla's rapport from warden chats by the two pawns' personality compatibility (Thought_RapportBuilt).
  public const bool adjustRapportByCompatibilityDefault = true;
  public static bool adjustRapportByCompatibility = true;

  public const float conversationDurationDefault = 60f;
  public static float conversationDuration = 60f;

  public const float convoMaxOpinionChangeDefault = 40f;
  public static float convoMaxOpinionChange = 40f;

  public const float convoMeanHoursDefault = 1.3f;
  public static float convoMeanHours = 1.3f;

  public const float convoTimeScaleHoursDefault = 1f;
  public static float convoTimeScaleHours = 1f;

  public const float convoPersonalityEffectMultiplierDefault = 1f;
  public static float convoPersonalityEffectMultiplier = 1f;

  public const float romanceChanceMultiplierDefault = 1f; // v1.1
  public static float romanceChanceMultiplier = 1f; // v1.1

  public const float romanceOpinionThresholdDefault = -5f; // also gates courtship; low so pawns who barely know each other can ask
  public static float romanceOpinionThreshold = -5f;

  // Divides the time between a couple's lovin (2 = twice as often). Slider range lovinFrequencyMin-lovinFrequencyMax.
  public const float lovinFrequencyDefault = 1f;
  public static float lovinFrequency = 1f;
  public const float lovinFrequencyMin = 0.1f;
  public const float lovinFrequencyMax = 5f;

  // Base chance (percent) that a pawn cheats on a partner they don't like; see RomanceUtility.CheatChance. Slider 0-100.
  public const float cheatChanceDefault = 50f;
  public static float cheatChance = 50f;

  // Courtship (InteractionWorker_AskOutOnDate): multiplies how often pawns ask someone out; 0 turns courtship off.
  public const float courtshipChanceDefault = 1f;
  public static float courtshipChance = 1f;
  public const float courtshipChanceMax = 5f;

  // Percent daily chance, at best, that a pair whose last courtship date went well for both asks for another wherever
  // they are on the map; see RomanceUtility.TrySecondDates. Slider 0-100; 0 turns it off.
  public const float secondDateChanceDefault = 33f;
  public static float secondDateChance = 33f;

  // Percent chance that a random interaction during a super-active social job (relaxing socially, parties, dancing,
  // billiards) becomes a look around for romance; see RomanceAttemptRouter.TrySocialRomance. Slider 0-100.
  public const float socialRomanceChanceDefault = 10f;
  public static float socialRomanceChance = 10f;

  // Multiplies how often pawns with a strong bond hug (InteractionWorker_Hug); 0 turns hugs off.
  public const float hugChanceDefault = 1f;
  public static float hugChance = 1f;
  public const float hugChanceMax = 5f;

  // Opinion of a courtship date at its best (quality 1) and worst (quality -1); see RomanceUtility.GainDateMemory.
  public const float dateOpinionGoodDefault = 15f;
  public static float dateOpinionGood = 15f;
  public const float dateOpinionBadDefault = -5f;
  public static float dateOpinionBad = -5f;
  public const float dateOpinionMax = 30f;

  // Percent chance a good courtship date ends in a romance attempt, and the bonus per earlier good date (percent of
  // that chance); see RomanceUtility.RomanceChanceAfterDate.
  public const float dateRomanceChanceDefault = 15f;
  public static float dateRomanceChance = 15f;
  public const float dateRomanceBonusDefault = 50f;
  public static float dateRomanceBonus = 50f;

  public const float traitOpinionMultiplierDefault = 0.25f; // v1.3
  public static float traitOpinionMultiplier = 0.25f; // v1.3

  public const float personalityExtremenessDefault = 0.33f; // v1.3
  public static float personalityExtremeness = 0.33f; // v1.3

  public const float ideoPsycheMultiplierDefault = 1f;
  public static float ideoPsycheMultiplier = 1f;

  public static Dictionary<string, SpeciesSettings> speciesDict = new Dictionary<string, SpeciesSettings>();

  // Hidden settings
  public const int displayOptionDefault = 4; // v1.3
  public static int displayOption = 4; // v1.3

  public const bool useColorsDefault = true; // v1.3
  public static bool useColors = true; // v1.3

  public const bool listAlphabeticalDefault = false; // v1.3
  public static bool listAlphabetical = false; // v1.3

  public const bool useAntonymsDefault = true; // v1.3
  public static bool useAntonyms = true; // v1.3

  // Hookup settings
  public const float hookupRateMultiplierDefault = 1f;
  public static float hookupRateMultiplier = 1f;

  public const float minOpinionForHookupDefault = 5f;
  public static float minOpinionForHookup = 5f;


  /* DEPRECATED SETTINGS */
  private const bool enableAnxietyDefault = true; // v1.1
  private static bool enableAnxiety = true; // v1.1

  public const float convoOpinionMultiplierDefault = 1f;
  public static float convoOpinionMultiplier = 1f;

  public override void ExposeData()
  {
    //Log.Message("PsychologySettings, ExposeData start");
    /* Options in settings window */
    ScribeValueAndAddToNameList(nameof(enableKinsey));
    ScribeValueAndAddToNameList(nameof(enableEmpathy));
    ScribeValueAndAddToNameList(nameof(enableIndividuality));
    ScribeValueAndAddToNameList(nameof(printDebugInfo));
    ScribeValueAndAddToNameList(nameof(enableDateLetters));
    ScribeValueAndAddToNameList(nameof(pawnsChoosePregnancyApproach));
    ScribeValueAndAddToNameList(nameof(mentalBreakAnxietyChance));
    ScribeValueAndAddToNameList(nameof(imprisonedDebuff));
    ScribeValueAndAddToNameList(nameof(adjustRapportByCompatibility));
    ScribeValueAndAddToNameList(nameof(arrestBaseChance));
    ScribeValueAndAddToNameList(nameof(conversationDuration));
    ScribeValueAndAddToNameList(nameof(convoMaxOpinionChange));
    ScribeValueAndAddToNameList(nameof(convoMeanHours));
    ScribeValueAndAddToNameList(nameof(convoTimeScaleHours));
    ScribeValueAndAddToNameList(nameof(convoPersonalityEffectMultiplier));
    ScribeValueAndAddToNameList(nameof(romanceChanceMultiplier));
    ScribeValueAndAddToNameList(nameof(romanceOpinionThreshold));
    ScribeValueAndAddToNameList(nameof(lovinFrequency));
    ScribeValueAndAddToNameList(nameof(cheatChance));
    ScribeValueAndAddToNameList(nameof(courtshipChance));
    ScribeValueAndAddToNameList(nameof(secondDateChance));
    ScribeValueAndAddToNameList(nameof(socialRomanceChance));
    ScribeValueAndAddToNameList(nameof(hugChance));
    ScribeValueAndAddToNameList(nameof(dateOpinionGood));
    ScribeValueAndAddToNameList(nameof(dateOpinionBad));
    ScribeValueAndAddToNameList(nameof(dateRomanceChance));
    ScribeValueAndAddToNameList(nameof(dateRomanceBonus));
    ScribeValueAndAddToNameList(nameof(traitOpinionMultiplier));
    ScribeValueAndAddToNameList(nameof(personalityExtremeness));
    ScribeValueAndAddToNameList(nameof(ideoPsycheMultiplier));

    Scribe_Values.Look(ref kinseyFormula, "Psychology_KinseyFormula", kinseyFormulaDefault);
    Scribe_Collections.Look(ref kinseyWeightCustom, "Psychology_KinseyWeightCustom", LookMode.Value);
    Scribe_Collections.Look(ref speciesDict, "Psychology_SpeciesSettings", LookMode.Value, LookMode.Deep);

    /* Hidden settings */
    Scribe_Values.Look(ref displayOption, "Psychology_DisplayOption", displayOptionDefault);
    Scribe_Values.Look(ref useColors, "Psychology_UseColors", useColorsDefault);
    Scribe_Values.Look(ref listAlphabetical, "Psychology_ListAlphabetical", listAlphabeticalDefault);
    Scribe_Values.Look(ref useAntonyms, "Psychology_UseAntonyms", useAntonymsDefault);

    /* Deprecated settings. Set each to default and use forceSave = false to essentially delete them from savefile */
    Scribe_Values.Look(ref enableAnxiety, "Psychology_EnableAnxiety", enableAnxietyDefault);
    enableAnxiety = enableAnxietyDefault;
    Scribe_Values.Look(ref convoOpinionMultiplier, "Psychology_ConvoOpinionMultiplier", convoOpinionMultiplierDefault);
    enableAnxiety = enableAnxietyDefault;

    /* Keep values saved by older versions (text boxes, wider ranges) inside the sliders' ranges */
    imprisonedDebuff = Mathf.Clamp(imprisonedDebuff, 0f, imprisonedDebuffMax);
    lovinFrequency = Mathf.Clamp(lovinFrequency, lovinFrequencyMin, lovinFrequencyMax);
    arrestBaseChance = Mathf.Clamp(arrestBaseChance, 0f, arrestBaseChanceMax);
    cheatChance = Mathf.Clamp(cheatChance, 0f, 100f);
    courtshipChance = Mathf.Clamp(courtshipChance, 0f, courtshipChanceMax);
    secondDateChance = Mathf.Clamp(secondDateChance, 0f, 100f);
    socialRomanceChance = Mathf.Clamp(socialRomanceChance, 0f, 100f);
    hugChance = Mathf.Clamp(hugChance, 0f, hugChanceMax);
    dateOpinionGood = Mathf.Clamp(dateOpinionGood, 0f, dateOpinionMax);
    dateOpinionBad = Mathf.Clamp(dateOpinionBad, -dateOpinionMax, 0f);
    dateRomanceChance = Mathf.Clamp(dateRomanceChance, 0f, 100f);
    dateRomanceBonus = Mathf.Clamp(dateRomanceBonus, 0f, 100f);
  }

  // Saves/loads the static field called settingName (bool or float) and registers it in the name lists.
  public static void ScribeValueAndAddToNameList(string settingName)
  {
    FieldInfo fieldInfo = AccessTools.Field(typeof(PsychologySettings), settingName);
    FieldInfo fieldInfoDefault = AccessTools.Field(typeof(PsychologySettings), settingName + "Default");
    object setting = fieldInfo.GetValue(null);
    object settingDefault = fieldInfoDefault.GetValue(null);
    string scribeName = "Psychology_" + settingName.CapitalizeFirst();
    if (setting is bool boolSetting && settingDefault is bool boolSettingDefault)
    {
      Scribe_Values.Look(ref boolSetting, scribeName, boolSettingDefault);
      fieldInfo.SetValue(null, boolSetting);
      if (BoolSettingNameList.Add(settingName))
      {
        CombinedSettingNameList.Add(settingName);
      }
      return;
    }
    if (setting is float floatSetting && settingDefault is float floatSettingDefault)
    {
      Scribe_Values.Look(ref floatSetting, scribeName, floatSettingDefault);
      fieldInfo.SetValue(null, floatSetting);
      if (FloatSettingNameList.Add(settingName))
      {
        CombinedSettingNameList.Add(settingName);
      }
      return;
    }
    Log.Error("Could not add setting " + settingName + " correctly");
  }

  public static object GetSettingFromName(string settingName)
  {
    FieldInfo field = AccessTools.Field(typeof(PsychologySettings), settingName);
    object obj = field.GetValue(null);
    if (obj == null)
    {
      Log.Error("GetSettingFromName, object was null");
    }
    return obj;
  }

  public static void SetSettingFromName(string settingName, object value)
  {
    AccessTools.Field(typeof(PsychologySettings), settingName).SetValue(null, value);
  }

  public static void ResetAllSettings()
  {
    foreach (string settingName in CombinedSettingNameList)
    {
      ResetSettingToDefault(settingName);
    }
    ResetKinseyFormula();
    ResetKinseyWeightCustom();
    ResetSpeciesSettings();
  }

  public static void ResetSettingToDefault(string settingName)
  {
    FieldInfo fieldInfoSetting = AccessTools.Field(typeof(PsychologySettings), settingName);
    FieldInfo fieldInfoDefault = AccessTools.Field(typeof(PsychologySettings), settingName + "Default");
    fieldInfoSetting.SetValue(null, fieldInfoDefault.GetValue(null));
  }

  public static void ResetKinseyFormula()
  {
    kinseyFormula = kinseyFormulaDefault;
  }

  public static void ResetKinseyWeightCustom()
  {
    kinseyWeightCustom = kinseyWeightCustomDefault.ListFullCopy();
  }

  public static void ResetSpeciesSettings()
  {
    SpeciesHelper.ResetSpeciesDict(speciesDict);
  }
}
