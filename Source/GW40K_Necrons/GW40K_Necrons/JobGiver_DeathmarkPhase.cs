using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

// Enemy-only AI: fires the Phase ability when the Deathmark has recently taken
// damage and a better-covered cell is reachable within the ability's range.
public class JobGiver_DeathmarkPhase : ThinkNode_JobGiver
{
    // How recently the pawn must have been harmed (ticks) to consider phasing.
    private const int recentHarmWindow = 300; // ~5 seconds

    // Minimum cover improvement (0-1) required to bother phasing.
    private const float minCoverImprovement = 0.15f;

    // How many candidate cells to sample when searching for cover.
    private const int sampleCount = 25;

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Only fire for non-player combatants.
        if (pawn.Faction == null || pawn.Faction.IsPlayer) return null;
        if (pawn.Downed || pawn.Dead) return null;

        // Pawn must have the Phase ability and it must be off cooldown.
        Ability phase = pawn.abilities?.GetAbility(NecronDefOfs.GW40K_Deathmark_Phase);
        if (phase == null || !phase.CanCast) return null;

        // Only react to recent damage.
        if (Find.TickManager.TicksGame - pawn.mindState.lastHarmTick > recentHarmWindow)
            return null;

        // Need a visible threat to take cover from.
        Thing threat = pawn.mindState.enemyTarget as Thing;
        if (threat == null) return null;

        float currentCover = CoverUtility.CalculateOverallBlockChance(
            pawn.Position, threat.Position, pawn.Map);

        float range = phase.def.verbProperties.range;
        IntVec3 dest = FindBetterCoverCell(pawn, threat, currentCover, range);
        if (!dest.IsValid) return null;

        return phase.GetJob(dest, LocalTargetInfo.Invalid);
    }

    private static IntVec3 FindBetterCoverCell(Pawn pawn, Thing threat, float currentCover, float range)
    {
        IntVec3 best = IntVec3.Invalid;
        float bestCover = currentCover + minCoverImprovement;
        int iRange = (int)range;

        for (int i = 0; i < sampleCount; i++)
        {
            if (!CellFinder.TryFindRandomCellNear(
                pawn.Position, pawn.Map, iRange,
                c => c.Standable(pawn.Map)
                  && !c.IsForbidden(pawn)
                  && c.DistanceTo(pawn.Position) >= 3f
                  && c.DistanceTo(pawn.Position) <= range,
                out IntVec3 candidate))
                continue;

            float cover = CoverUtility.CalculateOverallBlockChance(
                candidate, threat.Position, pawn.Map);
            if (cover > bestCover)
            {
                bestCover = cover;
                best = candidate;
            }
        }

        return best;
    }
}
