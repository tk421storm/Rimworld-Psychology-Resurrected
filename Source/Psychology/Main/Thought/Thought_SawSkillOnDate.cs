using RimWorld;
using UnityEngine;
using Verse;

namespace Psychology;

// Opinion of a pawn after watching them work on a date (DateActivityWorker_ShareWork): "X is good / really good / great
// at <skill>" by their level in it (below 10, 10 to 14, 15 and up), worth half their level in opinion (0 to 10).
public class Thought_SawSkillOnDate : Thought_MemorySocial
{
  private const int ReallyGoodLevel = 10;
  private const int GreatLevel = 15;

  public SkillDef skill;

  public static void Gain(Pawn watcher, Pawn worker, SkillDef skill)
  {
    int level = worker.skills?.GetSkill(skill)?.Level ?? 0;
    var thought = (Thought_SawSkillOnDate)ThoughtMaker.MakeThought(ThoughtDefOfPsychology.SawSkillOnDate);
    thought.skill = skill;
    thought.SetForcedStage(level >= GreatLevel ? 2 : level >= ReallyGoodLevel ? 1 : 0);
    thought.opinionOffset = Mathf.Clamp(level / 2f, 0f, 10f);
    watcher.needs.mood.thoughts.memories.TryGainMemory(thought, worker);
    PsychologyDebug.Log($"Date activity ShareWork: {watcher}: \"{thought.LabelCap}\" (level {level}, opinion +{thought.opinionOffset:0.#})");
  }

  public override string LabelCap
  {
    get
    {
      if (otherPawn == null || skill == null)
      {
        return base.LabelCap;
      }
      return CurStage.label.Formatted(otherPawn.Named("OTHERPAWN"), skill.label.Named("SKILL")).Resolve().CapitalizeFirst();
    }
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Defs.Look(ref skill, "skill");
  }
}
