using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Sapper-duty scarabs tunnel toward <c>sapperDest</c>: mine rock and mineable blockers,
/// self-destruct only on constructed walls when allies are clear.
/// </summary>
public class JobGiver_ScarabSapper : ThinkNode_JobGiver
{
    private const float ReachDestDistSq = 100f; // 10 tiles — matches JobGiver_AISapper
    private const int CheckOverrideInterval = 500;
    private const float SelfDestructBlastRadius = 4.9f;

    private static AbilityDef SelfDestructDef =>
        DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabSelfDestruct");

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ScarabRaidDutyUtility.IsHostileRaider(pawn) || !ScarabRaidDutyUtility.IsOnSapperDuty(pawn))
            return null;

        IntVec3 dest = pawn.mindState.duty.focus.Cell;
        if (dest.IsValid
            && dest.DistanceToSquared(pawn.Position) < ReachDestDistSq
            && dest.GetRoom(pawn.Map) == pawn.GetRoom()
            && dest.WithinRegions(pawn.Position, pawn.Map, 9, TraverseMode.NoPassClosedDoors))
        {
            pawn.GetLord()?.Notify_ReachedDutyLocation(pawn);
            return null;
        }

        if (!dest.IsValid)
        {
            if (!(from x in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn)
                  where !x.ThreatDisabled(pawn)
                        && x.Thing.Faction == Faction.OfPlayer
                        && pawn.Position.DistanceToSquared(x.Thing.Position) <= 2500
                        && pawn.CanReach(x.Thing, PathEndMode.OnCell, Danger.Deadly,
                            canBashDoors: false, canBashFences: false,
                            TraverseMode.PassAllDestroyableThings)
                  select x).TryRandomElement(out var result))
            {
                return null;
            }

            dest = result.Thing.Position;
        }

        if (!pawn.CanReach(dest, PathEndMode.OnCell, Danger.Deadly,
                canBashDoors: false, canBashFences: false, TraverseMode.PassAllDestroyableThings))
        {
            return null;
        }

        using PawnPath path = pawn.Map.pathFinder.FindPathNow(
            pawn.Position, dest,
            TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));

        IntVec3 cellBefore;
        Thing blocker = path.FirstBlockingBuilding(out cellBefore, pawn);
        if (blocker != null)
            return JobForBlocker(pawn, blocker, cellBefore);

        return JobMaker.MakeJob(JobDefOf.Goto, dest, CheckOverrideInterval, checkOverrideOnExpiry: true);
    }

    private static Job JobForBlocker(Pawn pawn, Thing blocker, IntVec3 cellBefore)
    {
        if (blocker.def.mineable)
            return ReservedMineJob(pawn, blocker, cellBefore);

        if (ScarabRaidDutyUtility.IsConstructedWall(blocker))
            return TrySelfDestructJob(pawn, blocker) ?? WaitNearJob(pawn, cellBefore);

        Job digJob = DigUtility.PassBlockerJob(pawn, blocker, cellBefore,
            canMineMineables: true, canMineNonMineables: true);
        if (digJob?.def != JobDefOf.AttackMelee)
            return digJob;

        return WaitNearJob(pawn, cellBefore);
    }

    private static Job TrySelfDestructJob(Pawn pawn, Thing wall)
    {
        if (!ScarabRaidDutyUtility.IsConstructedWall(wall))
            return null;

        AbilityDef def = SelfDestructDef;
        Ability ability = def != null ? pawn.abilities?.GetAbility(def) : null;
        if (ability == null || !ability.CanCast)
            return null;

        if (ScarabRaidDutyUtility.HasFriendlyInRadius(pawn, pawn.Position, SelfDestructBlastRadius))
            return null;

        return ability.GetJob(wall, wall);
    }

    private static Job ReservedMineJob(Pawn pawn, Thing blocker, IntVec3 cellBefore)
    {
        if (!pawn.CanReserve(blocker))
            return WaitNearJob(pawn, cellBefore);

        Job job = JobMaker.MakeJob(JobDefOf.Mine, blocker);
        job.ignoreDesignations = true;
        job.expiryInterval = JobGiver_AIFightEnemy.ExpiryInterval_ShooterSucceeded.RandomInRange;
        job.checkOverrideOnExpire = true;
        return job;
    }

    private static Job WaitNearJob(Pawn pawn, IntVec3 cellBefore)
    {
        IntVec3 near = CellFinder.RandomClosewalkCellNear(cellBefore, pawn.Map, 10);
        if (near == pawn.Position)
            return JobMaker.MakeJob(JobDefOf.Wait, 20, checkOverrideOnExpiry: true);

        return JobMaker.MakeJob(JobDefOf.Goto, near, CheckOverrideInterval, checkOverrideOnExpiry: true);
    }
}
