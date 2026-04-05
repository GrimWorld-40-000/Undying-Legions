using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

#nullable disable
namespace GW40K_Necrons;

public class JobGiver_GetMaintenance : ThinkNode_JobGiver
{
    public override ThinkNode DeepCopy(bool resolve = true)
    {
        return (ThinkNode)base.DeepCopy(resolve);
    }

    public override float GetPriority(Pawn pawn)
    {
        MaintenanceNeed need = (MaintenanceNeed)pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux);
        if (need == null) return 0f;
        if (Find.TickManager.TicksGame < pawn.mindState.canSleepTick) return 0f;
        Lord lord = pawn.GetLord();
        if (lord != null && !lord.CurLordToil.AllowSatisfyLongNeeds) return 0f;

        if (need.CurCategory <= RestCategory.Exhausted) return 9f;
        if (need.CurCategory <= RestCategory.VeryTired) return 8f;
        if (need.CurCategory <= RestCategory.Tired) return 7f;
        return 0f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        MaintenanceNeed need = (MaintenanceNeed)pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux);
        if (need == null || need.CurLevel > 0.20f) return null;

        Lord lord = pawn.GetLord();
        if (lord != null) return null;
        if (pawn.IsWildMan()) return null;
        if (pawn.InMentalState) return null;
        if (pawn.roping.IsRoped) return null;

        NecronCasket casket = NecroCasketUtility.FindCasketFor(pawn);
        if (casket == null) return null;

        return JobMaker.MakeJob(JobDefOf.EnterCryptosleepCasket, (LocalTargetInfo)(Thing)casket);
    }
}
