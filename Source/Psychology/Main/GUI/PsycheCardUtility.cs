using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

namespace Psychology;

/// <summary>
/// Draws the psyche card: the Big Five radar (pentagon, with tooltips per axis), the personality list or word cloud
/// (each node labelled "very/somewhat/slightly X" or its antonym from its adjusted rating, coloured by nodeHSV /
/// antonymHSV), and the sexuality/drive description. Display options (list vs cloud, colours, antonyms, sort order) are
/// PsychologySettings fields toggled from the card. Derived data (adjusted ratings, trait list / word cloud, Big Five,
/// sexuality labels) is rebuilt at most once per RefreshIntervalSeconds of real time, or straight away when the pawn or
/// a display option changes or RequestRefresh() is called, rather than on every GUI pass.
/// </summary>
[StaticConstructorOnStartup]
public class PsycheCardUtility
{
  public static Rect PsycheRect = new Rect(0f, 0f, 1f, 1f);

  public const float RowTopPadding = 0f;
  public const float BoundaryPadding = 7f;
  public const float HighlightPadding = 3.5f;
  public const float TraitListPadding = 30f;

  public static int OldSmallFontSize = Text.fontStyles[1].fontSize;
  public static int OldMediumFontSize = Text.fontStyles[2].fontSize;
  public const int SexualityFontSize = 16;
  public const int OptionsFontSize = 16;
  public const int FiveFactorTitleFontSize = 18;
  public const int CategoryNodeFontSize = 16;

  public static string RatingFormat = "##0.000%";

  public static int DisplayOptionCached;
  public static bool UseColorsCached;
  public static bool ListAlphabeticalCached;
  public static bool UseAntonymsCached;
  public static Pawn PawnCached;
  public static bool UseCachedBool = false;
  public static bool ShowNumbersBool = false;
  public const float RefreshIntervalSeconds = 1f;
  private static float nextRefreshTime = -1f;

  // Sexuality panel text, rebuilt on refresh.
  private static string KinseyTextCached = "";
  private static int SexDriveIntCached;
  private static int RomDriveIntCached;
  private static int FamDriveIntCached;

  // Big Five axis labels and their sizes (depend only on language and font size), built on first use.
  private static string[] BigFiveLabels;
  private static Vector2[] BigFiveLabelSizes;

  // Big Five radar geometry for the last centre/radius; the filled polygon also changes with the ratings.
  private static Vector2 bigFiveGeometryCenter = new Vector2(float.NaN, float.NaN);
  private static float bigFiveGeometryRadius = float.NaN;
  private static readonly List<Vector2>[] bigFiveRings = new List<Vector2>[RadialCategories];
  private static readonly List<Vector2>[] bigFiveHighlights = new List<Vector2>[5];
  private static readonly Rect[] bigFiveLabelRects = new Rect[5];
  private static readonly List<Vector2> bigFiveYellowVertices = new List<Vector2>(6);
  private static readonly List<int[]> bigFiveYellowTriangles = new List<int[]> { new[] { 0, 1, 5 }, new[] { 1, 2, 5 }, new[] { 2, 3, 5 }, new[] { 3, 4, 5 }, new[] { 4, 0, 5 } };
  private static readonly List<int[]> noTriangles = new List<int[]>();

  // What the word cloud was last laid out for; the layout is only redone when this changes.
  private static readonly List<string> CloudLayoutKey = new List<string>();
  private static Rect CloudLayoutRect;

  // Makes the next draw rebuild everything (after the pawn's psyche was edited, e.g. by the dev editor or indoctrination).
  public static void RequestRefresh()
  {
    nextRefreshTime = -1f;
  }

  public static Vector2 KinseyTextSize;
  public static List<string> SexDriveText = new List<string>();
  public static List<Vector2> SexDriveSize = new List<Vector2>();
  public static float SexDriveWidth;
  public static float SexDriveHeight;
  public static List<string> RomDriveText = new List<string>();
  public static List<Vector2> RomDriveSize = new List<Vector2>();
  public static float RomDriveWidth;
  public static float RomDriveHeight;
  public static List<string> FamDriveText = new List<string>();
  public static List<Vector2> FamDriveSize = new List<Vector2>();
  public static float FamDriveWidth;
  public static float FamDriveHeight;

  public static List<string> OptionsText = new List<string>();
  public static List<Vector2> OptionsSize = new List<Vector2>();
  public static float OptionsWidth;
  public static float OptionsHeight;

  public static string UseColorsText = "UseColors".Translate();
  public static Vector2 UseColorsSize;
  public static float UseColorsWidth;
  public static float UseColorsHeight;

  public static string AlphabeticalText = "ListAlphabetical".Translate();
  public static Vector2 AlphabeticalSize;
  public static float AlphabeticalWidth;
  public static float AlphabeticalHeight;

  public static string UseAntonymsText = "UseAntonyms".Translate();
  public static Vector2 UseAntonymsSize;
  public static float UseAntonymsWidth;
  public static float UseAntonymsHeight;

  public static string FiveFactorText = "FiveFactorModelTitle".Translate();
  public static Vector2 FiveFactorSize;
  public static float[] bigFiveRatings = new float[5];

  public static float SexualityWidth;
  public static float KinseyWidth;

