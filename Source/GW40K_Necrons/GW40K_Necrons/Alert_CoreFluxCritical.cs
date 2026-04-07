using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class Alert_CoreFluxCritical : Alert
{
    private readonly List<GlobalTargetInfo> targets = new List<GlobalTargetInfo>();
    private readonly List<string> targetLabels = new List<string>();

    public override string GetLabel()
    {
        if (targets.Count == 1)
            return "AlertCoreFluxCriticalPawn".Translate(targetLabels[0].Named("PAWN"));
        return "AlertCoreFluxCriticalPawns".Translate(targets.Count.ToStringCached().Named("NUMCULPRITS"));
    }

    private void CalculateTargets()
    {
        targets.Clear();
        targetLabels.Clear();
        foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
        {
            if (!p.RaceProps.Humanlike || p.Faction != Faction.OfPlayer || p.needs == null)
                continue;
            if (!p.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux, out Need need))
                continue;
            if (need is not MaintenanceNeed mNeed)
                continue;
            if (mNeed.CurLevel > MaintenanceNeed.LevelForCriticalAlert)
                continue;
            if (mNeed.CoreFluxReplenishing())
                continue;
            targets.Add(p);
            targetLabels.Add(p.NameShortColored.Resolve());
        }
    }

    public override TaggedString GetExplanation()
    {
        return "AlertCoreFluxCriticalDesc".Translate(targetLabels.ToLineList("  - ").Named("CULPRITS"));
    }

    public override AlertReport GetReport()
    {
        CalculateTargets();
        return AlertReport.CulpritsAre(targets);
    }
}
