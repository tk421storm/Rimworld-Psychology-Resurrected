using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace Psychology;

/// <summary>
/// A pawn's sexuality when the Kinsey system is enabled (the default):
///   - kinseyRating 0..6: 0 = exclusively heterosexual, 6 = exclusively homosexual, rolled from the "Kinsey formula" setting
///   - sexDrive and romanticDrive in [0, 1]: normal(1.1, 0.26) truncated to [0, 1], so most pawns sit near the top;
///     a sex drive below 0.1 counts as asexual. Both are further scaled by age (AdjustedSexDrive/AdjustedRomanticDrive).
///   - knownSexualities: Kinsey ratings of others this pawn has learned, e.g. by being turned down, so they stop
///     hitting on people who can't be interested in them.
///   - familyDrive in [0, 1]: how much the pawn wants children, normal(0.55, 0.2) truncated to [0, 1]. This is the raw
///     roll; personality and the Matriarch / Child-free traits shape it further (FamilyUtility.FamilyDrive). Used with
///     or without Kinsey.
///   - pregnancyApproachChoices: the pregnancy approach this pawn and each partner last chose for themselves
///     (FamilyUtility.UpdatePregnancyApproach), to tell when the player has changed it since.
/// When the Kinsey system is disabled, the vanilla Gay/Bisexual/Asexual traits are used instead.
/// </summary>
public class Pawn_SexualityTracker : IExposable
{
  public int kinseyRating;
  public float sexDrive;
  public float romanticDrive;
  // -1: not rolled yet (saves from before family drive); rolled on load.
  public float familyDrive = -1f;
  public Dictionary<Pawn, PregnancyApproach> pregnancyApproachChoices = new Dictionary<Pawn, PregnancyApproach>();
  private List<Pawn> pregnancyApproachChoicesKeys;
  private List<PregnancyApproach> pregnancyApproachChoicesValues;
  public List<Pawn> knownSexualitiesWorkingKeys;
  public List<int> knownSexualitiesWorkingValues;
  public Dictionary<Pawn, int> knownSexualities = new Dictionary<Pawn, int>();
  public Pawn pawn;
  public static readonly float[] onesArray = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
  public const float asexualCutoff = 0.1f;
  public static readonly int[] bisexualRatings = new int[] { 2, 3, 4 };
  public static readonly int[] homosexualRatings = new int[] { 5, 6 };
  public bool IsAsexual => sexDrive < asexualCutoff;

  public float AdjustedRomanticDrive => AdjustedDrive(true);
  public float AdjustedSexDrive => AdjustedDrive(false);

  public static readonly SimpleCurve FemaleSexDriveCurve = new SimpleCurve
    {
        new CurvePoint(13f, 0f),
        new CurvePoint(15f, 1f),
        new CurvePoint(35f, 1.6f),
        new CurvePoint(50f, 1f),
        new CurvePoint(80f, 0.6f),
    };

  public static readonly SimpleCurve MaleSexDriveCurve = new SimpleCurve
    {
        new CurvePoint(13f, 0f),
        new CurvePoint(15f, 1f),
        new CurvePoint(20f, 1.6f),
        new CurvePoint(50f, 1f),
        new CurvePoint(80f, 0.6f),
    };

  public static readonly SimpleCurve RomanticDriveCurve = new SimpleCurve
    {
        new CurvePoint(10f, 0f),
        new CurvePoint(15f, 1.3f),
        new CurvePoint(25f, 1.6f),
        new CurvePoint(35f, 1.3f),
        new CurvePoint(50f, 1f),
        new CurvePoint(80f, 0.8f),
    };

  public Pawn_SexualityTracker(Pawn p)
  {
    this.pawn = p;
  }

