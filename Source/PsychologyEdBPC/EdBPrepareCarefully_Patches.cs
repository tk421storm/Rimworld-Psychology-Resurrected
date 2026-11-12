using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using HarmonyLib;
using EdB.PrepareCarefully;

namespace Psychology.Harmony;

// EdB Prepare Carefully compatibility: a Psyche button in the Backstory panel header of the pawn-setup screen opens
// the editable psyche dialog. Prepare Carefully's presets and saved colonists don't store the psyche, so loading one
// gives the pawn a new random psyche.
[StaticConstructorOnStartup]
public static class EdBPrepareCarefully_Patches
{
    static EdBPrepareCarefully_Patches()
    {
        HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate.EdBPC");
        harmonyInstance.PatchAll();
        Log.Message("Psychology: completed patches for compatibility with Prepare Carefully.");
    }
}

[HarmonyPatch(typeof(PanelBackstory), nameof(PanelBackstory.Draw))]
public static class PanelBackstory_Draw_Patch
{
    [HarmonyPrefix]
    public static void Prefix(PanelBackstory __instance, float y)
    {
        Pawn pawn = __instance.ViewState?.CurrentPawn?.Pawn;
        if (!PsycheHelper.PsychologyEnabled(pawn))
        {
            return;
        }
        // Left of the panel's randomize button (Width - 32, header y + 9, 22x22)
        Rect rect = new Rect(__instance.Width - 64f, y + PanelModule.Margin.y + 6f, 26f, 26f);
        Color oldColor = GUI.color;
        GUI.color = rect.Contains(Event.current.mousePosition) ? UIAssets.ButtonLightColor : UIAssets.ButtonDarkColor;
        GUI.DrawTexture(rect, UIAssets.PsycheButton);
        GUI.color = oldColor;
        TooltipHandler.TipRegion(rect, "EdBPrepareCarefullyPsycheTooltip".Translate());
        if (Widgets.ButtonInvisible(rect, false))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
            Find.WindowStack.Add(new Dialog_ViewPsyche(pawn, true));
        }
    }
}
