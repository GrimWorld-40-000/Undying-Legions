using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>Helpers for splitting scarab raid behavior between sapper and breach lord duties.</summary>
internal static class ScarabRaidDutyUtility
{
    internal static bool IsHostileRaider(Pawn pawn) =>
        pawn != null && pawn.Faction != null && pawn.Faction != Faction.OfPlayer;

    internal static bool IsOnSapperDuty(Pawn pawn) =>
        pawn?.mindState?.duty?.def == DutyDefOf.Sapper;

    internal static bool IsOnBreachingDuty(Pawn pawn) =>
        pawn?.mindState?.duty?.def == DutyDefOf.Breaching;

    /// <summary>Player-built (or non-natural-rock) impassable structure — kamikaze sapper/breach target.</summary>
    internal static bool IsConstructedWall(Thing thing)
    {
        if (thing?.def == null || thing.Destroyed)
            return false;
        if (thing.def.passability != Traversability.Impassable)
            return false;
        if (thing.def.building == null)
            return false;
        if (thing.def.building.isNaturalRock)
            return false;
        return true;
    }

    internal static bool HasFriendlyInRadius(Pawn pawn, IntVec3 center, float radius)
    {
        if (pawn?.Map == null)
            return false;

        foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (other == pawn || other.Dead || !other.Spawned)
                continue;
            if (pawn.HostileTo(other))
                continue;
            if (other.Position.DistanceTo(center) <= radius)
                return true;
        }

        return false;
    }

    internal static bool IsScarab(Pawn pawn) =>
        pawn?.def?.defName == "GW40K_ScarabSwarm";

    /// <summary>
    /// True while the scarab has been harmed by any source within the last 30 seconds.
    /// Used by all work-mode job givers to yield to the Animal think tree during combat.
    /// </summary>
    // ── Leap issue-rate throttle ──────────────────────────────────────────────
    // Covers the gap between job-issued and ability-cooldown-started.
    // When a leap job cancels mid-warmup, Ability.Activate() never fires so the
    // 2000-tick ability cooldown never starts. The think tree re-evaluates
    // immediately and issues another leap job → loop.
    // This throttle blocks re-issue for warmup (12 ticks) + 1 second (60 ticks).
    private static readonly System.Collections.Generic.Dictionary<int, int> _leapIssuedTick = new();
    private const int LeapIssueCooldownTicks = 72; // ~0.5s warmup + 1s buffer

    internal static bool TryRecordLeapIssue(Pawn pawn)
    {
        int id  = pawn.thingIDNumber;
        int now = Find.TickManager.TicksGame;
        if (_leapIssuedTick.TryGetValue(id, out int last) && now - last < LeapIssueCooldownTicks)
            return false;
        _leapIssuedTick[id] = now;
        return true;
    }

    internal static bool IsInRecentCombat(Pawn pawn)
    {
        if (pawn?.mindState == null) return false;
        return Find.TickManager.TicksGame - pawn.mindState.lastHarmTick < 1800;
    }
}
