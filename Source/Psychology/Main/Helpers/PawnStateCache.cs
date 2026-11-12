using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Psychology;

// Per-pawn state for code that has one instance serving every pawn (e.g. a ThoughtWorker). Not saved. Cleared when a
// different game is loaded, and pawns that are dead or no longer spawned are dropped about once a day.
public class PawnStateCache<T> where T : new()
{
    private readonly Dictionary<Pawn, T> states = new Dictionary<Pawn, T>();
    private Game game;
    private int nextPruneTick;

    public T For(Pawn pawn, out bool created)
    {
        if (game != Current.Game)
        {
            states.Clear();
            game = Current.Game;
            nextPruneTick = 0;
        }
        int ticks = Find.TickManager.TicksGame;
        if (ticks >= nextPruneTick)
        {
            states.RemoveAll(kvp => kvp.Key.Dead || !kvp.Key.Spawned);
            nextPruneTick = ticks + GenDate.TicksPerDay;
        }
        created = !states.TryGetValue(pawn, out T state);
        if (created)
        {
            state = new T();
            states.Add(pawn, state);
        }
        return state;
    }
}
