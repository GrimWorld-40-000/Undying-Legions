using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Siege-assault scarabs: when adjacent to a wall blocking a high-value interior target
/// (generator, battery, turret, mortar ammo, or bedroom), fires GW40K_ScarabLeap to
/// land on the far side and detonate next to the objective.
///
/// This node lives inside the GW_UL_ScarabAssault DutyDef think tree, so it only
/// evaluates when a scarab has been assigned that duty by LordToil_NecronSiegeBombard.
/// </summary>
public class JobGiver_ScarabSiegeLeapToObjective : ThinkNode_JobGiver
{
    private const float LeapRange    = 8.9f;
    private const float ScanStep     = 0.5f;
    private const float SearchRadius = 60f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ScarabRaidDutyUtility.IsHostileRaider(pawn)) return null;

        AbilityDef leapDef = DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabLeap");
        if (leapDef == null) return null;

        Ability leap = pawn.abilities?.GetAbility(leapDef);
        if (leap == null || !leap.CanCast) return null;

        Thing target = FindBestObjective(pawn);
        if (target == null) return null;

        IntVec3 landing = FindLandingOverWall(pawn, target.Position);
        if (!landing.IsValid || !landing.InBounds(pawn.Map)) return null;

        // Issue-rate throttle: prevents re-issue when a leap job cancels mid-warmup
        // before Ability.Activate fires (no cooldown applied on cancel → instant loop).
        if (!ScarabRaidDutyUtility.TryRecordLeapIssue(pawn))
            return null;

        return leap.GetJob(landing, landing);
    }

    private static Thing FindBestObjective(Pawn pawn)
    {
        Map map   = pawn.Map;
        Thing best = null;
        float bestScore = 0f;

        foreach (Building b in map.listerBuildings.allBuildingsColonist)
        {
            if (b.Destroyed || !b.Spawned) continue;

            float priority = BuildingPriority(b);
            if (priority <= 0f) continue;

            float dist = pawn.Position.DistanceTo(b.Position);
            if (dist > SearchRadius) continue;

            float score = priority / (dist + 1f);
            if (score > bestScore) { bestScore = score; best = b; }
        }

        // Also check mortar shell stacks (items, not buildings).
        foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
        {
            if (t.Destroyed || !t.Spawned) continue;
            if (!IsMortarAmmo(t)) continue;

            float dist = pawn.Position.DistanceTo(t.Position);
            if (dist > SearchRadius) continue;

            float score = 3f / (dist + 1f);
            if (score > bestScore) { bestScore = score; best = t; }
        }

        return best;
    }

    private static float BuildingPriority(Building b)
    {
        // Power generators — positive-output CompPowerTrader (solar, wind, geo, etc.)
        CompPowerTrader power = b.TryGetComp<CompPowerTrader>();
        if (power != null && power.PowerOutput > 0f) return 5f;

        // Power batteries
        if (b.TryGetComp<CompPowerBattery>() != null) return 4f;

        // Turrets
        if (b is Building_TurretGun) return 4f;

        // Sleeping colonists are a high-value soft target
        if (b is Building_Bed bed)
        {
            Room room = bed.GetRoom();
            if (room?.Role == RoomRoleDefOf.Bedroom || room?.Role == RoomRoleDefOf.PrisonCell)
                return 2f;
        }

        return 0f;
    }

    private static bool IsMortarAmmo(Thing t) =>
        t.def?.thingCategories != null &&
        t.def.thingCategories.Any(c => c.defName == "MortarShells");

    private static IntVec3 FindLandingOverWall(Pawn pawn, IntVec3 targetPos)
    {
        Map     map  = pawn.Map;
        Vector3 from = pawn.Position.ToVector3Shifted();
        Vector3 dir  = (targetPos.ToVector3Shifted() - from).normalized;

        bool passedWall = false;
        for (float dist = 1f; dist <= LeapRange; dist += ScanStep)
        {
            IntVec3 cell = (from + dir * dist).ToIntVec3();
            if (!cell.InBounds(map) || cell == pawn.Position) continue;

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
