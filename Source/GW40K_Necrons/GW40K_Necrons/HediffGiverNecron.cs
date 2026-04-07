using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffGiverNecron : HediffGiver
{
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

        RestCategory curCategory = need.CurCategory;
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (curCategory == RestCategory.Rested)
        {
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
            return;
        }

        if (hediff == null)
        {
            hediff = HediffMaker.MakeHediff(NecronDefOfs.GW40K_Necron_TiredHediff, pawn);
            pawn.health.AddHediff(hediff);
        }

        switch (curCategory)
        {
            case RestCategory.Tired:
                hediff.Severity = 0.2f;
                break;
            case RestCategory.VeryTired:
                hediff.Severity = 0.4f;
                break;
            case RestCategory.Exhausted:
                hediff.Severity = 0.8f;
                break;
        }
    }

    private static void RemoveStrain(Pawn pawn)
    {
        Hediff h = pawn.health?.hediffSet?.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
        if (h != null)
            pawn.health.RemoveHediff(h);
    }
}
