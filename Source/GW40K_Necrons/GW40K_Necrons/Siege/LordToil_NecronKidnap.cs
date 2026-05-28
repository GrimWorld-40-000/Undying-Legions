using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Necrons capture organic pawns before withdrawing. Each Necron attempts to
/// find a downed or incapacitated non-Necron flesh pawn within 8 tiles.
///
/// Captive fates are resolved in <see cref="Cleanup"/> via
/// <see cref="NecronKidnapOutcomeResolver"/> — five outcomes are possible:
/// death, escaped safely, enslaved (early Vekh), full Vekh, or biotransferred.
/// </summary>
public class LordToil_NecronKidnap : LordToil
{
    private class Data : LordToilData
    {
        public List<int> captiveIds = new();
        public override void ExposeData() =>
            Scribe_Collections.Look(ref captiveIds, "kidnappedPawnIds", LookMode.Value);
    }

    private Data D => (Data)(data ??= new Data());

    public LordToil_NecronKidnap() { data = new Data(); }

    public override bool AllowSatisfyLongNeeds => false;
    public override bool AllowSelfTend         => false;

    // ── Think-tree duties ─────────────────────────────────────────────────────

    public override void UpdateAllDuties()
    {
        List<Thing> taken = null;
        foreach (Pawn p in lord.ownedPawns)
        {
            if (p.Dead || !p.Spawned) continue;

            // During withdrawal, Necrons grab targets regardless of combat danger —
            // they are already leaving and should seize victims even under fire.
            if (TryFindVictim(p, taken, out Pawn victim))
            {
                if (p.mindState.duty?.def != DutyDefOf.Kidnap)
                {
                    p.mindState.duty = new PawnDuty(DutyDefOf.Kidnap);
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                if (victim != null)
                {
                    (taken ??= new List<Thing>()).Add(victim);
                    TrackCaptive(victim);
                }
            }
            else
            {
                // No valid victim in range — begin exiting.
                if (p.mindState.duty?.def != DutyDefOf.ExitMapBest)
                    p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
            }
        }
    }

    public override void LordToilTick()
    {
        int tick = Find.TickManager.TicksGame;

        // Re-evaluate duties every 5 s so Necrons assigned ExitMapBest on the initial
        // UpdateAllDuties call (when all were flagged in dangerous combat) can retry
        // once the immediate threat clears and victims become available.
        if (tick % 300 == 0)
            UpdateAllDuties();

        // Poll every ~3 seconds to track pawns freshly picked up mid-phase.
        if (tick % 180 == 0)
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p.carryTracker?.CarriedThing is Pawn carried && IsValidTarget(carried))
                    TrackCaptive(carried);
            }
        }
    }

    // ── Cleanup / outcome resolution ──────────────────────────────────────────

    public override void Cleanup()
    {
        Map map = Map;
        Faction faction = lord.faction;

        // Resolve outcomes for captives still being carried on the map.
        foreach (Pawn p in lord.ownedPawns)
        {
            if (p.carryTracker?.CarriedThing is Pawn captive && IsValidTarget(captive))
            {
                D.captiveIds.Remove(captive.thingIDNumber);
                NecronKidnapOutcomeResolver.Resolve(captive, faction, map);
            }
        }

        // Any tracked captive that already left the map — fate unknown for now.
        // TODO: defer resolution via GameComponent_DeferredActions when they
        //       are confirmed off-map, using a per-captive timer.
        foreach (int id in D.captiveIds)
        {
            Pawn missing = FindWorldPawnById(id);
            if (missing != null && !missing.Dead && !missing.Destroyed)
                NecronKidnapOutcomeResolver.Resolve(missing, faction, map);
        }
        D.captiveIds?.Clear();

        base.Cleanup();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryFindVictim(Pawn kidnapper, List<Thing> taken, out Pawn victim)
    {
        // Already carrying a valid target — keep it.
        if (kidnapper.mindState.duty?.def == DutyDefOf.Kidnap
            && kidnapper.carryTracker.CarriedThing is Pawn carried
            && IsValidTarget(carried))
        {
            victim = carried;
            return true;
        }

        if (!KidnapAIUtility.TryFindGoodKidnapVictim(kidnapper, 20f, out victim, taken))
            return false;

        return victim != null && IsValidTarget(victim);
    }

    /// <summary>
    /// Necrons only take organic non-Necron pawns — the raw material for
    /// necrodermis colonization and Vekh conversion.
    /// </summary>
    private static bool IsValidTarget(Pawn p) =>
        p != null && p.RaceProps.IsFlesh && !NechEnergyUtility.IsNecronPawn(p);

    private static Pawn FindWorldPawnById(int id)
    {
        foreach (Pawn p in Find.WorldPawns.AllPawnsAliveOrDead)
            if (p.thingIDNumber == id) return p;
        return null;
    }

    private void TrackCaptive(Pawn captive)
    {
        if (!D.captiveIds.Contains(captive.thingIDNumber))
            D.captiveIds.Add(captive.thingIDNumber);
    }

}
