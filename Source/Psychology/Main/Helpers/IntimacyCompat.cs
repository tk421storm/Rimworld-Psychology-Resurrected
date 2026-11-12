using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Psychology;

/// <summary>
/// Soft hook into Intimacy - Friends n' Lovers (LovelyDovey.Sex.WithEuterpe). Its need only rises from interactions it
/// marks as intimate, a flat amount when the interaction fires; this lets Psychology add social intimacy at other times
/// (e.g. when a long conversation ends). Found by reflection, so nothing happens when Intimacy isn't loaded.
/// </summary>
public static class IntimacyCompat
{
  private static bool initialized;
  private static Type needType;
  private static MethodInfo gainSocialIntimacy;

  public static bool Active
  {
    get
    {
      if (!initialized)
      {
        initialized = true;
        if (ModsConfig.IsActive("LovelyDovey.Sex.WithEuterpe"))
        {
          needType = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.Need_Intimacy");
          gainSocialIntimacy = needType == null ? null : AccessTools.Method(needType, "GainSocialIntimacy", new[] { typeof(float) });
        }
      }
      return gainSocialIntimacy != null;
    }
  }

  // Adds intimacy through Intimacy's own social gain, so its cap on intimacy from social activities still applies.
  public static void GainSocialIntimacy(Pawn pawn, float amount)
  {
    if (!Active || amount <= 0f || pawn?.needs == null)
    {
      return;
    }
    Need need = pawn.needs.AllNeeds.Find(n => needType.IsInstanceOfType(n));
    if (need != null)
    {
      gainSocialIntimacy.Invoke(need, new object[] { amount });
    }
  }
}
