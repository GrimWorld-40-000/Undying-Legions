using RimWorld;
using Verse;
using Verse.AI;

namespace NecronGeneUtil;

public class JobGiver_GetNecrodermis : ThinkNode_JobGiver
{
    private static JobDef _useItemJob;

    private static JobDef UseItemJobDef
    {
        get
        {
            if (_useItemJob == null)
                _useItemJob = DefDatabase<JobDef>.GetNamedSilentFail("UseItem");
            return _useItemJob;
        }
    }

    public override float GetPriority(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive)
            return 0f;
        if (NecrodermisIngestionUtility.IsCanoptek(pawn))
            return 0f;
        var need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null)
            return 0f;
        if (!need.Starving && FoodUtility.ShouldBeFedBySomeone(pawn))
            return 0f;
        if (need.CurLevelPercentage >= pawn.RaceProps.FoodLevelPercentageWantEat)
            return 0f;
        return 9.5f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive)
            return null;
        if (NecrodermisIngestionUtility.IsCanoptek(pawn))
            return null;
        var need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevelPercentage >= pawn.RaceProps.FoodLevelPercentageWantEat)
            return null;
        Thing pack = GetNecrodermisPack(pawn);
        if (pack == null)
            return null;
        // Raw packs use CompUsable; same driver as serums. JobDefOf may omit UseItem on some RW versions.
        JobDef useItem = UseItemJobDef;
        if (useItem == null)
            return null;
        return JobMaker.MakeJob(useItem, pack);
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
