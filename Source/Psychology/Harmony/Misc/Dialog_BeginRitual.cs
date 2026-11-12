using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology.Harmony;

// Ritual dialog (and gravship launch, which uses it): below vanilla's "some participants are sleeping", a line for each
// selected participant's planned date (whether they planned it or were asked) that falls during the ritual: "will" if it
// starts before the ritual's shortest end, "may" if before its longest end plus an hour to gather. Such a date is pushed
// back while they're busy (Hediff_PlannedDate.IsBusy), or cancelled if the ritual runs long.
[HarmonyPatch(typeof(Dialog_BeginRitual), nameof(Dialog_BeginRitual.DrawOutcomeWarnings))]
public static class Dialog_BeginRitual_DateWarningPatch
{
    private const int GatherTicks = GenDate.TicksPerHour;
    // Vanilla's default ritual length (RitualBehaviorDef.durationTicks).
    private static readonly IntRange DefaultDuration = new IntRange(7500, 7500);

    private static readonly AccessTools.FieldRef<Dialog_BeginRitual, RitualRoleAssignments> assignments = AccessTools.FieldRefAccess<Dialog_BeginRitual, RitualRoleAssignments>("assignments");
    private static readonly AccessTools.FieldRef<Dialog_BeginRitual, Precept_Ritual> ritual = AccessTools.FieldRefAccess<Dialog_BeginRitual, Precept_Ritual>("ritual");
    private static readonly AccessTools.FieldRef<Dialog_BeginRitual, Map> map = AccessTools.FieldRefAccess<Dialog_BeginRitual, Map>("map");

    [HarmonyPostfix]
    public static void WarnAboutDates(Dialog_BeginRitual __instance, Rect viewRect, ref float curY, ref float totalInfoHeight)
    {
        List<Pawn> participants = assignments(__instance)?.Participants;
        Map ritualMap = map(__instance);
        if (participants.NullOrEmpty() || ritualMap == null)
        {
            return;
        }
        IntRange duration = ritual(__instance)?.behavior?.def?.durationTicks ?? DefaultDuration;
        int now = Find.TickManager.TicksAbs;
        foreach (Hediff_PlannedDate date in PlannedDates(ritualMap, participants))
        {
            int start = date.StartTick;
            string key = start < now + duration.min ? "RitualDateWillClash" : start < now + duration.max + GatherTicks ? "RitualDateMightClash" : null;
            if (key == null)
            {
                continue;
            }
            string text = key.Translate(date.pawn.Named("PAWN"), date.partner.Named("PARTNER"), RendezvousUtility.DateKind(date.courtship).Named("KIND"), date.hour.Named("HOUR"), (start - now).ToStringTicksToPeriod().Named("IN"));
            float height = Mathf.Max(Text.CalcHeight(text, viewRect.width) + 3f, 28f);
            Widgets.Label(new Rect(viewRect.x, curY + 4f, viewRect.width, height), text);
            curY += height;
            totalInfoHeight += height;
        }
    }

    private static IEnumerable<Hediff_PlannedDate> PlannedDates(Map map, List<Pawn> participants)
    {
        return map.mapPawns.AllHumanlikeSpawned
            .SelectMany(p => p.health.hediffSet.hediffs.OfType<Hediff_PlannedDate>())
            .Where(d => d.partner != null && (participants.Contains(d.pawn) || participants.Contains(d.partner)));
    }
}
