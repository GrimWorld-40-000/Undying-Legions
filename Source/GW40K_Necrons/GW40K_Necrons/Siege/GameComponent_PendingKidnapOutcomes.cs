using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Persists kidnap outcomes that have been rolled but not yet revealed.
/// Outcomes fire between 3 and 7 game-days after the siege ends, simulating
/// the time it takes for news (or the pawn themselves) to reach the colony.
/// </summary>
public class GameComponent_PendingKidnapOutcomes : GameComponent
{
    private List<PendingKidnapOutcome> pending = new();

    public GameComponent_PendingKidnapOutcomes(Game game) { }

    public static GameComponent_PendingKidnapOutcomes Current =>
        Verse.Current.Game?.GetComponent<GameComponent_PendingKidnapOutcomes>();

    // ── Scheduling ────────────────────────────────────────────────────────────

    public void Schedule(int pawnId, NecronKidnapOutcome outcome, int necronFactionLoadId)
    {
        int delay = Rand.Range(3, 8) * GenDate.TicksPerDay; // 3–7 days
        pending.Add(new PendingKidnapOutcome
        {
            FireTick         = Find.TickManager.TicksGame + delay,
            PawnId           = pawnId,
            Outcome          = outcome,
            NecronFactionId  = necronFactionLoadId,
        });
    }

    // ── Tick ──────────────────────────────────────────────────────────────────

    public override void GameComponentTick()
    {
        if (pending.Count == 0) return;
        int now = Find.TickManager.TicksGame;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].FireTick > now) continue;
            Fire(pending[i]);
            pending.RemoveAt(i);
        }
    }

    private void Fire(PendingKidnapOutcome entry)
    {
        Pawn captive = FindPawnById(entry.PawnId);
        if (captive == null || captive.Destroyed) return;

        Faction faction = Find.FactionManager.AllFactions
            .FirstOrDefault(f => f.loadID == entry.NecronFactionId);

        Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;

        NecronKidnapOutcomeResolver.ApplyOutcome(entry.Outcome, captive, faction, map);
    }

    private static Pawn FindPawnById(int id)
    {
        foreach (Pawn p in Find.WorldPawns.AllPawnsAliveOrDead)
            if (p.thingIDNumber == id) return p;
        return null;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref pending, "pendingKidnapOutcomes", LookMode.Deep);
        pending ??= new List<PendingKidnapOutcome>();
    }
}

/// <summary>A single scheduled kidnap outcome, fully serializable.</summary>
public class PendingKidnapOutcome : IExposable
{
    public int                 FireTick;
    public int                 PawnId;
    public NecronKidnapOutcome Outcome;
    public int                 NecronFactionId;

    public void ExposeData()
    {
        Scribe_Values.Look(ref FireTick,        "fireTick");
        Scribe_Values.Look(ref PawnId,          "pawnId");
        Scribe_Values.Look(ref Outcome,         "outcome");
        Scribe_Values.Look(ref NecronFactionId, "necronFactionId");
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
static class HarmonyPatch_RegisterPendingKidnapOutcomes
{
    static void Postfix(Game __instance)
    {
        if (__instance.GetComponent<GameComponent_PendingKidnapOutcomes>() == null)
            __instance.components.Add(new GameComponent_PendingKidnapOutcomes(__instance));
    }
}