  // Age-adjusted drive: 0 below the species' romance age; otherwise the raw drive times an age curve evaluated at the
  // "human-equivalent" age (RomanticDriveCurve for dating, the gender-specific sex-drive curves for lovin), or the raw
  // drive for species without age gaps / with a minimum age of 0.
  private float AdjustedDrive(bool isDating)
  {
    // Pawns can still have drive even if they are underage from the user species settings. Hence no check on species settings.
    if (!SpeciesHelper.RomanceLifestageAgeCheck(pawn, true))
    {
      return 0f;
    }
    float age = pawn.ageTracker.AgeBiologicalYearsFloat;
    SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
    float minSettingsAge = isDating ? settings.minDatingAge : settings.minLovinAge;
    float drive = isDating ? romanticDrive : sexDrive;
    if (!settings.enablePsyche || minSettingsAge < 0f)
    {
      return 0f;
    }
    if (!settings.enableAgeGap)
    {
      //Log.Message("AdjustedDrive, !RomanceLifestageAgeCheck, pawn: " + pawn + ", minSettingsAge: " + minSettingsAge);
      // settings.minDatingAge here is intentional for both romantic and sex drive.
      return age > settings.minDatingAge ? drive : 0f;
    }
    if (minSettingsAge == 0f)
    {
      return drive;
    }
    float scaledAge = isDating ? PsycheHelper.DatingBioAgeToVanilla(age, minSettingsAge) : PsycheHelper.LovinBioAgeToVanilla(age, minSettingsAge);
    float ageFactor = 1f;
    if (isDating)
    {
      ageFactor = RomanticDriveCurve.Evaluate(scaledAge);
    }
    else
    {
      switch (pawn.gender)
      {
        case Gender.Female:
          ageFactor = FemaleSexDriveCurve.Evaluate(scaledAge);
          break;
        case Gender.Male:
          ageFactor = MaleSexDriveCurve.Evaluate(scaledAge);
          break;
        default:
          // Maybe one day other genders will come to the Rim...
          ageFactor = 0.5f * (FemaleSexDriveCurve.Evaluate(scaledAge) + MaleSexDriveCurve.Evaluate(scaledAge));
          break;
      }
    }
    return ageFactor * drive;

  }

  // True if this pawn has learned the recipient's Kinsey rating and it says they aren't into this pawn's gender
  // (rating 4+ toward the other gender, or under 4 toward the same gender).
  public virtual bool IncompatibleSexualityKnown(Pawn recipient)
  {
    if (this.knownSexualities.ContainsKey(recipient))
    {
      return ((knownSexualities[recipient] - 4) >= 0) != (recipient.gender == this.pawn.gender);
    }
    return false;
  }

  // Records p's Kinsey rating as known to this pawn (only if p has a psyche to read it from).
  public virtual void LearnSexuality(Pawn p)
  {
    if (p != null && PsycheHelper.PsychologyEnabled(p) && !knownSexualities.Keys.Contains(p))
    {
      knownSexualities.Add(p, PsycheHelper.Comp(p).Sexuality.kinseyRating);
    }
  }

