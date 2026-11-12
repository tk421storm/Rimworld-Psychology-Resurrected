using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    // Dancing on a date (DateActivityWorker_MusicAndDance): vanilla's dance from Ideology's dance parties (sway and hop in
    // place, its body offset reused as is), but without needing Ideology, now and then facing the partner (TargetA)
    // instead of a random way, and giving recreation (Social). Ends when the job expires.
    public class JobDriver_DateDance : JobDriver_Dance
    {
        // Vanilla's private dance-move flag, which its ForcedBodyOffset reads.
        private static readonly AccessTools.FieldRef<JobDriver_Dance, bool> jumping = AccessTools.FieldRefAccess<JobDriver_Dance, bool>("jumping");

        // As vanilla's dance: switch moves every 240 ticks, turn every 120 while swaying.
        private const int MoveChangeInterval = 240;
        private const int TurnInterval = 120;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            jumping(this) = Rand.Bool;
            toil.tickIntervalAction = delegate (int delta)
            {
                if (AgeTicks % MoveChangeInterval == 0)
                {
                    jumping(this) = !jumping(this);
                }
                if (AgeTicks % TurnInterval == 0 && !jumping(this))
                {
                    if (Rand.Bool && TargetA.IsValid)
                    {
                        pawn.rotationTracker.FaceTarget(TargetA);
                    }
                    else
                    {
                        pawn.Rotation = Rot4.Random;
                    }
                }
                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
            };
            toil.socialMode = RandomSocialMode.SuperActive;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            yield return toil;
        }
    }
}
