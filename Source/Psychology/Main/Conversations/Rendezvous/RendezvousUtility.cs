using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology
{
    // Shared rules for social outings: a colony-size factor (fewer than 8 colonists -> rarer), schedule preferences per
    // hour, and "is it safe to go out" (no danger, nobody bleeding, at least 4 colonists, under half drafted).
    public static class RendezvousUtility
    {
        // Rest below which a date can't start, or is cut short: near exhaustion, not just tired (vanilla's "tired" is 28%,
        // "very tired" 14%), so a date in the evening runs its course.
        public const float DateTooTiredRest = 0.08f;

        public static float ColonySizeFactor(Pawn pawn)
        {
            return Mathf.Clamp01(pawn.Map.mapPawns.FreeColonistsCount / 8f);
        }

        // How well an hour of pawn's schedule suits a date: recreation is best, anything/meditate hours will do, and
        // sleep and work hours never (sleepers would be woken; work hours get the date cancelled).
        public static float TimeAssignmentFactor(Pawn pawn, int hour)
        {
            TimeAssignmentDef assignment = pawn.timetable?.GetAssignment(hour);
            if (assignment == null || assignment == TimeAssignmentDefOf.Joy)
            {
                return 1f;
            }
            if (assignment == TimeAssignmentDefOf.Anything || assignment == TimeAssignmentDefOf.Meditate)
            {
                return 0.35f;
            }
            return 0f;
        }

        
        public static bool AcceptableGameConditionsToStartHangingOut(Map map)
        {
            if (GatheringsUtility.AnyLordJobPreventsNewGatherings(map))
            {
                return false;
            }
            if (map.dangerWatcher.DangerRating != StoryDanger.None)
            {
                return false;
            }
            int freeColonistsSpawnedCount = map.mapPawns.FreeColonistsSpawnedCount;
            if (freeColonistsSpawnedCount < 4)
            {
                return false;
            }
            int num = 0;
            foreach (Pawn current in map.mapPawns.FreeColonistsSpawned)
            {
                if (current.health.hediffSet.BleedRateTotal > 0f)
                {
                    return false;
                }
                if (current.Drafted)
                {
                    num++;
                }
            }
            if ((float)num / (float)freeColonistsSpawnedCount >= 0.5f)
            {
                return false;
            }
            return true;
        }

        // Whether pawn got hurt during an outing, given their health when it started (so old scars or missing parts
        // don't count).
        public static bool HurtSince(Pawn pawn, float healthAtStart)
        {
            return pawn.health.summaryHealth.SummaryHealthPercent < healthAtStart;
        }

        // Whether a mental break calls off an outing (or a planned date at its start): any break but a social fight,
        // which is over in moments, with whoever it's with; the pawn rejoins once it ends.
        public static bool EndsOuting(MentalStateDef state)
        {
            return state != null && state != MentalStateDefOf.SocialFighting;
        }

        // When a planned date starts (absolute ticks): the first time from `from` on that it's `hour` on the map, which is
        // the next day when that hour has already passed on `from`'s day (see Hediff_PlannedDate.Tick).
        public static int DateStartTick(int from, int hour, Map map)
        {
            float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            return from + (hour - GenDate.HourOfDay(from, longitude) + GenDate.HoursPerDay) % GenDate.HoursPerDay * GenDate.TicksPerHour;
        }

        public static string DateKind(bool courtship)
        {
            return (courtship ? "DateKindCourtship" : "DateKindDateNight").Translate();
        }

        // A top-left message about a date (started, its outcome, cut short, cancelled...). The key gets the two pawns,
        // the kind of date and an optional reason as {0}-{3}.
        public static void DateMessage(string key, Pawn pawn, Pawn partner, bool courtship, MessageTypeDef type, string reason = null)
        {
            Messages.Message(key.Translate(pawn, partner, DateKind(courtship), reason ?? ""), new LookTargets(pawn, partner), type);
        }

        // Schedules a date (date night for a couple, or a courtship date) on the initiator as a Hediff_PlannedDate, and
        // sends the letter if date letters are on.
        public static void PlanDate(Pawn initiator, Pawn recipient, bool courtship)
        {
            //Choose a time that works with their schedule, based on their personality
            // Pick the hour: each node with preferredDateHours votes for its hours, weighted by how far both partners are from
            // average in that node (and damped by Aggressive), then weighted by both schedules (TimeAssignmentFactor). If
            // none of their preferred hours are free for both, any hour free for both; failing that, any hour neither
            // sleeps. Less Spontaneous couples plan further ahead (up to 5 days).
            Dictionary<int, float> possibleHours = new Dictionary<int, float>();
            for (int i = 0; i < GenDate.HoursPerDay; i++)
            {
                possibleHours.Add(i, 0f);
            }
            foreach (PersonalityNodeDef d in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
            {
                if(d.preferredDateHours != null)
                {
                    foreach (int h in d.preferredDateHours)
                    {
                        possibleHours[h] += (Mathf.Pow(Mathf.Abs(0.5f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(d)), 2) / (1.3f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive)) + Mathf.Pow(Mathf.Abs(0.5f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(d)), 2) / (1.3f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive)) / 2f);
                    }
                }
            }
            Func<int, float> scheduleFactor = h => TimeAssignmentFactor(initiator, h) * TimeAssignmentFactor(recipient, h);
            Func<int, float> awakeFactor = h => initiator.timetable?.GetAssignment(h) == TimeAssignmentDefOf.Sleep || recipient.timetable?.GetAssignment(h) == TimeAssignmentDefOf.Sleep ? 0f : 1f;
            Func<int, float> weight = h => possibleHours[h] * scheduleFactor(h);
            if (!possibleHours.Keys.Any(h => weight(h) > 0f))
            {
                weight = scheduleFactor;
            }
            if (!possibleHours.Keys.Any(h => weight(h) > 0f))
            {
                weight = h => (possibleHours[h] + 0.1f) * awakeFactor(h);
            }
            if (!possibleHours.Keys.Any(h => weight(h) > 0f))
            {
                weight = h => possibleHours[h] + 0.1f;
            }
            int hour = possibleHours.Keys.RandomElementByWeight(weight);
            //More Spontaneous couples will plan their dates sooner; possibly even immediately!
            int day = Find.TickManager.TicksAbs + Mathf.RoundToInt(GenDate.TicksPerDay * 5 * (((1f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Spontaneous)) + (1f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Spontaneous))) / 2f));
            Hediff_PlannedDate plannedDate = HediffMaker.MakeHediff(HediffDefOfPsychology.PlannedDate, initiator) as Hediff_PlannedDate;
            plannedDate.partner = recipient;
            plannedDate.day = day;
            plannedDate.hour = hour;
            plannedDate.courtship = courtship;
            initiator.health.AddHediff(plannedDate);
            PsycheHelper.Comp(initiator).Psyche.SetLastDateTick(recipient, day);
            PsycheHelper.Comp(recipient).Psyche.SetLastDateTick(initiator, day);
            float longitude = Find.WorldGrid.LongLatOf(initiator.Map.Tile).x;
            int startTick = DateStartTick(day, hour, initiator.Map);
            PsychologyDebug.Log($"{(courtship ? "Date" : "Date night")} planned: {initiator} and {recipient}, in {(startTick - Find.TickManager.TicksAbs) / (float)GenDate.TicksPerDay:0.0} days, on the {GenDate.QuadrumDateStringAt(startTick, longitude)} at hour {hour}");
            if(PsychologySettings.enableDateLetters)
            {
                String dateTime = GenDate.QuadrumDateStringAt(startTick, longitude);
                string labelKey = courtship ? "LetterLabelCourtshipDatePlanned" : "LetterLabelDatePlanned";
                string textKey = courtship ? "LetterCourtshipDatePlanned" : "LetterDatePlanned";
                Letter dateLetter = LetterMaker.MakeLetter(labelKey.Translate(), textKey.Translate(initiator, recipient, dateTime, hour), LetterDefOf.PositiveEvent, new LookTargets(initiator, recipient));
                Find.LetterStack.ReceiveLetter(dateLetter);
            }
        }
    }
}
