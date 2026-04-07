using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class ThoughtWorker_CoreFluxCollapse : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (p.needs == null || NecronDefOfs.GW40K_CoreFlux == null)
            return ThoughtState.Inactive;
        if (!p.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux, out Need need) || need is not MaintenanceNeed mNeed)
            return ThoughtState.Inactive;
        return mNeed.CurLevel <= 0f ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
    }
}
