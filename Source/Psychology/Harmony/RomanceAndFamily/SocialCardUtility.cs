using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology.Harmony;

// Social tab (Biotech): if either pawn is child-free, the pregnancy approach button is greyed out on "avoid pregnancy"
// and can't be changed; its tooltip says who is child-free. Child-free pawns always avoid pregnancy anyway
// (FamilyUtility.OnApproachRead).
[HarmonyPatch(typeof(SocialCardUtility), "DrawPregnancyApproach")]
public static class SocialCardUtility_DrawPregnancyApproachPatch
{
    private static readonly System.Type EntryType = AccessTools.Inner(typeof(SocialCardUtility), "CachedSocialTabEntry");
    private static readonly AccessTools.FieldRef<object, Pawn> otherPawn = AccessTools.FieldRefAccess<Pawn>(EntryType, "otherPawn");
    private static readonly AccessTools.FieldRef<object, PregnancyApproach?> pregnancyApproach = AccessTools.FieldRefAccess<PregnancyApproach?>(EntryType, "pregnancyApproach");

    [HarmonyPrefix]
    public static bool GreyOutForChildFree(object entry, Rect rect, Pawn selPawnForSocialInfo)
    {
        // Vanilla draws nothing in these cases.
        if (!pregnancyApproach(entry).HasValue || !Find.Storyteller.difficulty.ChildrenAllowed)
        {
            return true;
        }
        Pawn other = otherPawn(entry);
        Pawn childFree = FamilyUtility.IsChildFree(selPawnForSocialInfo) ? selPawnForSocialInfo : other != null && FamilyUtility.IsChildFree(other) ? other : null;
        if (childFree == null)
        {
            return true;
        }
        GUI.color = Color.grey;
        GUI.DrawTexture(rect, PregnancyApproach.AvoidPregnancy.GetIcon());
        GUI.color = Color.white;
        string tip = "ChildFreePregnancyApproach".Translate(childFree.Named("PAWN"));
        if (Widgets.ButtonInvisible(rect))
        {
            Messages.Message(tip, MessageTypeDefOf.RejectInput, false);
        }
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, tip);
        }
        return false;
    }
}
