using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Ends player "take control of Nech" work when the target Nech goes rogue or hostile.</summary>
public static class NechTakeControlJobUtility
{
    /// <summary>
    /// Take command may only attach to lawful friendly targets — same faction as commander and not hostile
    /// to them (e.g. rogue same-faction nechs remain blocked).
    /// </summary>
    public static bool IsFriendlyTakeControlTarget(Pawn commander, Pawn target)
    {
        if (commander == null || target == null)
            return false;
        if (commander.Faction == null || target.Faction == null)
            return false;
        if (target.Faction != commander.Faction)
            return false;
        if (target.HostileTo(commander))
            return false;
        return true;
    }

    public static bool TakeControlTargetNoLongerValid(Pawn targetNech, Pawn controller)
    {
        if (targetNech == null || controller == null)
            return true;
        if (targetNech.HostileTo(controller))
            return true;
        MentalStateDef rogueDef = NecronDefOfs.GW40K_NechRogue;
        if (targetNech.InMentalState)
        {
            MentalStateDef cur = targetNech.MentalStateDef;
            if (rogueDef != null && cur == rogueDef)
                return true;
            if (rogueDef == null && cur == MentalStateDefOf.Berserk)
                return true;
        }

        return false;
    }

    public static void CancelTakeControlJobsTargeting(Pawn targetNech)
    {
        if (targetNech == null)
            return;
        JobDef takeControl = NecronDefOfs.GW40K_TakeControlOfNech;
        if (takeControl == null)
            return;

        foreach (Map map in Find.Maps)
        {
            if (map == null)
                continue;
            foreach (Pawn worker in map.mapPawns.AllPawnsSpawned)
                CancelTakeControlOnPawn(worker, takeControl, targetNech);
        }
    }

    private static void CancelTakeControlOnPawn(Pawn worker, JobDef takeControl, Pawn targetNech)
    {
        if (worker?.jobs == null)
            return;

        Job cur = worker.jobs.curJob;
        if (cur != null && cur.def == takeControl && cur.targetA.Pawn == targetNech)
            worker.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);

        JobQueue queue = worker.jobs.jobQueue;
        if (queue == null || queue.Count == 0)
            return;

        queue.RemoveAll(worker, j =>
            j != null && j.def == takeControl && j.targetA.Pawn == targetNech);
    }
}
