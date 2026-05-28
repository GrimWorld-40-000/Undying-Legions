using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Fires at the top of <see cref="GW_UL_SpyderThinkTree"/> for hostile non-siege Spyders.
///
/// Overrides <c>pawn.mindState.duty</c> to <c>GW_UL_SpyderRaidStandoff</c> pointed at a
/// position ~17 tiles from the colony (inside particle-beamer max-range 23, well clear of
/// the 5-tile min-range / <c>ai_AvoidFriendlyFireRadius</c> zone). Always returns <c>null</c>
/// so the Mechanoid subtree that follows issues the actual job using the duty we just set.
///
/// During Necron siege raids the lord already manages standoff:
/// <see cref="LordToil_NecronSiegeBombard"/> holds the Spyder in siege mode;
/// <see cref="LordToil_NecronSiegeAssault.AssignNecronAssaultDuties"/> assigns this same
/// duty directly, so this giver is skipped for <see cref="LordJob_NecronSiege"/> pawns.
/// </summary>
public class JobGiver_SpyderRaidStandoff : ThinkNode_JobGiver
{
    /// Desired distance from colony center.
    /// Comfortable mid-range for the particle beamer (max 23, min 5).
    public const float StandoffDist = 17f;

    private static DutyDef _standoffDuty;
    private static DutyDef StandoffDuty =>
        _standoffDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_SpyderRaidStandoff");

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Skip player-faction Spyders and non-hostile pawns.
        if (pawn.Faction == null || !pawn.Faction.HostileTo(Faction.OfPlayer)) return null;
        if (!pawn.Spawned || pawn.Dead || pawn.Map == null) return null;

        // Necron siege lord manages standoff itself via LordToil_NecronSiegeAssault.
        if (pawn.GetLord()?.LordJob is LordJob_NecronSiege) return null;

        // Scope to pawns that have the particle beamer verb (the Spyder).
        if (!HasParticleBeamer(pawn)) return null;

        DutyDef dutyDef = StandoffDuty;
        if (dutyDef == null) return null;

        IntVec3 colonyCenter = RaidStrategyWorker_NecronSiege.FindColonyCenter(pawn.Map);
        if (!colonyCenter.IsValid) return null;

        // Standoff position: ~17 tiles from colony, on the line from colony toward the Spyder.
        // Stable once at standoff — direction from colony to Spyder doesn't drift there.
        IntVec3 standoff = CalcStandoffPos(pawn.Map, colonyCenter, pawn.Position, StandoffDist);
        if (!standoff.IsValid) return null;

        // Override duty every think cycle so the lord's UpdateAllDuties (which may fire
        // AssaultColony with a march-in focus) can never permanently override us.
        pawn.mindState.duty = new PawnDuty(dutyDef, standoff);

        return null; // real job comes from Mechanoid subtree → LordDuty → JobGiver_AIDefendPoint
    }

    // ── Helpers shared with LordToil_NecronSiegeAssault ─────────────────────────

    /// <summary>True if <paramref name="pawn"/> has a <see cref="Verb_SpyderParticleBeamer"/>.
    /// Used to identify the Spyder without hard-coding a def name.</summary>
    internal static bool HasParticleBeamer(Pawn pawn) =>
        pawn.verbTracker?.AllVerbs?.Find(v => v is Verb_SpyderParticleBeamer) != null;

    /// <summary>
    /// Returns a walkable cell approximately <paramref name="dist"/> tiles from
    /// <paramref name="colony"/>, on the line from <paramref name="colony"/> toward
    /// <paramref name="approachFrom"/>. Falls back to the nearest standable cell
    /// within 8 tiles if the exact position is blocked.
    /// </summary>
    internal static IntVec3 CalcStandoffPos(Map map, IntVec3 colony, IntVec3 approachFrom, float dist)
    {
        Vector3 dir = (approachFrom - colony).ToVector3();
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector3.right;
        else
            dir.Normalize();

        IntVec3 candidate = (colony.ToVector3Shifted() + dir * dist).ToIntVec3();

        // Keep inside map with a 2-cell margin.
        candidate.x = Mathf.Clamp(candidate.x, 2, map.Size.x - 3);
        candidate.z = Mathf.Clamp(candidate.z, 2, map.Size.z - 3);

        if (candidate.Standable(map))
            return candidate;

        // Widen the search if the exact cell is blocked (rocks, walls, etc).
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(candidate, 8, false))
        {
            if (cell.InBounds(map) && cell.Standable(map))
                return cell;
        }

        return IntVec3.Invalid;
    }
}
