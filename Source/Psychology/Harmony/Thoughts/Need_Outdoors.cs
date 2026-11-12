using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Outdoorsy trait: makes the outdoors need fall faster indoors. Patches the three indoor fall-rate constants in
// vanilla's NeedInterval (-0.32, -0.45, -0.4) by subtracting 0.6 / 0.7 / 0.7 more for Outdoorsy pawns.
// Fragile: depends on those exact constants still being in vanilla's IL.
[HarmonyPatch(typeof(Need_Outdoors), nameof(Need_Outdoors.NeedInterval))]
public static class Need_OutdoorsPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OutdoorsyModifier(IEnumerable<CodeInstruction> instrs)
    {
        foreach (CodeInstruction itr in instrs)
        {
            yield return itr;
            float floatOperand;
            if(itr.opcode == OpCodes.Ldc_R4 && float.TryParse(""+itr.operand, out floatOperand)
                && (floatOperand == -0.32f || floatOperand == -0.45f || floatOperand == -0.4f)) 
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Need_Outdoors), "pawn"));
                yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                if (floatOperand == -0.32f)
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.6f);
                else if (floatOperand == -0.45f)
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.7f);
                else if (floatOperand == -0.4f)
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.7f);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Need_OutdoorsPatch), nameof(Need_OutdoorsPatch.HasOutdoorsyTraitVal), new Type[] { typeof(Pawn), typeof(float), typeof(float) }));
                yield return new CodeInstruction(OpCodes.Sub);
            }
        }
    }

    public static float HasOutdoorsyTraitVal(Pawn pawn, float val1, float val2)
    {
        return (pawn.story.traits.HasTrait(TraitDefOfPsychology.Outdoorsy) ? val2 : val1);
    }
}
