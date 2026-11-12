using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

// "Antisocial" break: just keeps to themselves (no random social interactions).
public class MentalState_Antisocial : MentalState
{
    public override RandomSocialMode SocialModeMax()
    {
        return RandomSocialMode.Quiet;
    }
}
