using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Right-side alert: player-faction Nech pawns that have no valid Nechinator
/// command link. Clicking the alert selects the uncontrolled Nech.
/// </summary>
public class Alert_NechUncontrolled : Alert
{
    private readonly List<GlobalTargetInfo> targets = new();
    private readonly List<string> labels = new();

    public Alert_NechUncontrolled()
    {
        defaultPriority = AlertPriority.Medium;
    }

    public override string GetLabel() => "GW40K_AlertNechUncontrolled".Translate();

    public override TaggedString GetExplanation() =>
        "GW40K_AlertNechUncontrolledDesc".Translate(labels.ToLineList("  - ").Named("CULPRITS"));

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
            if (pawn.def.GetModExtension<NecronMechExtension>() == null) continue;
            if (NechInspectStringUtility.IsNechProperlyCommanded(pawn)) continue;

            targets.Add(pawn);
            labels.Add(pawn.LabelShortCap);
        }
    }
}
