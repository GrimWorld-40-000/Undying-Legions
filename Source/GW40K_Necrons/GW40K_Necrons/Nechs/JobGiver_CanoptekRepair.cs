using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Repair-mode behavior for Canoptek constructs:
/// priority is self, then friendly Necrons, then friendly vanilla mechs, then claimed structures.
/// </summary>
public class JobGiver_CanoptekRepair : ThinkNode_JobGiver
{
    private const float AllyRepairSearchRadius = 56f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null || pawn.Drafted)
            return null;
        if (!NecrodermisIngestionUtility.IsCanoptek(pawn))
            return null;

        if (pawn.GetLord() != null && pawn.Faction != Faction.OfPlayer)
            return null;

        // Yield to the Animal think tree while under attack.
        if (ScarabRaidDutyUtility.IsInRecentCombat(pawn)) return null;

        GameComponent_CanoptekConstructModes store = GameComponent_CanoptekConstructModes.Current;
        ControlNodeMode mode = store?.GetMode(pawn) ?? ControlNodeMode.Consume;
        if (mode != ControlNodeMode.Repair)
            return null;

        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel <= 0.001f)
            return null;

        if (ThingComp_CanoptekRepairPolicy.AllowSelf(pawn)
            && JobDriver_CanoptekRepair.TechnicianMeetsNecroThresholdForPatient(pawn, pawn)
            && (JobDriver_CanoptekRepair.HasMissingScarabPart(pawn)
                || JobDriver_CanoptekRepair.HasInjuredScarabUnitParts(pawn)))
            return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekRepair, pawn);

        if (ThingComp_CanoptekRepairPolicy.AllowFriendlyNecrons(pawn))
        {
            Pawn friendlyNecron = FindFriendlyNecronTarget(pawn);
            if (friendlyNecron != null)
                return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekRepair, friendlyNecron);
        }

        if (ThingComp_CanoptekRepairPolicy.AllowFriendlyMechs(pawn))
        {
            Pawn friendlyMech = FindFriendlyVanillaMechTarget(pawn);
            if (friendlyMech != null)
                return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekRepair, friendlyMech);
        }

        if (ThingComp_CanoptekRepairPolicy.AllowNecronStructures(pawn))
        {
            Thing necronStructure = FindStructureTarget(pawn, necronOnly: true);
            if (necronStructure != null)
                return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekRepair, necronStructure);
        }

        if (ThingComp_CanoptekRepairPolicy.AllowStructures(pawn))
        {
            Thing structure = FindStructureTarget(pawn, necronOnly: false);
            if (structure != null)
                return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekRepair, structure);
        }

        return null;
    }

    private static Pawn FindFriendlyNecronTarget(Pawn pawn)
    {
        if (pawn?.Faction == null || pawn.Map?.mapPawns?.AllPawnsSpawned == null)
            return null;

        float maxSq = AllyRepairSearchRadius * AllyRepairSearchRadius;
        Pawn best = null;
        float bestD = float.MaxValue;

        foreach (Pawn cand in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (cand == null || cand == pawn || cand.Destroyed)
                continue;
            if (!cand.Spawned || cand.MapHeld != pawn.MapHeld)
                continue;
            if (cand.Faction != pawn.Faction)
                continue;
            if (!JobDriver_CanoptekRepair.NeedsFriendlyNecronAllyRepair(cand))
                continue;
            if (!JobDriver_CanoptekRepair.TechnicianMeetsNecroThresholdForPatient(pawn, cand))
                continue;

            float dSq = pawn.Position.DistanceToSquared(cand.Position);
            if (dSq > maxSq)
                continue;
            if (!pawn.CanReserveAndReach(cand, PathEndMode.Touch, Danger.Deadly))
                continue;

            if (dSq < bestD)
            {
                bestD = dSq;
                best = cand;
            }
        }

        return best;
    }

    private static Pawn FindFriendlyVanillaMechTarget(Pawn pawn)
    {
        if (pawn?.Faction == null || pawn.Map?.mapPawns?.AllPawnsSpawned == null)
            return null;

        float maxSq = AllyRepairSearchRadius * AllyRepairSearchRadius;
        Pawn best = null;
        float bestD = float.MaxValue;

        foreach (Pawn cand in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (cand == null || cand == pawn || cand.Destroyed)
                continue;
            if (!cand.Spawned || cand.MapHeld != pawn.MapHeld)
                continue;
            if (cand.Faction != pawn.Faction)
                continue;
            if (!JobDriver_CanoptekRepair.NeedsFriendlyVanillaMechRepair(cand))
                continue;
            if (!JobDriver_CanoptekRepair.TechnicianMeetsNecroThresholdForPatient(pawn, cand))
                continue;

            float dSq = pawn.Position.DistanceToSquared(cand.Position);
            if (dSq > maxSq)
                continue;
            if (!pawn.CanReserveAndReach(cand, PathEndMode.Touch, Danger.Deadly))
                continue;

            if (dSq < bestD)
            {
                bestD = dSq;
                best = cand;
            }
        }

        return best;
    }

    /// <param name="necronOnly">When true, only GW40K_/GW_UD_ building defs; when false, any other artificial building.</param>
    private static Thing FindStructureTarget(Pawn pawn, bool necronOnly)
    {
        return GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
            PathEndMode.Touch,
            TraverseParms.For(pawn),
            9999f,
            t => IsValidStructureTarget(pawn, t, necronOnly));
    }

    private static bool IsValidStructureTarget(Pawn pawn, Thing t, bool necronOnly)
    {
        if (t == null || !t.Spawned || t.Map != pawn.Map || !t.def.useHitPoints)
            return false;
        if (t is not Building)
            return false;
        if (t.HitPoints >= t.MaxHitPoints)
            return false;
        // Claimed / owned only — same faction as the repairer (excludes neutral ruins and unclaimed wrecks).
        if (pawn.Faction == null || t.Faction != pawn.Faction)
            return false;
        if (t.IsForbidden(pawn))
            return false;
        if (!pawn.CanReserve(t))
            return false;

        bool isNecronBuilding = JobDriver_CanoptekRepair.IsNecronStructureBuilding(t);
        if (necronOnly)
            return isNecronBuilding;
        return !isNecronBuilding;
    }

    /// <summary>
    /// Quick check used by auto-mode to determine whether any repair work exists before
    /// deciding to stay in Repair mode or fall through to Work mode.
    /// </summary>
    public static bool HasAnyRepairTarget(Pawn pawn)
    {
        if (pawn?.Map == null)
            return false;

        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel <= 0.001f)
            return false;

        // Self checks
        if (ThingComp_CanoptekRepairPolicy.AllowSelf(pawn)
            && JobDriver_CanoptekRepair.TechnicianMeetsNecroThresholdForPatient(pawn, pawn)
            && (JobDriver_CanoptekRepair.HasMissingScarabPart(pawn)
                || JobDriver_CanoptekRepair.HasInjuredScarabUnitParts(pawn)))
            return true;

        // Friendly Necrons (Find* only returns targets the technician can afford to repair now).
        if (ThingComp_CanoptekRepairPolicy.AllowFriendlyNecrons(pawn) && FindFriendlyNecronTarget(pawn) != null)
            return true;

        if (ThingComp_CanoptekRepairPolicy.AllowFriendlyMechs(pawn) && FindFriendlyVanillaMechTarget(pawn) != null)
            return true;

        // Structures
        if (ThingComp_CanoptekRepairPolicy.AllowNecronStructures(pawn)
            && FindStructureTarget(pawn, necronOnly: true) != null)
            return true;

        if (ThingComp_CanoptekRepairPolicy.AllowStructures(pawn)
            && FindStructureTarget(pawn, necronOnly: false) != null)
            return true;

        return false;
    }
}
