using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology;

/// <summary>
/// The per-pawn container for everything Psychology adds to a pawn. It is attached at runtime (not via XML) to every
/// ThingDef that SpeciesHelper decides is a humanlike/sapient species, see SpeciesHelper.AddCompPsychologyToHumanlikeDef.
///
/// It owns two trackers, both created lazily on first access so pawns from old saves or other mods get one too:
///   - Psyche:    the personality (one PersonalityNode per PersonalityNodeDef), see Pawn_PsycheTracker
///   - Sexuality: Kinsey rating plus sex and romance drives, see Pawn_SexualityTracker
/// It also keeps when the pawn last saw their lover (used by the long-distance-relationship thought).
///
/// Most code reaches this through PsycheHelper.Comp(pawn) after checking PsycheHelper.PsychologyEnabled(pawn).
/// </summary>
public class CompPsychology : ThingComp
{
    private Pawn_SexualityTracker sexuality;
    private Pawn_PsycheTracker psyche;
    //public Pawn_TourMemories recruiting;
    // Absolute tick when this pawn last shared a map with their lover; see ThoughtWorker_LDR.
    private int tickSinceLastSeenLover;

    public Pawn_SexualityTracker Sexuality
    {
        get
        {
            if (this.sexuality == null)
            {
                Pawn p = this.parent as Pawn;
                if (p != null)
                {
                    this.sexuality = new Pawn_SexualityTracker(p);
                    this.sexuality.GenerateSexuality();
                }
                else
                {
                    Log.Error("Psychology :: CompPsychology was added to " + this.parent.Label + " which cannot be cast to a Pawn.");
                }
            }
            return this.sexuality;
        }
        set
        {
            this.sexuality = value;
        }
    }

    public Pawn_PsycheTracker Psyche
    {
        get
        {
            if (this.psyche == null)
            {
                Pawn p = this.parent as Pawn;
                if (p != null)
                {
                    this.psyche = new Pawn_PsycheTracker(p);
                    this.psyche.Initialize();
                }
                else
                {
                    Log.Error("Psychology :: CompPsychology was added to " + this.parent.Label + " which cannot be cast to a Pawn.");
                }
            }
            return this.psyche;
        }
        set
        {
            this.psyche = value;
        }
    }

    public int LDRTick
    {
        get
        {
            return tickSinceLastSeenLover;
        }
        set
        {
            tickSinceLastSeenLover = value;
        }
    }

    // Note the getters above create the trackers on demand, so this is effectively "parent is a Pawn".
    public bool IsPsychologyPawn
    {
        get
        {
            return this.Psyche != null && this.Sexuality != null;
        }
    }

    // Dev mode: the romance testing menu on colonists (RomanceDebugUtility).
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        if (Prefs.DevMode && this.parent is Pawn pawn && pawn.Spawned && pawn.IsColonist && PsycheHelper.PsychologyEnabled(pawn))
        {
            yield return RomanceDebugUtility.RomanceGizmo(pawn);
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref this.sexuality, "sexuality", new object[]
        {
            this.parent as Pawn
        });
        Scribe_Deep.Look(ref this.psyche, "psyche", new object[]
        {
            this.parent as Pawn
        });
        /*Scribe_Deep.Look(ref this.recruiting, "recruiting", new object[]
        {
            this.parent as Pawn
        });*/
        Scribe_Values.Look(ref this.tickSinceLastSeenLover, "tickSinceLastSeenLover", Find.TickManager.TicksAbs);
    }

    /// <summary>
    /// Replaces this pawn's psyche and sexuality with a copy of another pawn's. Used when one pawn object "becomes" another,
    /// e.g. Pawnmorpher former humans and Character Editor/Prepare Carefully re-creating pawns. Raw ratings are copied;
    /// adjusted ratings are recalculated from this pawn's own traits/skills.
    /// </summary>
    public void DeepCopyFromOtherComp(CompPsychology otherComp)
    {
        Pawn pawn = this.parent as Pawn;
        this.sexuality = new Pawn_SexualityTracker(pawn);
        this.sexuality.DeepCopyFromOtherTracker(otherComp.Sexuality);
        this.psyche = new Pawn_PsycheTracker(pawn);
        this.psyche.DeepCopyFromOtherTracker(otherComp.Psyche);
        this.tickSinceLastSeenLover = otherComp.LDRTick;
        try
        {
            Pawn otherPawn = otherComp.parent as Pawn;
        }
        catch (Exception ex)
        {
            Log.Warning("CompPsychology, deep copy complete for pawn = " + pawn + ", " + pawn.def.label + ", cast of otherComp.parent failed because of " + ex);
        }
    }
}
