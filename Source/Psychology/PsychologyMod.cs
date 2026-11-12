using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;
using Verse.Noise;
using Unity;

namespace Psychology;

// ToDo:
// - Make indoctrination texture with hands hovering over a brain
// - Make setting to control magnitude of the conversation opinion offsets
// - Make panic attack a mental break

/// <summary>
/// Mod entry point: loads PsychologySettings and draws the settings window. The Harmony patches are applied separately
/// by Harmony/HarmonyInitialize.cs once defs are loaded.
/// </summary>
public class PsychologyMod : Mod
{
  public static PsychologySettings settings;
  public static int lastDrawFrame;
  public static bool initializeLastDrawFrame = true;

  public override string SettingsCategory() => "Psychology Resurrected";

  public PsychologyMod(ModContentPack content) : base(content)
  {
    settings = this.GetSettings<PsychologySettings>();
  }

  public override void DoSettingsWindowContents(Rect inRect)
  {
    if (initializeLastDrawFrame)
    {
      lastDrawFrame = Time.frameCount - 10;
    }
    if (Time.frameCount - lastDrawFrame > 5)
    {
      SettingsWindowUtility.Initialize();
    }
    SettingsWindowUtility.DrawSettingsWindow(inRect);
    lastDrawFrame = Time.frameCount;
  }

  public override void WriteSettings()
  {
    base.WriteSettings();
    global::Psychology.Harmony.ArrestChanceSetting.Apply();
  }
}
