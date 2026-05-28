using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Critical-priority alert (same readout tier as urgent medical/colonist danger) when a Nech is
/// uncontrolled for <see cref="CompNechUncontrolledTimer.HostileLockoutImminentSeconds"/>+
/// while its map tile is not fogged (skipped for unrevealed ancient dangers, etc.).
/// The timer forcing hostile takeover at ~999 s still applies regardless.
/// </summary>
public class Alert_NechHostileLockoutImminent : Alert
{
    private readonly List<GlobalTargetInfo> targets = new();
    private readonly List<string> labels = new();

    public Alert_NechHostileLockoutImminent()
    {
        defaultPriority = AlertPriority.Critical;
    }

    public override string GetLabel() =>
        targets.Count == 1
            ? "GW40K_AlertHostileLockoutImminent_Single".Translate(labels[0].Named("PAWN"))
            : "GW40K_AlertHostileLockoutImminent_Multi".Translate(targets.Count.ToStringCached().Named("COUNT"));

    public override TaggedString GetExplanation() =>
        "GW40K_AlertHostileLockoutImminentDesc".Translate(labels.ToLineList("  - ").Named("CULPRITS"));

    public override AlertReport GetReport()
    {
        CalculateTargets();
        return AlertReport.CulpritsAre(targets);
    }

    internal static bool MatchesHostileLockoutImminent(Pawn pawn, int ticksGame)
    {
        if (pawn == null || pawn.Dead || pawn.Destroyed)
            return false;
        CompNechUncontrolledTimer timer = pawn.TryGetComp<CompNechUncontrolledTimer>();
        if (timer == null)
            return false;

        int sec = timer.UncontrolledSecondsAtTick(ticksGame);
        if (sec < CompNechUncontrolledTimer.HostileLockoutImminentSeconds)
            return false;

        return NechUncontrolledRevealUtility.IsRevealedForCriticalUncontrolledAlert(pawn);
    }

    private void CalculateTargets()
    {
        targets.Clear();
        labels.Clear();
        int t = Find.TickManager.TicksGame;

        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned.ToList())
        {
            if (pawn.Faction != Faction.OfPlayer)
                continue;
            if (pawn.def.GetModExtension<NecronMechExtension>() == null)
                continue;
            if (NechInspectStringUtility.IsNechProperlyCommanded(pawn))
                continue;
            if (!MatchesHostileLockoutImminent(pawn, t))
                continue;

            targets.Add(pawn);
            labels.Add(pawn.LabelShortCap);
        }
    }
}
