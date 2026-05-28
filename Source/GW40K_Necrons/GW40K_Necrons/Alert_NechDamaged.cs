using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Right-side alert: player-faction Nech pawns that have sustained damage
/// (injuries or missing body parts). Clicking selects the damaged Nech.
/// </summary>
public class Alert_NechDamaged : Alert
{
    private readonly List<GlobalTargetInfo> targets = new();
    private readonly List<string> labels = new();

    public Alert_NechDamaged()
    {
        defaultPriority = AlertPriority.Medium;
    }

    public override string GetLabel() =>
        targets.Count == 1
            ? "GW40K_AlertNechDamaged_Single".Translate(labels[0].Named("PAWN"))
            : "GW40K_AlertNechDamaged_Multi".Translate(targets.Count.ToStringCached().Named("COUNT"));

    public override TaggedString GetExplanation() =>
        "GW40K_AlertNechDamagedDesc".Translate(labels.ToLineList("  - ").Named("CULPRITS"));

    public override AlertReport GetReport()
    {
        CalculateTargets();
        return AlertReport.CulpritsAre(targets);
    }

    private void CalculateTargets()
    {
        targets.Clear();
        labels.Clear();

        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
        {
            if (pawn.Faction != Faction.OfPlayer) continue;
            if (!NechEnergyUtility.IsNecronPawn(pawn)) continue;
            if (pawn.def?.defName?.IndexOf("Scarab", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (pawn.health.summaryHealth.SummaryHealthPercent >= 1f) continue;

            targets.Add(pawn);
            labels.Add(pawn.LabelShortCap);
        }
    }
}
