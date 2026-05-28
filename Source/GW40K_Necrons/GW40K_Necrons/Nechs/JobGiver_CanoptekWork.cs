using System.Collections.Generic;
using System.Linq;
using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Work mode for Canoptek constructs: haul, mine, and construct when set to ControlNodeMode.Work.
/// Delegates to vanilla WorkGiver_Scanner implementations — no pawn work-settings required.
/// </summary>
public class JobGiver_CanoptekWork : ThinkNode_JobGiver
{
    private static List<WorkGiver_Scanner> _workGivers;

    private static List<WorkGiver_Scanner> WorkGivers
    {
        get
        {
            if (_workGivers != null)
                return _workGivers;

            _workGivers = DefDatabase<WorkGiverDef>.AllDefsListForReading
                .Where(d => d.workType != null &&
                            (d.workType == WorkTypeDefOf.Hauling      ||
                             d.workType == WorkTypeDefOf.Construction  ||
                             d.workType == WorkTypeDefOf.Mining))
                .OrderByDescending(d => d.priorityInType)
                .Select(d => d.Worker)
                .OfType<WorkGiver_Scanner>()
                .ToList();

            return _workGivers;
        }
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null || pawn.Drafted)
            return null;
        if (!NecrodermisIngestionUtility.IsCanoptek(pawn))
            return null;

        // Yield to the Animal think tree while under attack.
        if (ScarabRaidDutyUtility.IsInRecentCombat(pawn)) return null;

        GameComponent_CanoptekConstructModes store = GameComponent_CanoptekConstructModes.Current;
        if (store?.GetMode(pawn) != ControlNodeMode.Work)
            return null;

        foreach (WorkGiver_Scanner scanner in WorkGivers)
        {
            if (scanner.MissingRequiredCapacity(pawn) != null)
                continue;

            Job job = TryGetJobFrom(pawn, scanner);
            if (job != null)
                return job;
        }

        return null;
    }

    private static Job TryGetJobFrom(Pawn pawn, WorkGiver_Scanner scanner)
    {
        try
        {
            if (scanner.def.scanThings)
            {
                ThingRequest req = scanner.PotentialWorkThingRequest;
                if (req.IsUndefined)
                    return null;

                Thing t = GenClosest.ClosestThingReachable(
                    pawn.Position, pawn.Map, req,
                    scanner.PathEndMode,
                    TraverseParms.For(pawn),
                    9999f,
                    x => !x.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, x, false));

                return t != null ? scanner.JobOnThing(pawn, t, false) : null;
            }
            else
            {
                // Cell-based scanner (e.g. mining) — use PotentialWorkCellsGlobal
                IEnumerable<IntVec3> cells = scanner.PotentialWorkCellsGlobal(pawn);
                if (cells == null)
                    return null;

                foreach (IntVec3 c in cells)
                {
                    if (scanner.HasJobOnCell(pawn, c, false))
                        return scanner.JobOnCell(pawn, c, false);
                }

                return null;
            }
        }
        catch
        {
            return null;
        }
    }
}
