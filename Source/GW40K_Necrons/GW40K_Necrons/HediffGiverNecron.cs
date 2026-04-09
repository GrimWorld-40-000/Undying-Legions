using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffGiverNecron : HediffGiver
{
    private const float FluxThresholdStart = 0.5f;

    public override void OnIntervalPassed(Pawn pawn, Hediff cause)
    {
        if (pawn.Dead)
            return;
        if (pawn.needs == null)
            return;
        if (!pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux, out Need needRaw) || needRaw is not MaintenanceNeed need)
            return;

        if (need.CoreFluxReplenishing() || need.CurLevel <= 0f)
        {
            RemoveStrain(pawn);
            return;
        }

        if (need.CurLevel >= FluxThresholdStart)
        {
            RemoveStrain(pawn);
            return;
        }

        float severity = (FluxThresholdStart - need.CurLevel) / FluxThresholdStart;
        if (severity < 0f) severity = 0f;
        if (severity > 1f) severity = 1f;

        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (hediff == null)
        {
            hediff = HediffMaker.MakeHediff(NecronDefOfs.GW40K_Necron_TiredHediff, pawn);
            pawn.health.AddHediff(hediff);
        }
        hediff.Severity = severity;
    }

    private static void RemoveStrain(Pawn pawn)
    {
        Hediff h = pawn.health?.hediffSet?.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (h != null)
            pawn.health.RemoveHediff(h);
    }
}
