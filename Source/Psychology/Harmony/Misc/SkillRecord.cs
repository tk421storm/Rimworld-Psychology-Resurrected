using System;using Verse;using RimWorld;using HarmonyLib;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using System.Linq;

namespace Psychology.Harmony;// Skills shape personality (PersonalityNodeDef.skillModifiers), so whenever SkillRecord.Learn changes levelInt
// (the two `levelInt = levelInt +/- 1` spots), recalculate the pawn's adjusted ratings.
[HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]public static class SkillRecord_Learn_Patch{    [HarmonyTranspiler]    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)    {        int count = 0;        List<CodeInstruction> codeList = codes.ToList();        FieldInfo levelInt = AccessTools.Field(typeof(SkillRecord), "levelInt");        for (int i = 0; i < codeList.Count(); i++)
        {
            yield return codeList[i];
            if (i < 3)
            {
                continue;
            }
            bool bool0 = codeList[i - 3].LoadsField(levelInt);
            bool bool2 = codeList[i - 1].opcode == OpCodes.Add || codeList[i - 1].opcode == OpCodes.Sub;
            bool bool3 = codeList[i - 0].StoresField(levelInt);
            if (bool0 && bool2 && bool3)
            {
                count++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return CodeInstruction.LoadField(typeof(SkillRecord), "pawn");
                yield return CodeInstruction.Call(typeof(SkillRecord_Learn_Patch), nameof(SkillRecord_Learn_Patch.RecalcAdjustedRatings), new Type[] { typeof(Pawn) });
            }
        }
        if (count == 2)
        {
            yield break;
        }
        Log.Error("Psychology: SkillRecord.Learn patch unsuccessful");
    }

    public static void RecalcAdjustedRatings(Pawn pawn)
    {
        if (PsycheHelper.PsychologyEnabled(pawn) != true)
        {
            return;
        }
        PsycheHelper.Comp(pawn).Psyche.CalculateAdjustedRatings();
    }
}
