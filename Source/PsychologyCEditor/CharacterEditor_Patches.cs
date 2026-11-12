using UnityEngine;
using RimWorld;
using Verse;
using System;
using Verse.Sound;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;using System.Reflection.Emit;using System.Linq;

namespace Psychology.Harmony;

// Character Editor compatibility: replaces Character Editor's own Psychology dialog with this mod's psyche card plus
// the psyche editor (EditPsycheUtility). CEditor.API.Pawn is fetched through a DynamicMethod because the CEditor type
// is only known by name. (CE isn't installed here, so this project can't be built locally; the shipped 1.6 DLL is kept.)
[StaticConstructorOnStartup]
public class CharacterEditor_Patches
{
    public static Type typeCEditor = AccessTools.TypeByName("CharacterEditor.CEditor");
    public static MethodInfo methodAPI = AccessTools.PropertyGetter(typeCEditor, "API");
    public static MethodInfo methodPawn = AccessTools.PropertyGetter(typeCEditor, "Pawn");
    public delegate Pawn CEPawnGetter();
    public static CEPawnGetter CEditor_API_Pawn;

    static CharacterEditor_Patches()
    {
        CEditor_API_Pawn = CreateCEPawnGetter();

        MethodInfo originalInfo;
        HarmonyMethod harmonyMethod;
        HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate.CEditor");

        originalInfo = AccessTools.Method(typeof(CharacterEditor.DialogPsychology), nameof(CharacterEditor.DialogPsychology.DoWindowContents));
        harmonyMethod = new HarmonyMethod(typeof(CharacterEditor_Patches), nameof(CharacterEditor_Patches.DialogPsychology_DoWindowContentsPrefix));
        harmonyInstance.Patch(originalInfo, prefix: harmonyMethod);

        Log.Message("Psychology: completed compability patches for Character Editor");
    }

    private static CEPawnGetter CreateCEPawnGetter()
    {
        DynamicMethod dm = new DynamicMethod($"CEPawnGetter", typeof(Pawn), Array.Empty<Type>(), typeof(CharacterEditor_Patches).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Call, methodAPI);
        il.Emit(OpCodes.Call, methodPawn);
        il.Emit(OpCodes.Ret);
        return (CEPawnGetter)dm.CreateDelegate(typeof(CEPawnGetter));
    }

    public static bool DialogPsychology_DoWindowContentsPrefix(Rect inRect)
    {
        Pawn pawn = CEditor_API_Pawn();

        Rect oldRect = inRect;
        GUI.EndGroup();

        if (PsycheHelper.TryGetPawnSeed(pawn) != true)
        {
            return false;
        }
        if (PsycheHelper.PsychologyEnabled(pawn) != true)
        {
            return false;
        }
        Rect psycheRect = PsycheCardUtility.PsycheRect;
        Rect editRect = new Rect(psycheRect.xMax, psycheRect.y, EditPsycheUtility.CalculateEditWidth(pawn), psycheRect.height);

        inRect = new Rect(psycheRect.x, psycheRect.y, psycheRect.width + editRect.width, psycheRect.height);
        Find.WindowStack.currentlyDrawnWindow.windowRect = inRect;
        Find.WindowStack.currentlyDrawnWindow.windowRect.center = 0.5f * new Vector2(UI.screenWidth, UI.screenHeight);

        GUI.BeginGroup(inRect);
        PsycheCardUtility.DrawPsycheCard(psycheRect, pawn, true, true);
        EditPsycheUtility.DrawEditPsyche(editRect, pawn);
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        Widgets.DrawLineVertical(editRect.x, editRect.y, editRect.height);
        GUI.color = Color.white;
        GUI.EndGroup();

        // Added this, might cause errors?
        GUI.BeginGroup(oldRect);
        return false;
    }

    //// Transpiler method

}
