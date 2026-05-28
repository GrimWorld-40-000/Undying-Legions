using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// During breach duty, traces a line toward the enemy. If that line crosses an impassable wall
/// and a walkable cell exists on the far side within leap range, uses GW40K_ScarabLeap.
/// </summary>
public class JobGiver_ScarabLeapOverWall : ThinkNode_JobGiver
{
    private const float LeapRange = 8.9f;
    private const float ScanStep = 0.5f;

    // Only leap when the enemy is already close — this prevents the scarab from leaping
    // the instant it gets Breaching duty (e.g. from LordJob_AssaultColony), landing far
    // from any actual target and standing idle. LeapRange * 2 means the leap genuinely
    // bridges the remaining gap to the target.
    private const float MaxEnemyDistToLeap = LeapRange * 2f; // ~18 cells

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ScarabRaidDutyUtility.IsHostileRaider(pawn) || !ScarabRaidDutyUtility.IsOnBreachingDuty(pawn))
            return null;

        Thing enemy = pawn.mindState?.enemyTarget as Thing;
        if (enemy == null)
            return null;

        // Don't leap if the enemy is too far away — walk closer first.
        if (pawn.Position.DistanceTo(enemy.Position) > MaxEnemyDistToLeap)
            return null;

        Ability leap = pawn.abilities?.GetAbility(
            DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabLeap"));
        if (leap == null || !leap.CanCast)
            return null;

        IntVec3 landing = FindLandingOverWall(pawn, enemy.Position);
        if (!landing.IsValid || !landing.InBounds(pawn.Map))
            return null;

        // Issue-rate throttle: if leap job was just issued (e.g. cancelled mid-warmup
        // before Ability.Activate fires), wait warmup + 1 second before retrying.
        if (!ScarabRaidDutyUtility.TryRecordLeapIssue(pawn))
            return null;

        return leap.GetJob(landing, landing);
    }

    private IntVec3 FindLandingOverWall(Pawn pawn, IntVec3 targetPos)
    {
        Map map = pawn.Map;
        Vector3 from = pawn.Position.ToVector3Shifted();
        Vector3 dir = (targetPos.ToVector3Shifted() - from).normalized;

        bool passedWall = false;
        for (float dist = 1f; dist <= LeapRange; dist += ScanStep)
        {
            IntVec3 cell = (from + dir * dist).ToIntVec3();
            if (!cell.InBounds(map) || cell == pawn.Position)
                continue;

            Building edifice = cell.GetEdifice(map);
            bool isWall = edifice != null
                && edifice.def.passability == Traversability.Impassable
                && !edifice.Destroyed;

            if (isWall)
                passedWall = true;
            else if (passedWall && cell.Walkable(map))
                return cell;
        }

        return IntVec3.Invalid;
    }
}

/// <summary>
/// Breach-duty scarabs: adjacent constructed wall + clear blast radius → self-destruct.
/// </summary>
public class JobGiver_ScarabBreachDetonate : ThinkNode_JobGiver
{
    private static AbilityDef SelfDestructDef =>
        DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabSelfDestruct");

    private const float BlastRadius = 4.9f;
    private const float DetonateChance = 0.35f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ScarabRaidDutyUtility.IsHostileRaider(pawn) || !ScarabRaidDutyUtility.IsOnBreachingDuty(pawn))
            return null;

        if (pawn.mindState?.enemyTarget == null)
            return null;

        AbilityDef def = SelfDestructDef;
        if (def == null)
            return null;

        Ability ability = pawn.abilities?.GetAbility(def);
        if (ability == null || !ability.CanCast)
            return null;

        Building breachTarget = FindAdjacentWallTarget(pawn);
        if (breachTarget == null)
            return null;

        if (!Rand.Chance(DetonateChance))
            return null;

        if (ScarabRaidDutyUtility.HasFriendlyInRadius(pawn, pawn.Position, BlastRadius))
            return null;

        return ability.GetJob(breachTarget, breachTarget);
    }

    private static Building FindAdjacentWallTarget(Pawn pawn)
    {
        foreach (IntVec3 dir in GenAdj.CardinalDirections)
        {
            IntVec3 cell = pawn.Position + dir;
            if (!cell.InBounds(pawn.Map))
                continue;

            Building b = cell.GetEdifice(pawn.Map);
            if (b != null && !b.Destroyed && ScarabRaidDutyUtility.IsConstructedWall(b))
                return b;
        }

        return null;
    }
}
