using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// "Abusive" break: the pawn seeks out colonists to insult and slight (JobGiver_Abuse / JobDriver_Abuse).
public class MentalState_Abuse : MentalState
{
    public override RandomSocialMode SocialModeMax()
    {
        return RandomSocialMode.Off;
    }
}
