using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// "Compulsion" break: obsessive cleaning or hauling (JobGiver_Compulsion).
public class MentalState_Compulsion : MentalState
{
    public override RandomSocialMode SocialModeMax()
    {
        return RandomSocialMode.Off;
    }
}
