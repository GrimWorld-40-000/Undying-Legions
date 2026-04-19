using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffGiverNecron : HediffGiver
{
    private const float FluxThresholdStart = 0.48f;
    private const float FluxThresholdClear = 0.55f;
    private const float PassiveRepairPointsPerDay = 12f;
    private const int PassiveRepairIntervalTicks = 600;

    public override void OnIntervalPassed(Pawn pawn, Hediff cause)
    {
        if (pawn.Dead)
            return;
        if (pawn.needs == null)
            return;
        if (!pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux, out Need needRaw) || needRaw is not MaintenanceNeed need)
            return;

        bool replenishing = need.CoreFluxReplenishing();
        if (replenishing || need.CurLevel <= 0f)
        {
            RemoveStrain(pawn);
            return;
        }

        Hediff strain = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (strain != null && need.CurLevel >= FluxThresholdClear)
        {
            RemoveStrain(pawn);
            strain = null;
        }

        if (strain == null && need.CurLevel < FluxThresholdStart)
        {
            strain = HediffMaker.MakeHediff(NecronDefOfs.GW40K_Necron_TiredHediff, pawn);
            pawn.health.AddHediff(strain);
        }

        if (strain != null)
        {
            float severity = (FluxThresholdStart - need.CurLevel) / FluxThresholdStart;
            if (severity < 0f) severity = 0f;
            if (severity > 1f) severity = 1f;
            strain.Severity = severity;
        }

        if (need.CurLevel > 0f && pawn.IsHashIntervalTick(PassiveRepairIntervalTicks) && HasRepairableInjuries(pawn))
        {
            float points = PassiveRepairPointsPerDay * (PassiveRepairIntervalTicks / (float)GenDate.TicksPerDay);
            NecronStasisHealing.ApplyHealPulse(pawn, points);
        }
    }

    private static void RemoveStrain(Pawn pawn)
    {
        Hediff h = pawn.health?.hediffSet?.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (h != null)
            pawn.health.RemoveHediff(h);
    }

    private static bool HasRepairableInjuries(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return false;

        for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
        {
            if (pawn.health.hediffSet.hediffs[i] is Hediff_Injury inj && !inj.IsPermanent() && inj.Severity > 0.001f)
                return true;
        }
        return false;
    }
}