  public static readonly string[] BigFiveLetters = { "O", "C", "E", "A", "N" };
  public static readonly Vector2[] BigFiveSpacings = { new Vector2(0f, -1f), new Vector2(1f, -0.32492f), new Vector2(0.726543f, 1f), new Vector2(-0.726543f, 1f), new Vector2(-1f, -0.32492f) };
  public static readonly Vector3[] BigFiveHSVs = { new Vector3(125f, 1f, 0.95f), new Vector3(185f, 1f, 0.95f), new Vector3(55f, 1f, 0.95f), new Vector3(285f, 0.9f, 1f), new Vector3(345f, 1f, 0.95f) };
  public static readonly Color[] BigFiveColors = new Color[5];
  public const int RadialCategories = 2;
  public const float SideScaling = 0.809017f;
  public static float[] BigFiveManualSliders = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };

  public static float HighlightRadius = 0f;

  public static float NodeWidth = 0f;
  public static float CategoryWidth = 0f;
  public static float CategoryNodeHeight = 0f;
  public static readonly string[] NodeDescriptions = { "Not", "Slightly", "Less", "Somewhat", "More", "Very", "Extremely" };
  public static readonly Color[] CategoryColors = { new Color(1f, 0.2f, 0.2f, 0.8f), new Color(1f, 0.4f, 0.4f, 0.6f), new Color(1f, 0.6f, 0.6f, 0.4f), new Color(1f, 1f, 1f, 0.2f), new Color(0.6f, 1f, 0.6f, 0.4f), new Color(0.4f, 1f, 0.4f, 0.6f), new Color(0.2f, 1f, 0.2f, 0.8f) };
  public static Vector2 NodeScrollPosition = Vector2.zero;
  public static List<Tuple<string, PersonalityNode, string>> LabelNodeList = new List<Tuple<string, PersonalityNode, string>>();

  public static List<Rect> CloudTextRects = new List<Rect>();
  public static List<Rect> CloudTightRects = new List<Rect>();
  public static List<string> CloudTexts = new List<string>();
  public static List<PersonalityNode> CloudNodes = new List<PersonalityNode>();
  public static List<int> CloudFontSizes = new List<int>();
  public const float PointsOnSpiral = 2000f;
  public const float kSpiral = 60f * Mathf.PI;

  public const float xMinTightScaling = 0.005f;
  public const float xMaxTightScaling = 0.005f;
  public const float yMinTightScaling = 0.135f;
  public const float yMaxTightScaling = 0.005f;
  public static Vector2 calcSizeScalingVector = new Vector2(1.035f, 1.025f);

  public static readonly string[] CreativeLetters = { "C", "r", "e", "a", "t", "i", "v", "e" };
  public static readonly Vector3[] CreativeHSVs = { new Vector3(0f, 0.9f, 1f), new Vector3(30f, 1f, 1f), new Vector3(55f, 0.9f, 0.85f), new Vector3(120f, 0.8f, 0.8f), new Vector3(195f, 1f, 1f), new Vector3(210f, 1f, 1f), new Vector3(280f, 0.8f, 1f), new Vector3(305f, 1f, 0.85f) };
  public static string CreativeRainbow = "";

  public static GUIStyle style;
  public static TextAnchor OldAnchor;

  static PsycheCardUtility()
  {
    DisplayOptionCached = PsychologySettings.displayOption;
    UseColorsCached = PsychologySettings.useColors;
    ListAlphabeticalCached = PsychologySettings.listAlphabetical;
    UseAntonymsCached = PsychologySettings.useAntonyms;

    Text.Font = GameFont.Small;
    GUIStyle style = Text.fontStyles[1];

    for (int i = 0; i < 5; i++)
    {
      BigFiveColors[i] = HSVtoColor(BigFiveHSVs[i]);
    }

    style.fontSize = SexualityFontSize;
    // Kinsey rating text
    KinseyTextSize = Text.CalcSize("KinseyRating".Translate() + " 0") * calcSizeScalingVector;
    // Sex, romantic and family drive text
    for (int i = 0; i < 5; i++)
    {
      SexDriveText.Add(("SexDrive" + i.ToString()).Translate());
      RomDriveText.Add(("RomanticDrive" + i.ToString()).Translate());
      FamDriveText.Add(("FamilyDrive" + i.ToString()).Translate());
      SexDriveSize.Add(Text.CalcSize(SexDriveText[i]) * calcSizeScalingVector);
      RomDriveSize.Add(Text.CalcSize(RomDriveText[i]) * calcSizeScalingVector);
      FamDriveSize.Add(Text.CalcSize(FamDriveText[i]) * calcSizeScalingVector);
    }
    SexDriveWidth = SexDriveSize.Max(s => s.x);
    SexDriveHeight = SexDriveSize.Max(s => s.y);
    RomDriveWidth = RomDriveSize.Max(s => s.x);
    RomDriveHeight = RomDriveSize.Max(s => s.y);
    FamDriveWidth = FamDriveSize.Max(s => s.x);
    FamDriveHeight = FamDriveSize.Max(s => s.y);

    style.fontSize = OptionsFontSize;
    // Personality options text
    for (int i = 0; i < 5; i++)
    {
      OptionsText.Add(("OptionsText" + i.ToString()).Translate());
      OptionsSize.Add(Text.CalcSize(OptionsText[i]) * calcSizeScalingVector);
    }
    OptionsWidth = 20f + OptionsSize.Max(s => s.x);
    OptionsHeight = 5f + OptionsSize.Max(s => s.y);

    UseColorsSize = Text.CalcSize(UseColorsText) * calcSizeScalingVector;
    UseColorsWidth = UseColorsSize.x + 29f;
    UseColorsHeight = Mathf.Max(UseColorsSize.y, 24f);

    AlphabeticalSize = Text.CalcSize(AlphabeticalText) * calcSizeScalingVector;
    AlphabeticalWidth = AlphabeticalSize.x + 29f;
    AlphabeticalHeight = Mathf.Max(AlphabeticalSize.y, 24f);

    UseAntonymsSize = Text.CalcSize(UseAntonymsText) * calcSizeScalingVector;
    UseAntonymsWidth = UseAntonymsSize.x + 29f;
    UseAntonymsHeight = Mathf.Max(UseAntonymsSize.y, 24f);

    style.fontSize = FiveFactorTitleFontSize;
    FiveFactorSize = Text.CalcSize(FiveFactorText) * calcSizeScalingVector;

    SexualityWidth = Mathf.Max(KinseyTextSize.x, SexDriveWidth, RomDriveWidth, FamDriveWidth, OptionsWidth, UseColorsWidth, AlphabeticalWidth, UseAntonymsWidth, FiveFactorSize.x);
    KinseyWidth = SexualityWidth + BoundaryPadding - 27f;

    for (int c = 0; c < CreativeLetters.Count(); c++)
    {
      CreativeRainbow += CreativeLetters[c].Colorize(HSVtoColor(CreativeHSVs[c]));
    }

    style.fontSize = CategoryNodeFontSize;
    foreach (string nodeDescription in NodeDescriptions)
    {
      string categoryText = ("Psyche" + nodeDescription).Translate();
      Vector2 categoryTextSize = Text.CalcSize(categoryText);
      CategoryWidth = Mathf.Max(CategoryWidth, 1.025f * categoryTextSize.x);
      CategoryNodeHeight = Mathf.Max(CategoryNodeHeight, categoryTextSize.y);
    }
    foreach (PersonalityNodeDef def in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
    {
      string labelText = def.label.CapitalizeFirst();
      string antoText = def.antonymLabel.CapitalizeFirst();
      Vector2 labelSize = Text.CalcSize(labelText);
      Vector2 antoSize = Text.CalcSize(antoText);
      NodeWidth = Mathf.Max(NodeWidth, 1.025f * labelSize.x, 1.025f * antoSize.x);
      CategoryNodeHeight = Mathf.Max(CategoryNodeHeight, labelSize.y, antoSize.y);
    }
    style.fontSize = OldSmallFontSize;

    float personalityRectWidth = BoundaryPadding + TraitListPadding + CategoryWidth + TraitListPadding + GenUI.ScrollBarWidth + NodeWidth + TraitListPadding + BoundaryPadding;
    float forbiddenRectWidth = BoundaryPadding + HighlightPadding + SexualityWidth + BoundaryPadding;
    float totalRectWidth = personalityRectWidth + forbiddenRectWidth;
    float forbiddenRectHeight = BoundaryPadding + KinseyTextSize.y + RowTopPadding + SexDriveHeight + RowTopPadding + RomDriveHeight + RowTopPadding + FamDriveHeight + BoundaryPadding + OptionsHeight + BoundaryPadding + UseColorsHeight + BoundaryPadding;
    HighlightRadius = 0.506f * forbiddenRectWidth;
    float totalRectHeight = forbiddenRectHeight + AlphabeticalHeight + BoundaryPadding + UseAntonymsHeight + BoundaryPadding + FiveFactorSize.y + BoundaryPadding + 2f * HighlightRadius + BoundaryPadding;// + FiveFactorSize.y + BoundaryPadding;
    PsycheRect = new Rect(0f, 0f, totalRectWidth, totalRectHeight);

  }

  public static void DrawPsycheCard(Rect totalRect, Pawn pawn, bool OnWindow = true, bool ShowNumbers = false)
  {
    CheckIfCachedChanged(pawn);

    GUI.BeginGroup(totalRect);
    totalRect.position = Vector2.zero;
    Rect originalTotalRect = totalRect;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
    style = Text.fontStyles[1];
    OldAnchor = Text.Anchor;

    PawnCached = PawnCached == null ? pawn : PawnCached;
    ShowNumbersBool = ShowNumbers || Prefs.DevMode;
    float kWidth = OnWindow || Prefs.DevMode ? SexualityWidth : KinseyWidth;
    Rect kinseyRect = new Rect(totalRect.xMax - SexualityWidth - BoundaryPadding, totalRect.y + BoundaryPadding, kWidth, KinseyTextSize.y);
    Rect sexDriveRect = new Rect(kinseyRect.x, kinseyRect.yMax + RowTopPadding, SexualityWidth, SexDriveHeight);
    Rect romDriveRect = new Rect(kinseyRect.x, sexDriveRect.yMax + RowTopPadding, SexualityWidth, RomDriveHeight);
    Rect famDriveRect = new Rect(kinseyRect.x, romDriveRect.yMax + RowTopPadding, SexualityWidth, FamDriveHeight);

    // Calculate rectanges for display options
    Rect optionsRect = new Rect(kinseyRect.x - HighlightPadding, famDriveRect.yMax + BoundaryPadding, OptionsWidth, OptionsHeight);
    Rect useColorsRect = new Rect(kinseyRect.x, optionsRect.yMax + BoundaryPadding, UseColorsWidth, UseColorsHeight);
    Rect alphabeticalRect = new Rect(kinseyRect.x, useColorsRect.yMax + BoundaryPadding, AlphabeticalWidth, AlphabeticalHeight);
    Rect useAntonymsRect = new Rect(kinseyRect.x, alphabeticalRect.yMax + BoundaryPadding, UseAntonymsWidth, UseAntonymsHeight);

    //Calculate personality rectangle
    Rect personalityRect = totalRect;
    personalityRect.xMax = kinseyRect.x - HighlightPadding;
    personalityRect = personalityRect.ContractedBy(BoundaryPadding);
    Rect forbiddenRect = new Rect(personalityRect.xMax, 0f, totalRect.width - personalityRect.xMax, useColorsRect.yMax + BoundaryPadding);
    Rect fiveFactorRect = new Rect(forbiddenRect.center.x - 0.5f * FiveFactorSize.x, useAntonymsRect.yMax + 2f * BoundaryPadding, FiveFactorSize.x, FiveFactorSize.y);

    personalityRect.xMax = totalRect.xMax - SexualityWidth - HighlightPadding - 2f * BoundaryPadding;

    // Draw the widgets for sexuality panel
    if (PsychologySettings.enableKinsey)
    {
      string kinseyText = KinseyTextCached;
      int sexDriveInt = SexDriveIntCached;
      int romDriveInt = RomDriveIntCached;
      //Kinsey rating
      Text.Anchor = TextAnchor.MiddleLeft;
      style.fontSize = SexualityFontSize;
      Widgets.Label(kinseyRect, kinseyText);
      Widgets.Label(sexDriveRect, SexDriveText[sexDriveInt]);
      Widgets.Label(romDriveRect, RomDriveText[romDriveInt]);
      style.fontSize = OldSmallFontSize;
      Text.Anchor = OldAnchor;

      kinseyRect.xMin -= HighlightPadding;
      Widgets.DrawHighlightIfMouseover(kinseyRect);
      TooltipHandler.TipRegion(kinseyRect, delegate
      {
        return ((string)"KinseyDescription".Translate()).ReplaceFirst("{0}", "KinseyDescription0".Translate().Colorize(UIAssets.TitleColor));
      }, 89140);

      //Sex drive
      sexDriveRect.xMin -= HighlightPadding;
      Widgets.DrawHighlightIfMouseover(sexDriveRect);
      TooltipHandler.TipRegion(sexDriveRect, delegate
      {
        string sexString = ((string)"SexDriveDescription".Translate()).ReplaceFirst("{0}", "SexDriveDescription0".Translate().Colorize(UIAssets.TitleColor));
        if (sexDriveInt == 0)
        {
          sexString += "\n\n" + (string)"AsexualDescription".Translate();
        }
        if (ShowNumbersBool)
        {
          string rawRating = PsycheHelper.Comp(pawn).Sexuality.sexDrive.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
          string adjRating = PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
          sexString += "\n\nRaw: " + rawRating + "  Adjusted: " + adjRating;
        }
        return sexString;
      }, 89141);

      //Romantic drive
      romDriveRect.xMin -= HighlightPadding;
      Widgets.DrawHighlightIfMouseover(romDriveRect);
      TooltipHandler.TipRegion(romDriveRect, delegate
      {
        string romString = ((string)"RomanticDriveDescription".Translate()).ReplaceFirst("{0}", "RomanticDriveDescription0".Translate().Colorize(UIAssets.TitleColor));
        if (romDriveInt == 0)
        {
          romString += "\n\n" + (string)"AromanticDescription".Translate();
        }
        if (ShowNumbersBool)
        {
          string rawRating = PsycheHelper.Comp(pawn).Sexuality.romanticDrive.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
          string adjRating = PsycheHelper.Comp(pawn).Sexuality.AdjustedRomanticDrive.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
          romString += "\n\nRaw: " + rawRating + "  Adjusted: " + adjRating;
        }
        return romString;
      }, 89142);

    }
    else
    {
      Rect disabledRect = new Rect(kinseyRect.x, kinseyRect.y, kinseyRect.width, romDriveRect.yMax - kinseyRect.y);
      GUI.color = Color.red;
      Widgets.Label(disabledRect, "SexualityDisabledWarning".Translate());
      GUI.color = Color.white;
    }

    // Family drive: shown with or without Kinsey (FamilyUtility uses it either way).
    int famDriveInt = FamDriveIntCached;
    Text.Anchor = TextAnchor.MiddleLeft;
    style.fontSize = SexualityFontSize;
    Widgets.Label(famDriveRect, FamDriveText[famDriveInt]);
    style.fontSize = OldSmallFontSize;
    Text.Anchor = OldAnchor;
    famDriveRect.xMin -= HighlightPadding;
    Widgets.DrawHighlightIfMouseover(famDriveRect);
    TooltipHandler.TipRegion(famDriveRect, delegate
    {
      string famString = ((string)"FamilyDriveDescription".Translate()).ReplaceFirst("{0}", "FamilyDriveDescription0".Translate().Colorize(UIAssets.TitleColor));
      if (ShowNumbersBool)
      {
        string rawRating = PsycheHelper.Comp(pawn).Sexuality.familyDrive.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
        string adjRating = FamilyUtility.FamilyDrive(pawn).ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
        famString += "\n\nRaw: " + rawRating + "  Adjusted: " + adjRating;
      }
      return famString;
    }, 89143);

    style.fontSize = OptionsFontSize;
    if (Widgets.ButtonText(optionsRect, OptionsText[PsychologySettings.displayOption], drawBackground: true, doMouseoverSound: true, true))
    {
      List<FloatMenuOption> list = new List<FloatMenuOption>();
      list.Add(new FloatMenuOption(OptionsText[4], delegate
      {
        PsychologySettings.displayOption = 4;
      }));
      list.Add(new FloatMenuOption(OptionsText[3], delegate
      {
        PsychologySettings.displayOption = 3;
      }));
      list.Add(new FloatMenuOption(OptionsText[2], delegate
      {
        PsychologySettings.displayOption = 2;
      }));
      list.Add(new FloatMenuOption(OptionsText[1], delegate
      {
        PsychologySettings.displayOption = 1;
      }));
      list.Add(new FloatMenuOption(OptionsText[0], delegate
      {
        PsychologySettings.displayOption = 0;
      }));
      Find.WindowStack.Add(new FloatMenu(list));
    }
    Widgets.CheckboxLabeled(useColorsRect, UseColorsText, ref PsychologySettings.useColors);
    style.fontSize = OldSmallFontSize;

    if (PsychologySettings.displayOption == 4)
    {
      GUI.color = UIAssets.LineColor;
      Widgets.DrawLineVertical(forbiddenRect.x, forbiddenRect.y, forbiddenRect.height);
      Widgets.DrawLineHorizontal(forbiddenRect.x, forbiddenRect.yMax, forbiddenRect.width);
      GUI.color = Color.white;
      Vector2 cloudCenter = totalRect.center;
      cloudCenter.x = 0.5f * totalRect.center.x + 0.5f * personalityRect.center.x;
      PersonalityWordCloud(totalRect, cloudCenter, forbiddenRect, pawn);//, editRect);
    }
    else
    {
      style.fontSize = OptionsFontSize;
      Widgets.CheckboxLabeled(alphabeticalRect, AlphabeticalText, ref PsychologySettings.listAlphabetical);
      Widgets.CheckboxLabeled(useAntonymsRect, UseAntonymsText, ref PsychologySettings.useAntonyms);
      style.fontSize = OldSmallFontSize;

      GUI.color = UIAssets.LineColor;
      Widgets.DrawLineVertical(forbiddenRect.x, totalRect.y, totalRect.height);
      Widgets.DrawLineHorizontal(forbiddenRect.x, useAntonymsRect.yMax + BoundaryPadding, forbiddenRect.width);
      GUI.color = Color.white;

      /* Draw Big Five */
      DrawBigFive(pawn, fiveFactorRect, forbiddenRect);

      /* Draw personality node list */
      PersonalityTraitList(personalityRect, pawn);
    }

    if (PsycheHelper.PsychologyEnabled(pawn) != true)
    {
      Widgets.DrawHighlight(totalRect);
      Widgets.DrawHighlight(totalRect);
      Text.Anchor = TextAnchor.MiddleCenter;
      style.fontSize = 30;
      GUI.color = new Color(1f, 0f, 0f, 0.85f);
      Widgets.Label(totalRect, "PsycheCurrentlyDisabled".Translate());
      GUI.color = Color.white;
      style.fontSize = OldSmallFontSize;
      Text.Anchor = OldAnchor;
    }
    GUI.EndGroup();
  }

  public static string ColorizedNodeText(PersonalityNodeDef nodeDef, float displacement)
  {
    bool antoBool = PsychologySettings.useAntonyms && displacement < 0f;
    string nodeText = (antoBool ? nodeDef.antonymLabel : nodeDef.label).CapitalizeFirst();
    Color nodeColor = antoBool ? HSVtoColor(nodeDef.antonymHSV) : HSVtoColor(nodeDef.nodeHSV);
    nodeText = PsychologySettings.useColors ? nodeText == "Creative" ? CreativeRainbow : nodeText.Colorize(nodeColor) : nodeText;
    return nodeText;
  }

  public static void DrawBigFive(Pawn pawn, Rect fiveFactorRect, Rect forbiddenRect)
  {
    /* DRAW FIVE FACTOR MODEL */
    Text.Anchor = TextAnchor.MiddleCenter;
    style.fontSize = FiveFactorTitleFontSize;
    Widgets.Label(fiveFactorRect, FiveFactorText);
    style.fontSize = OldSmallFontSize;
    Text.Anchor = OldAnchor;

    fiveFactorRect.xMin -= HighlightPadding;
    fiveFactorRect.xMax += HighlightPadding;
    Widgets.DrawHighlightIfMouseover(fiveFactorRect);
    TooltipHandler.TipRegion(fiveFactorRect, delegate
    {
      return ((string)"FiveFactorDescription".Translate()).ReplaceFirst("{0}", "FiveFactorDescription0".Translate().Colorize(UIAssets.TitleColor));
    }, 521312);

    float pentaRadius = 0.38f * forbiddenRect.width;
    Vector2 pentaCenter = new Vector2(forbiddenRect.center.x, fiveFactorRect.yMax + 0.5f * forbiddenRect.width + BoundaryPadding);
    bool geometryChanged = UpdateBigFiveGeometry(pentaCenter, pentaRadius);

    // Lines only show up on Repaint; skip the matrix maths on Layout and input passes.
    if (Event.current.type == EventType.Repaint)
    {
      for (int r = 0; r < RadialCategories; r++)
      {
        List<Vector2> pentaPoints = bigFiveRings[r];
        for (int p = 0; p < 5; p++)
        {
          UIAssets.DrawLine(pentaPoints[p], pentaPoints[(p + 1) % 5], Color.white, 0.5f);
          if (r == RadialCategories - 1)
          {
            UIAssets.DrawLine(pentaCenter, pentaPoints[p], Color.white, 0.5f);
          }
        }
      }
    }

    bool ratingsChanged = !UseCachedBool;
    CalculateBigFiveRatings(pawn);
    if (geometryChanged || ratingsChanged || bigFiveYellowVertices.Count == 0)
    {
      bigFiveYellowVertices.Clear();
      for (int p = 0; p < 5; p++)
      {
        bigFiveYellowVertices.Add(RadialProjectionFromCenter(pentaCenter, Mathf.Max(0.01f, bigFiveRatings[p]) * pentaRadius, 72f * p));
      }
      bigFiveYellowVertices.Add(pentaCenter);
    }
    PolygonUtility.DrawPolygon(bigFiveYellowVertices, bigFiveYellowTriangles, UIAssets.PsycheYellowMat);

    for (int p = 0; p < 5; p++)
    {
      string letter = BigFiveLetters[p];

      style.fontSize = SexualityFontSize;
      Text.Anchor = TextAnchor.MiddleCenter;
      Widgets.Label(bigFiveLabelRects[p], BigFiveLabels[p].Colorize(PsychologySettings.useColors ? BigFiveColors[p] : Color.white));
      Text.Anchor = OldAnchor;
      style.fontSize = OldSmallFontSize;

      List<Vector2> verticies = bigFiveHighlights[p];
      PolygonUtility.DrawPolygonHighlightIfMouseover(verticies, noTriangles);

      Color bigFiveTitleColor = PsychologySettings.useColors ? BigFiveColors[p] : ColoredText.TipSectionTitleColor;
      float bigFiveRating = bigFiveRatings[p];
      PolygonTooltipHandler.TipRegion(verticies, delegate
      {
        string bigFiveDescription = ("BigFive" + letter + "Description").Translate();
        bigFiveDescription = bigFiveDescription.ReplaceFirst("{0}", ("BigFive" + letter + "Description0").Translate().Colorize(bigFiveTitleColor));
        if (ShowNumbersBool)
        {
          bigFiveDescription += "\n\nRating: " + bigFiveRating.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
        }
        return bigFiveDescription;
      }, 38975 + p * 237);
    }
  }

  // Builds the axis labels once, and the radar's rings, highlight wedges and label rects whenever the centre or radius
  // changes. Returns true if the geometry was rebuilt.
  private static bool UpdateBigFiveGeometry(Vector2 pentaCenter, float pentaRadius)
  {
    if (BigFiveLabels == null)
    {
      BigFiveLabels = new string[5];
      BigFiveLabelSizes = new Vector2[5];
      int oldFontSize = style.fontSize;
      style.fontSize = SexualityFontSize;
      for (int p = 0; p < 5; p++)
      {
        BigFiveLabels[p] = "<b>" + ("BigFive" + BigFiveLetters[p]).Translate() + "</b>";
        Vector2 labelSize = Text.CalcSize(BigFiveLabels[p]);
        labelSize.x *= 1.1f;
        BigFiveLabelSizes[p] = labelSize;
      }
      style.fontSize = oldFontSize;
    }
    if (pentaCenter == bigFiveGeometryCenter && pentaRadius == bigFiveGeometryRadius)
    {
      return false;
    }
    bigFiveGeometryCenter = pentaCenter;
    bigFiveGeometryRadius = pentaRadius;
    for (int r = 0; r < RadialCategories; r++)
    {
      bigFiveRings[r] = GetPentagonVerticies(pentaCenter, (r + 1) * pentaRadius / RadialCategories);
    }
    for (int p = 0; p < 5; p++)
    {
      bigFiveHighlights[p] = new List<Vector2>
      {
        pentaCenter,
        RadialProjectionFromCenter(pentaCenter, SideScaling * HighlightRadius, 72f * (p - 0.5f)),
        RadialProjectionFromCenter(pentaCenter, HighlightRadius, 72f * p),
        RadialProjectionFromCenter(pentaCenter, SideScaling * HighlightRadius, 72f * (p + 0.5f))
      };
      Vector2 letterSize = BigFiveLabelSizes[p];
      Vector2 tightSize = new Vector2(letterSize.x, 0.65f * letterSize.y);
      float displacement = 0.61f * (tightSize * BigFiveSpacings[p]).magnitude;
      Vector2 position = RadialProjectionFromCenter(pentaCenter, pentaRadius + displacement, 72 * p);
      bigFiveLabelRects[p] = new Rect(position.x - 0.5f * letterSize.x, position.y - 0.46f * letterSize.y, letterSize.x, letterSize.y);
    }
    return true;
  }

  public static void PersonalityTraitList(Rect personalityRect, Pawn pawn)
  {
    if (UseCachedBool)
    {
      DrawTraitList(personalityRect);
      return;
    }
    LabelNodeList.Clear();

    List<string> nodeTextList = new List<string>();
    foreach (PersonalityNode node in PsycheHelper.Comp(pawn).Psyche.PersonalityNodes)
    {
      float displacement = 2f * node.AdjustedRating - 1f;
      string nodeText = ColorizedNodeText(node.def, displacement);
      float yAxis = PsychologySettings.useAntonyms ? Mathf.Abs(displacement) : displacement;
      int category = Mathf.RoundToInt(3f + 3f * yAxis * Mathf.Sqrt(Mathf.Abs(yAxis)));
      if (Mathf.Abs(category - 3) >= PsychologySettings.displayOption)
      {
        string categoryText = ("Psyche" + NodeDescriptions[category]).Translate();
        LabelNodeList.Add(new Tuple<string, PersonalityNode, string>(nodeText, node, categoryText.Colorize(CategoryColors[category])));
        nodeTextList.Add(PsychologySettings.useAntonyms && displacement < 0f ? node.def.antonymLabel : node.def.label);
      }
    }
    if (PsychologySettings.listAlphabetical)
    {
      LabelNodeList = LabelNodeList.OrderBy(tup => nodeTextList[LabelNodeList.IndexOf(tup)]).ToList();
    }
    else if (PsychologySettings.useAntonyms)
    {
      LabelNodeList = LabelNodeList.OrderBy(tup => -Mathf.Abs(tup.Item2.AdjustedRating - 0.5f)).ToList();
    }
    else
    {
      LabelNodeList = LabelNodeList.OrderBy(tup => -tup.Item2.AdjustedRating).ToList();
    }
    DrawTraitList(personalityRect);
  }

  public static void DrawTraitList(Rect personalityRect)
  {
    Text.Font = GameFont.Small;
    GUIStyle style = Text.fontStyles[1];
    TextAnchor oldAnchor = Text.Anchor;
    float yCompression = 0.9f;
    float highlightShift = 0.07f;

    Rect viewRect = new Rect(0f, 0f, personalityRect.width - 20f, LabelNodeList.Count() * yCompression * CategoryNodeHeight + 3f);
    Widgets.BeginScrollView(personalityRect, ref NodeScrollPosition, viewRect);
    float categoryNodeVerticalPosition = 0f;
    float xSpacing = Mathf.Max(BoundaryPadding, 0.33f * (viewRect.width - NodeWidth - CategoryWidth));
    float categoryRectX = viewRect.center.x - 0.5f * (CategoryWidth + xSpacing + NodeWidth);
    Rect categoryRect = new Rect(categoryRectX, categoryNodeVerticalPosition, CategoryWidth, CategoryNodeHeight);
    Rect nodeRect = new Rect(categoryRect.xMax + xSpacing, categoryNodeVerticalPosition, NodeWidth, CategoryNodeHeight);
    Rect highlightRect = categoryRect;
    highlightRect.xMin = viewRect.xMin;
    highlightRect.xMax = viewRect.xMax;
    highlightRect.height *= yCompression;

    style.fontSize = CategoryNodeFontSize;
    for (int j = 0; j < LabelNodeList.Count(); j++)
    {
      categoryRect.y = categoryNodeVerticalPosition;
      nodeRect.y = categoryNodeVerticalPosition;
      highlightRect.y = categoryNodeVerticalPosition + highlightShift * CategoryNodeHeight;

      Text.Anchor = TextAnchor.MiddleLeft;
      Widgets.Label(nodeRect, LabelNodeList[j].Item1);
      Widgets.Label(categoryRect, LabelNodeList[j].Item3);
      Text.Anchor = oldAnchor;
      DrawHighlightAndTooltip(highlightRect, LabelNodeList[j].Item2, j);
      categoryNodeVerticalPosition += yCompression * CategoryNodeHeight;
    }
    style.fontSize = OldSmallFontSize;

    Widgets.EndScrollView();

  }

  public static void PersonalityWordCloud(Rect totalRect, Vector2 cloudCenter, Rect forbiddenRect, Pawn pawn)//, Rect editPsycheRect)
  {
    if (UseCachedBool)
    {
      DrawWordCloud();
      return;
    }

    Rect cloudRect = totalRect.ContractedBy(BoundaryPadding);
    Rect forbiddenRect1 = forbiddenRect.ExpandedBy(BoundaryPadding);
    List<PersonalityNode> allNodes = (from n in PsycheHelper.Comp(pawn).Psyche.PersonalityNodes
                                      orderby Mathf.Abs(n.AdjustedRating - 0.5f) descending, n.def.defName
                                      select n).ToList();

    // Ratings drift slowly, so on most refreshes the words and their order are unchanged: keep the old layout then.
    List<string> layoutKey = new List<string>(allNodes.Count + 1) { pawn.thingIDNumber.ToString() };
    PsychologySettings.useAntonyms = true;
    foreach (PersonalityNode node in allNodes)
    {
      layoutKey.Add(ColorizedNodeText(node.def, node.AdjustedRating - 0.5f));
    }
    PsychologySettings.useAntonyms = UseAntonymsCached;
    if (totalRect == CloudLayoutRect && layoutKey.SequenceEqual(CloudLayoutKey))
    {
      DrawWordCloud();
      return;
    }
    CloudLayoutKey.Clear();
    CloudLayoutKey.AddRange(layoutKey);
    CloudLayoutRect = totalRect;

    CloudTextRects.Clear();
    CloudTightRects.Clear();
    CloudTexts.Clear();
    CloudNodes.Clear();
    CloudFontSizes.Clear();
    Text.Font = GameFont.Medium;
    GUIStyle style = Text.fontStyles[2];
    float lengthX = totalRect.xMax - cloudCenter.x;
    float lengthY = cloudCenter.y - totalRect.y;
    float dt = 1f / PointsOnSpiral;
    float dl = kSpiral * dt;
    for (int i = 0; i < allNodes.Count(); i++)
    {
      PersonalityNode node = allNodes[i];
      float displacement = node.AdjustedRating - 0.5f;

      PsychologySettings.useAntonyms = true;
      string personalityText = ColorizedNodeText(node.def, displacement);
      PsychologySettings.useAntonyms = UseAntonymsCached;
      style.fontSize = Mathf.RoundToInt(Mathf.Lerp(40f, 10f, Mathf.Pow(i / 36f, 0.75f)));
      Vector2 textSize = Text.CalcSize(personalityText.Colorize(Color.white));
      textSize.x *= calcSizeScalingVector.x;
      Rect textRect = new Rect(cloudCenter - 0.5f * textSize, textSize);
      Rect tightRect = textRect;
      tightRect.xMin = textRect.xMin + xMinTightScaling * textRect.width;
      tightRect.xMax = textRect.xMax - xMaxTightScaling * textRect.width;
      tightRect.yMin = textRect.yMin + yMinTightScaling * textRect.height;
      tightRect.yMax = textRect.yMax - yMaxTightScaling * textRect.height;
      float x0 = tightRect.x;
      float y0 = tightRect.y;
      int sign = Rand.ValueSeeded(3 * pawn.GetHashCode() + 11 * i) < 0.5f ? 1 : -1;
      float alpha0 = 2f * Mathf.PI * Rand.ValueSeeded(node.GetHashCode() + 71 * i);
      for (float t = dt; t < 1.414f; t += dt)
      {
        if (RectDoesNotOverlapWordCloud(tightRect, cloudRect, forbiddenRect1))//, forbiddenRect2))
        {
          textRect.x = tightRect.x - xMinTightScaling * textRect.width;
          textRect.y = tightRect.y - yMinTightScaling * textRect.height;
          CloudTextRects.Add(textRect);
          CloudTightRects.Add(tightRect);
          CloudTexts.Add(personalityText);
          CloudNodes.Add(node);
          CloudFontSizes.Add(style.fontSize);
          break;
        }
        tightRect.x = x0 + lengthX * t * Mathf.Cos(alpha0);
        tightRect.y = y0 + lengthY * t * Mathf.Sin(alpha0) * sign;
        alpha0 += dl;
      }
    }
    style.fontSize = OldMediumFontSize;
    Text.Font = GameFont.Small;
    DrawWordCloud();
  }

  public static void DrawWordCloud()
  {
    Text.Font = GameFont.Medium;
    GUIStyle style = Text.fontStyles[2];
    TextAnchor oldAnchor = Text.Anchor;
    Text.Anchor = TextAnchor.MiddleCenter;
    for (int i = 0; i < CloudTextRects.Count(); i++)
    {
      style.fontSize = CloudFontSizes[i];
      Widgets.Label(CloudTextRects[i], CloudTexts[i]);
      DrawHighlightAndTooltip(CloudTightRects[i], CloudNodes[i], i);
    }
    style.fontSize = OldMediumFontSize;
    Text.Anchor = oldAnchor;
    Text.Font = GameFont.Small;
  }

  public static bool RectDoesNotOverlapWordCloud(Rect foundRectangle, Rect totalRect, Rect forbiddenRect1)//, Rect forbiddenRect2)
  {
    foreach (Rect rect in CloudTightRects)
    {
      if (foundRectangle.Overlaps(rect))
      {
        return false;
      }
    }
    if (foundRectangle.Overlaps(forbiddenRect1))// || foundRectangle.Overlaps(forbiddenRect2))
    {
      return false;
    }
    if (foundRectangle.x < totalRect.x || totalRect.xMax < foundRectangle.xMax || foundRectangle.y < totalRect.y || totalRect.yMax < foundRectangle.yMax)
    {
      return false;
    }
    return true;
  }

  public static void DrawHighlightAndTooltip(Rect highlightRect, PersonalityNode node, int i)
  {
    Widgets.DrawHighlightIfMouseover(highlightRect);
    TooltipHandler.TipRegion(highlightRect, delegate
    {
      string nodeName = node.def.descriptionLabel;
      string antonymLabel = node.def.antonymLabel;
      if (PsychologySettings.useColors)
      {
        Color nodeColor = HSVtoColor(node.def.nodeHSV);
        Color antoColor = HSVtoColor(node.def.antonymHSV);
        nodeName = nodeName == "Creative" ? CreativeRainbow : nodeName.Colorize(nodeColor);
        antonymLabel = antonymLabel.Colorize(antoColor);
      }
      else
      {
        nodeName = nodeName.Colorize(ColoredText.TipSectionTitleColor);
        antonymLabel = antonymLabel.Colorize(ColoredText.TipSectionTitleColor);
      }
      string tooltipString = node.def.description.ReplaceFirst("{0}", nodeName) + ((string)"AntonymColon".Translate()).ReplaceFirst("{0}", antonymLabel);

      List<string> convoTopics = node.def.conversationTopics;
      if (convoTopics != null)
      {
        string convoString = "\n\n" + "ConversationTooltip".Translate(string.Join("PsycheComma".Translate(), convoTopics.Take(convoTopics.Count - 1).ToArray()), convoTopics.Last());
        tooltipString += convoString;
      }
      if (ShowNumbersBool)
      {
        string rawRating = node.rawRating.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
        string adjRating = node.AdjustedRating.ToString(RatingFormat, CultureInfo.InvariantCulture).Colorize(UIAssets.RatingColor);
        tooltipString += "\n\nRaw: " + rawRating + "  Adjusted: " + adjRating;
      }
      return tooltipString;
    }, 613261 + 612 * i);
  }

  public static Color HSVtoColor(Vector3 HSV)
  {
    float H = GenMath.PositiveMod(HSV.x, 360f);
    float S = HSV.y;
    float V = HSV.z;
    float m = V * (1f - S);
    float z = V * S * (1f - Mathf.Abs((H / 60f % 2f) - 1f)) + m;
    if (0f <= H && H < 60f)
    {
      return new Color(V, z, m);
    }
    if (60f <= H && H < 120f)
    {
      return new Color(z, V, m);
    }
    if (120f <= H && H < 180f)
    {
      return new Color(m, V, z);
    }
    if (180f <= H && H < 240f)
    {
      return new Color(m, z, V);
    }
    if (240f <= H && H < 300f)
    {
      return new Color(z, m, V);
    }
    else
    {
      return new Color(V, m, z);
    }
  }

  public static Vector2 RadialProjectionFromCenter(Vector2 center, float radius, float angle)
  {
    float x = center.x + radius * Mathf.Sin(Mathf.Deg2Rad * angle);
    float y = center.y - radius * Mathf.Cos(Mathf.Deg2Rad * angle);
    return new Vector2(x, y);
  }

  public static List<Vector2> GetPentagonVerticies(Vector2 center, float radius)
  {
    List<Vector2> verticies = new List<Vector2>();
    for (int p = 0; p < 5; p++)
    {
      verticies.Add(RadialProjectionFromCenter(center, radius, 72 * p));
    }
    return verticies;
  }

  // Big Five scores for the radar: the pawn's probit-scaled ratings projected onto each Big Five direction
  // (PersonalityNodeMatrix.bigFiveVectors), normalised by that direction's standard deviation.
  public static void CalculateBigFiveRatings(Pawn pawn)
  {
    if (UseCachedBool)
    {
      return;
    }

    double[] gaussianList = new double[PersonalityNodeMatrix.order];
    foreach (PersonalityNode node in PsycheHelper.Comp(pawn).Psyche.PersonalityNodes)
    {
      int index = PersonalityNodeMatrix.indexDict[node.def];
      gaussianList[index] = PsycheHelper.NormalCDFInv(node.AdjustedRating);
    }

    double[] projection = PersonalityNodeMatrix.bigFiveMatrix.MultiplyVector(gaussianList);
    for (int p = 0; p < 5; p++)
    {
      bigFiveRatings[p] = PsycheHelper.NormalCDF((float)projection[p]);
    }

  }

  // Legacy method
  public static void DrawPsycheMenuCard(Rect totalRect, Pawn pawn)
  {
    Find.WindowStack.Add(new Dialog_ViewPsyche(pawn, true));
  }

  //Legacy method
  public static void DrawDebugOptions(Rect totalRect, Pawn pawn)
  {
    Find.WindowStack.Add(new Dialog_EditPsyche(pawn));
  }

  public static void CheckIfCachedChanged(Pawn pawn)
  {
    bool bool0 = Time.realtimeSinceStartup < nextRefreshTime;
    bool bool1 = PawnCached == pawn;
    bool bool2 = DisplayOptionCached == PsychologySettings.displayOption;
    bool bool3 = UseColorsCached == PsychologySettings.useColors;
    bool bool4 = UseAntonymsCached == PsychologySettings.useAntonyms;
    bool bool5 = ListAlphabeticalCached == PsychologySettings.listAlphabetical;
    UseCachedBool = bool0 && bool1 && bool2 && bool3 && bool4 && bool5;
    if (UseCachedBool)
    {
      return;
    }
    CompPsychology comp = PsycheHelper.Comp(pawn);
    comp.Psyche.CalculateAdjustedRatings();
    nextRefreshTime = Time.realtimeSinceStartup + RefreshIntervalSeconds;
    PawnCached = pawn;
    DisplayOptionCached = PsychologySettings.displayOption;
    UseColorsCached = PsychologySettings.useColors;
    UseAntonymsCached = PsychologySettings.useAntonyms;
    ListAlphabeticalCached = PsychologySettings.listAlphabetical;

    KinseyTextCached = "KinseyRating".Translate() + " " + comp.Sexuality.kinseyRating;
    SexDriveIntCached = DriveCategory(comp.Sexuality.AdjustedSexDrive);
    RomDriveIntCached = DriveCategory(comp.Sexuality.AdjustedRomanticDrive);
    FamDriveIntCached = FamilyDriveCategory(FamilyUtility.FamilyDrive(pawn));
  }

  // Index into SexDriveText / RomDriveText.
  private static int DriveCategory(float drive)
  {
    return (drive > 1.30f) ? 4 : (drive > 0.90f) ? 3 : (drive > 0.50f) ? 2 : (drive > 0.10f) ? 1 : 0;
  }

  // Index into FamDriveText. Family drive isn't age-scaled, so it stays within 0-1.
  private static int FamilyDriveCategory(float drive)
  {
    return (drive >= 0.85f) ? 4 : (drive >= 0.65f) ? 3 : (drive >= 0.35f) ? 2 : (drive >= 0.15f) ? 1 : 0;
  }

  // Serialise / restore a pawn's psyche as a "key:value|..." string (meant for copy/paste between editors). Not called
  // anywhere in this assembly at the moment.
  public static string GetPsychology(Pawn pawn)
  {
    if (!PsycheHelper.PsychologyEnabled(pawn))
    {
      return "";
    }
    Dictionary<string, float> psychologyDict = new Dictionary<string, float>();

    CompPsychology comp = PsycheHelper.Comp(pawn);
    Pawn_SexualityTracker sexuality = comp.Sexuality;
    Pawn_PsycheTracker psyche = comp.Psyche;

    psychologyDict[nameof(sexuality.kinseyRating)] = sexuality.kinseyRating;
    psychologyDict[nameof(sexuality.sexDrive)] = sexuality.sexDrive;
    psychologyDict[nameof(sexuality.romanticDrive)] = sexuality.romanticDrive;
    psychologyDict[nameof(sexuality.familyDrive)] = sexuality.familyDrive;

    foreach (PersonalityNode node in comp.Psyche.PersonalityNodes)
    {
      psychologyDict[node.def.defName] = node.rawRating;
    }
    List<string> psychologyList = new List<string>();
    foreach (KeyValuePair<string, float> kvp in psychologyDict)
    {
      psychologyList.Add(kvp.Key + ":" + kvp.Value);
    }
    return string.Join("|", psychologyList);
  }

  public static void SetPsychology(Pawn pawn, string psychologyDataAsString)
  {
    if (psychologyDataAsString.NullOrEmpty() || !PsycheHelper.PsychologyEnabled(pawn))
    {
      return;
    }

    Dictionary<string, float> psychologyDict = new Dictionary<string, float>();
    string[] array = psychologyDataAsString.Split('|');
    float parsedFloat;
    foreach (string entry in array)
    {
      string[] pair = entry.Split(':');
      if (pair.Count() < 2)
      {
        continue;
      }
      if (float.TryParse(pair[1], out parsedFloat))
      {
        psychologyDict[pair[0]] = parsedFloat;
      }
    }

    CompPsychology comp = PsycheHelper.Comp(pawn);
    Pawn_SexualityTracker sexuality = comp.Sexuality;
    Pawn_PsycheTracker psyche = comp.Psyche;

    if (psychologyDict.TryGetValue(nameof(sexuality.kinseyRating), out parsedFloat))
    {
      sexuality.kinseyRating = Mathf.RoundToInt(parsedFloat);
    }
    if (psychologyDict.TryGetValue(nameof(sexuality.sexDrive), out parsedFloat))
    {
      sexuality.sexDrive = parsedFloat;
    }
    if (psychologyDict.TryGetValue(nameof(sexuality.romanticDrive), out parsedFloat))
    {
      sexuality.romanticDrive = parsedFloat;
    }
    if (psychologyDict.TryGetValue(nameof(sexuality.familyDrive), out parsedFloat))
    {
      sexuality.familyDrive = parsedFloat;
    }
    foreach (PersonalityNode node in psyche.PersonalityNodes)
    {
      if (psychologyDict.TryGetValue(node.def.defName, out parsedFloat))
      {
        node.rawRating = parsedFloat;
      }
    }
  }

}
