using Verse;
using RimWorld;

namespace Psychology
{
    // Matriarch/Patriarch trait mood: one stage per child (FamilyUtility.ChildCount, counting one on the way), the last
    // for the def's final stage count or more; nothing without children.
    public class ThoughtWorker_FamilySize : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!FamilyUtility.IsMatriarch(p))
            {
                return ThoughtState.Inactive;
            }
            int children = FamilyUtility.ChildCount(p);
            if (children == 0)
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(System.Math.Min(children, this.def.stages.Count) - 1);
        }
    }

    // Child-free trait mood: miserable while raising a child who isn't grown up yet, or expecting one (1); content
    // without any children (0); nothing once all their children are adults.
    public class ThoughtWorker_ChildFree : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!FamilyUtility.IsChildFree(p))
            {
                return ThoughtState.Inactive;
            }
            if (FamilyUtility.ChildCount(p, youngOnly: true) > 0)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            return FamilyUtility.ChildCount(p) > 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(0);
        }
    }
}
