using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology
{
    [DefOf]
    public static class RulePackDefOfPsychology
    {
        public static RulePackDef Sentence_SocialFightConvoInitiatorStarted;
        public static RulePackDef Sentence_SocialFightConvoRecipientStarted;
        // Casual lovin
        public static RulePackDef HookupSucceeded;
        public static RulePackDef HookupFailed;
        public static RulePackDef HookupNoBed;
        // Romance attempt types (RomanceAttemptRouter)
        public static RulePackDef RomanceAttemptDirect;
        public static RulePackDef RomanceAttemptDateNight;
        public static RulePackDef RomanceAttemptDateLovin;
        public static RulePackDef DateLovinAccepted;
        public static RulePackDef DateLovinDeclined;
        public static RulePackDef RomanceAttemptCourtship;
        public static RulePackDef RomanceAttemptHookup;
        public static RulePackDef RomanceAttemptCheating;
        // Courtship
        public static RulePackDef DateAccepted;
        public static RulePackDef DateDeclined;
        // Hugs
        public static RulePackDef HugAccepted;
        public static RulePackDef HugDeclined;
    }
}
