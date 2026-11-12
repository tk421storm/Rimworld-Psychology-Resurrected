using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Sound;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Adds the Psyche button next to the info button on the character card: replaces the Widgets.InfoCardButton call with
// PsycheCardButton, which draws our button (opening Dialog_ViewPsyche) and then the original info button.
[HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard))]
public static class CharacterCardUtility_DrawCharacterCard_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        MethodInfo methodInfoInfoCardButton = AccessTools.Method(typeof(Widgets), nameof(Widgets.InfoCardButton), new Type[] { typeof(float), typeof(float), typeof(Thing) });
        foreach (CodeInstruction c in codes)
        {
            if (c.Calls(methodInfoInfoCardButton))
            {
                yield return CodeInstruction.Call(typeof(CharacterCardUtility_DrawCharacterCard_Patch), nameof(PsycheCardButton));
                continue;
            }
            yield return c;
        }
    }

    public static bool PsycheCardButton(float x, float y, Pawn pawn)
    {
        if (PsycheHelper.PsychologyEnabled(pawn))
        {
            Rect rect = new Rect(x + 23f, y - 3f, 30f, 30f);
            Color oldColor = GUI.color;
            GUI.color = rect.Contains(Event.current.mousePosition) ? UIAssets.ButtonLightColor : UIAssets.ButtonDarkColor;
            GUI.DrawTexture(rect, UIAssets.PsycheButton);
            if (Widgets.ButtonInvisible(rect, false))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                Find.WindowStack.Add(new Dialog_ViewPsyche(pawn, Prefs.DevMode));
            }
            GUI.color = oldColor;
        }
        return Widgets.InfoCardButton(x, y, pawn);
    }

}
