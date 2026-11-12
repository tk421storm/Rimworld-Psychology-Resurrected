using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Pawns that are in a conversation (HoldingConversation hediff) can't start or receive other random
// social interactions, so a conversation isn't interrupted by chitchat.
[HarmonyPatch(typeof(SocialInteractionUtility), "CanReceiveRandomInteraction")]
public static class InteractionUtility_CanReceive_Patch
{
    [HarmonyPostfix]
    public static void PsychologyAddonsForCanReceive(ref bool __result, Pawn p)
    {
        __result = __result && !p.health.hediffSet.HasHediff(HediffDefOfPsychology.HoldingConversation);
    }
}

[HarmonyPatch(typeof(SocialInteractionUtility), "CanInitiateRandomInteraction", new[] { typeof(Pawn) })]
public static class InteractionUtility_CanInitiate_Patch
{
    [HarmonyPostfix]
    public static void PsychologyAddonsForCanInitiate(ref bool __result, Pawn p)
    {
        __result = __result && !p.health.hediffSet.HasHediff(HediffDefOfPsychology.HoldingConversation);
    }
}

// Social fights between humans shouldn't involve biting: re-roll the melee verb (up to 20 times) if vanilla picked a bite.
[HarmonyPatch(typeof(SocialInteractionUtility), nameof(SocialInteractionUtility.TryGetRandomVerbForSocialFight))]
public static class InteractionUtility_SocialFightVerb_Patch
{
    [HarmonyPostfix]
    public static void RemoveBiting(ref Verb verb, Pawn p)
    {
        if (verb == null)
        {
            return;
        }
        if (verb.verbProps.meleeDamageDef.label != "bite")
        {
            return;
        }
        int iter = 0;
        while (iter < 20)
        {
            SocialInteractionUtility.TryGetRandomVerbForSocialFight(p, out Verb v);
            if (v != null)
            {
                if (v.verbProps?.meleeDamageDef?.label != "bite")
                {
                    verb = v;
                    return;
                }
            }
            iter++;
        }
    }
}
