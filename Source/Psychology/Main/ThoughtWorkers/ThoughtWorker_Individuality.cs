using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Psychology;

// "Individuality" (setting): colonists with high expectations get a bad mood if someone else on the map wears the
// same outfit. Re-evaluated every ~250 calls per pawn.
public class ThoughtWorker_Individuality : ThoughtWorker
{
    private class Check
    {
        public int countdown;
        public int stage = -1;
    }

    // One ThoughtWorker serves every pawn, so each pawn's countdown and last result are kept per pawn.
    private readonly PawnStateCache<Check> checks = new PawnStateCache<Check>();

    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!PsychologySettings.enableIndividuality)
            return ThoughtState.Inactive;
        if (!p.Spawned)
            return ThoughtState.Inactive;
        if (p.Map == null)
            return ThoughtState.Inactive;
        if (!p.IsColonist)
            return ThoughtState.Inactive;
        if (p.apparel.PsychologicallyNude)
            return ThoughtState.Inactive;

        Check check = checks.For(p, out bool flag);
        if (flag)
        {
            check.countdown = p.GetHashCode() % 250;
        }
        check.countdown--;
        if (check.countdown < 0)
        {
            check.countdown = 250;
            flag = true;
        }

        if (flag)
        {
            // Disabled below high expectations. Read the expectation directly (as vanilla's Expectations thought does):
            // asking for the pawn's mood thoughts here would rebuild the situational thought list while it's being
            // enumerated.
            if (!p.IsSlave && ExpectationsUtility.CurrentExpectationFor(p) is ExpectationDef expectation && expectation.thoughtStage < 4)
            {
                check.stage = -1;
                return ThoughtState.Inactive;
            }

            // Might be faster to find the opposite
            Func<Apparel, bool> identical = delegate (Apparel x)
            {
                foreach (Apparel a in p.apparel.WornApparel)
                {
                    if (a.def == x.def && a.Stuff == x.Stuff && a.DrawColor == x.DrawColor)
                        return true;
                }
                return false;
            };
            IEnumerable<Pawn> colonists = from c in p.Map.mapPawns.FreeColonistsSpawned
                                          where c != p
                                          select c;
            IEnumerable<Pawn> sameClothes = from c in colonists
                                            where (from x in c.apparel.WornApparel
                                                   where identical(x)
                                                   select x).Count() == p.apparel.WornApparelCount && p.apparel.WornApparelCount == c.apparel.WornApparelCount
                                            select c;
            int sameClothesCount = sameClothes.Count();
            int colonistsCount = colonists.Count();
            if (sameClothesCount == colonistsCount && colonistsCount > 5)
            {
                check.stage = 3;
            }
            else if (sameClothesCount >= (colonistsCount / 2) && colonistsCount > 5)
            {
                check.stage = 2;
            }
            else if (sameClothesCount > 1)
            {
                check.stage = 1;
            }
            else if (sameClothesCount > 0)
            {
                check.stage = 0;
            }
            else
            {
                check.stage = -1;
            }
        }
        if (check.stage >= 0)
        {
            return ThoughtState.ActiveAtStage(check.stage);
        }
        return ThoughtState.Inactive;
    }

}