  public virtual void ExposeData()
  {
    Scribe_Values.Look(ref this.kinseyRating, "kinseyRating", 0, false);
    Scribe_Values.Look(ref this.sexDrive, "sexDrive", 1, false);
    Scribe_Values.Look(ref this.romanticDrive, "romanticDrive", 1, false);
    Scribe_Values.Look(ref this.familyDrive, "familyDrive", -1f, false);
    // Remembered pawns can be discarded later (e.g. world pawn clean-up of old visitors), which leaves references that
    // don't resolve: forget them before saving, and don't log the ones older saves still have (they're just skipped).
    if (Scribe.mode == LoadSaveMode.Saving)
    {
      this.knownSexualities?.RemoveAll(kvp => kvp.Key == null || kvp.Key.Discarded);
      this.pregnancyApproachChoices?.RemoveAll(kvp => kvp.Key == null || kvp.Key.Discarded);
    }
    Scribe_Collections.Look(ref this.knownSexualities, "knownSexualities", LookMode.Reference, LookMode.Value, ref this.knownSexualitiesWorkingKeys, ref this.knownSexualitiesWorkingValues, logNullErrors: false);
    Scribe_Collections.Look(ref this.pregnancyApproachChoices, "pregnancyApproachChoices", LookMode.Reference, LookMode.Value, ref this.pregnancyApproachChoicesKeys, ref this.pregnancyApproachChoicesValues, logNullErrors: false);
    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
      this.knownSexualities ??= new Dictionary<Pawn, int>();
      this.pregnancyApproachChoices ??= new Dictionary<Pawn, PregnancyApproach>();
      this.pregnancyApproachChoices.RemoveAll(kvp => kvp.Key == null);
      if (this.familyDrive < 0f)
      {
        GenerateFamilyDrive();
      }
    }
  }

  public virtual void GenerateSexuality(int inputSeed = 0)
  {
    GenerateSexuality(onesArray, inputSeed);
  }

  public virtual void GenerateSexuality(float[] overrideKinseyArray, int inputSeed = 0)
  {
    GenerateKinsey(overrideKinseyArray, inputSeed);
    GenerateSexDrive(inputSeed);
    GenerateRomanticDrive(inputSeed);
    GenerateFamilyDrive(inputSeed);
  }

  public virtual void GenerateKinsey(float[] overrideKinseyArray, int inputSeed = 0)
  {
    kinseyRating = RandKinsey(overrideKinseyArray, inputSeed);
  }

  public virtual void GenerateSexDrive(int inputSeed = 0)
  {
    sexDrive = RandSexDrive(inputSeed);
  }

  public virtual void GenerateRomanticDrive(int inputSeed = 0)
  {
    romanticDrive = RandRomanticDrive(inputSeed);
  }

  public virtual void GenerateFamilyDrive(int inputSeed = 0)
  {
    familyDrive = RandFamilyDrive(inputSeed);
  }

  /*
   * Average roll: 0.989779
   * Percent chance of rolling each number:
   * 0: 62.4949 %
   * 1: 11.3289 %
   * 2: 9.2658 %
   * 3: 6.8466 %
   * 4: 4.522 %
   * 5: 2.7806 %
   * 6: 2.7612 %
   * Percent chance of being predominantly straight:  83.0896 %
   * Percent chance of being predominantly gay:       10.0638 %
   * Percent chance of being more or less straight:   73.8238 %
   * Percent chance of being more or less bisexual:   20.6344 %
   * Percent chance of being more or less gay:         5.5418 %
   */

  // oList multiplies the formula weights, e.g. to force "mostly bisexual" when replacing a vanilla trait.
  public virtual int RandKinsey(float[] oList, int inputSeed = 0)
  {
    float[] wArray = new float[7];
    if (PsychologySettings.kinseyFormula != KinseyMode.Custom)
    {
      wArray = PsycheHelper.KinseyModeWeightDict[PsychologySettings.kinseyFormula];
    }
    else
    {
      wArray = PsychologySettings.kinseyWeightCustom.ToArray();
      if (wArray.Sum() == 0f)
      {
        wArray = onesArray;
      }
    }
    float wCumSum = 0f;
    float woCumSum = 0f;
    float[] wCumSumArray = new float[7];
    float[] woCumSumArray = new float[7];
    for (int i = 0; i < 7; i++)
    {
      wCumSum += wArray[i];
      wCumSumArray[i] = wCumSum;
      woCumSum += wArray[i] * oList[i];
      woCumSumArray[i] = woCumSum;
    }
    if (woCumSumArray[6] > 0f)
    {
      return RandKinseyByWeight(woCumSumArray, inputSeed);
    }
    return RandKinseyByWeight(wCumSumArray, inputSeed);
  }

  public virtual int RandKinseyByWeight(float[] wCumSumArray, int inputSeed = 0)
  {
    float randValue = Rand.ValueSeeded(17 * PsycheHelper.PawnSeed(this.pawn) + 11 * inputSeed + 31) * wCumSumArray[6];
    for (int s = 0; s < 6; s++)
    {
      if (randValue <= wCumSumArray[s])
      {
        return s;
      }
    }
    return 6;
  }

  // Rejection-samples a seeded normal(1.1, 0.26) until it falls inside [0, 1].
  public virtual float RandSexDrive(int inputSeed = 0)
  {
    float drive = -1f;
    int kill = 0;
    int pawnSeed = PsycheHelper.PawnSeed(this.pawn);
    int seed1 = 11 * pawnSeed + 2 * inputSeed + 131;
    int seed2 = 13 * pawnSeed + 7 * inputSeed + 89;
    while ((drive < 0f || 1f < drive) && kill < 500)
    {
      drive = PsycheHelper.RandGaussianSeeded(seed1, seed2, 1.1f, 0.26f);
      seed1 += 43;
      seed2 += 67;
      kill++;
    }
    return Mathf.Clamp01(drive);
  }

  public virtual float RandRomanticDrive(int inputSeed = 0)
  {
    return RandSexDrive(859456 + 3 * inputSeed);
  }

  // Rejection-samples a seeded normal(0.55, 0.2) until it falls inside [0, 1]: unlike the other drives, spread across
  // the whole range, as people differ a lot in how much they want children.
  public virtual float RandFamilyDrive(int inputSeed = 0)
  {
    float drive = -1f;
    int kill = 0;
    int pawnSeed = PsycheHelper.PawnSeed(this.pawn);
    int seed1 = 17 * pawnSeed + 5 * inputSeed + 271;
    int seed2 = 19 * pawnSeed + 3 * inputSeed + 613;
    while ((drive < 0f || 1f < drive) && kill < 500)
    {
      drive = PsycheHelper.RandGaussianSeeded(seed1, seed2, 0.55f, 0.2f);
      seed1 += 43;
      seed2 += 67;
      kill++;
    }
    return Mathf.Clamp01(drive);
  }

  // The *TraitReroll methods convert a vanilla orientation trait into the equivalent Kinsey values (see
  // PsycheHelper.CorrectTraitsForPawnKinseyEnabled).
  public void AsexualTraitReroll()
  {
    if (this.sexDrive < asexualCutoff)
    {
      return;
    }
    this.sexDrive = asexualCutoff * Rand.ValueSeeded(5 * PsycheHelper.PawnSeed(this.pawn) + 8);
  }

  public void BisexualTraitReroll()
  {
    if (bisexualRatings.Contains(this.kinseyRating))
    {
      return;
    }
    GenerateKinsey(new float[] { 0f, 0f, 1f, 3f, 1f, 0f, 0f });
    PsycheHelper.Comp(this.pawn).Psyche.CalculateAdjustedRatings();
  }

  public void GayTraitReroll()
  {
    if (homosexualRatings.Contains(this.kinseyRating))
    {
      return;
    }
    GenerateKinsey(new float[] { 0f, 0f, 0f, 0f, 0f, 1f, 3f });
    PsycheHelper.Comp(pawn).Psyche.CalculateAdjustedRatings();
  }

  public void DeepCopyFromOtherTracker(Pawn_SexualityTracker otherTracker)
  {
    this.kinseyRating = otherTracker.kinseyRating;
    this.sexDrive = otherTracker.sexDrive;
    this.romanticDrive = otherTracker.romanticDrive;
    this.familyDrive = otherTracker.familyDrive;
    this.pregnancyApproachChoices = new Dictionary<Pawn, PregnancyApproach>(otherTracker.pregnancyApproachChoices);
    if (otherTracker.knownSexualitiesWorkingKeys != null)
    {
      this.knownSexualitiesWorkingKeys = new List<Pawn>();
      foreach (Pawn p in otherTracker.knownSexualitiesWorkingKeys)
      {
        this.knownSexualitiesWorkingKeys.Add(p);
      }
    }
    if (otherTracker.knownSexualitiesWorkingValues != null)
    {
      this.knownSexualitiesWorkingValues = new List<int>();
      foreach (int i in otherTracker.knownSexualitiesWorkingValues)
      {
        this.knownSexualitiesWorkingValues.Add(i);
      }
    }
    this.knownSexualities = new Dictionary<Pawn, int>();
    foreach (KeyValuePair<Pawn, int> kvp in otherTracker.knownSexualities)
    {
      this.knownSexualities.Add(kvp.Key, kvp.Value);
    }
  }
}
