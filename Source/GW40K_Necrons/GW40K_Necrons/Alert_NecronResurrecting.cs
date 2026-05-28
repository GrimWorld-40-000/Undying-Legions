using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Right-side alert: hostile Necron corpses that are actively in the resurrection protocol.
/// Replaces the per-pawn enemy resurrection letter spam — one letter fires at most once
/// per 6 game-hours (handled in CompResurrectible), while this alert remains visible for
/// as long as any enemy is reanimating, listing all culprits as clickable targets.
///
/// RimWorld auto-discovers Alert subclasses via reflection, so no XML registration needed.
/// </summary>
public class Alert_NecronResurrecting : Alert_Critical
{
    private readonly List<GlobalTargetInfo> targets = new();
    private readonly List<string>           labels  = new();

    // Alert_Critical fires a ThreatBig message every frame the alert is active;
    // suppress it — CompResurrectible already sends a targeted letter.
    protected override bool DoMessage => false;

    public override string GetLabel() =>
        "GW40K_AlertEnemyResurrecting".Translate();

    public override TaggedString GetExplanation() =>
        "GW40K_AlertEnemyResurrectingDesc"
            .Translate(labels.ToLineList("  - ").Named("CULPRITS"));

    public override AlertReport GetReport()
    {
        CalculateTargets();
        return AlertReport.CulpritsAre(targets);
    }

    private void CalculateTargets()
    {
        targets.Clear();
        labels.Clear();

        HediffDef resurrDef = NecronDefOfs.GW40K_Necron_ResurrectionActive;
        if (resurrDef == null) return;

        foreach (Map map in Find.Maps)
        {
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t is not Corpse corpse) continue;
                Pawn inner = corpse.InnerPawn;
                if (inner == null) continue;
                if (inner.Faction == null || !inner.Faction.HostileTo(Faction.OfPlayer)) continue;
                if (!inner.health.hediffSet.HasHediff(resurrDef)) continue;

                targets.Add(corpse);
                labels.Add(inner.LabelShortCap);
            }
        }
    }
}
