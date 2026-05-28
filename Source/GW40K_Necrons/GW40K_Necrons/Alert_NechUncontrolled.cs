using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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

    // Pulsing orange background — mirrors Alert_Critical's red pulse but tinted orange.
    protected override Color BGColor
    {
        get
        {
            float num = Pulser.PulseBrightness(0.5f, Pulser.PulseBrightness(0.5f, 0.6f));
            return new Color(num, num * 0.45f, 0f);
        }
    }

    public Alert_NechUncontrolled()
    {
        defaultPriority = AlertPriority.High;
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
        int ticks = Find.TickManager.TicksGame;

        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned.ToList())
        {
            if (pawn.Faction != Faction.OfPlayer) continue;
            if (!NechUtility.IsNechControlled(pawn)) continue;
            if (NechInspectStringUtility.IsNechProperlyCommanded(pawn)) continue;
            if (Alert_NechHostileLockoutImminent.MatchesHostileLockoutImminent(pawn, ticks)) continue;

            targets.Add(pawn);
            labels.Add(pawn.LabelShortCap);
        }
    }
}
