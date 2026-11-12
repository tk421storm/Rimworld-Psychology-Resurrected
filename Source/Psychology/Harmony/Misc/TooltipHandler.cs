using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;


namespace Psychology.Harmony;

// Draws the custom tooltips of the Psyche tab's radar chart after vanilla tooltips (see GUI/PolygonTooltipHandler).
[HarmonyPatch(typeof(TooltipHandler), nameof(TooltipHandler.DoTooltipGUI))]
public class TooltipHandler_DoTooltipGUI_PsychologyPatch
{
    
    [HarmonyPostfix]
    public static void DoTooltipGUI()
    {
        PolygonTooltipHandler.DoTooltipGUI();
    }
}

