using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NecronGeneUtil;

public class JobGiver_GetNecrodermis : ThinkNode_JobGiver
{
    public override float GetPriority(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive) return 0f;
        if (pawn.needs.TryGetNeed<Need_Necrodermis>() == null) return 0f;
        return 9.1f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive) return null;
        var need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevelPercentage >= 0.25f) return null;
        int num = Mathf.CeilToInt(need.MaxLevel - need.CurLevel);
        if (num <= 0) return null;
        Thing pack = GetNecrodermisPack(pawn);
        if (pack == null) return null;
        Job job = JobMaker.MakeJob(JobDefOf.Ingest, pack);
        job.count = Mathf.Min(pack.stackCount, num);
        job.ingestTotalCount = true;
        return job;
    }

    private Thing GetNecrodermisPack(Pawn pawn)
    {
        Thing carried = pawn.carryTracker.CarriedThing;
        if (carried != null && carried.def == FMJ_DefOf.GW40k_Necron_Necrodermis) return carried;
        for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            if (pawn.inventory.innerContainer[i].def == FMJ_DefOf.GW40k_Necron_Necrodermis)
                return pawn.inventory.innerContainer[i];
        return GenClosest.ClosestThing_Global_Reachable(
            pawn.Position, pawn.Map,
            pawn.Map.listerThings.ThingsOfDef(FMJ_DefOf.GW40k_Necron_Necrodermis),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn, Danger.Deadly),
            9999f,
            t => pawn.CanReserve(t) && !t.IsForbidden(pawn));
    }
}
