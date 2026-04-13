using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Right-side alert: appears when a player colonist has a Gauss weapon equipped
/// but insufficient Gauss energy to fire it. Clicking the alert selects the pawn.
/// </summary>
public class Alert_LowGaussEnergy : Alert
{
    private readonly List<GlobalTargetInfo> targets = new();
    private readonly List<string> targetLabels = new();

    public Alert_LowGaussEnergy()
    {
        defaultPriority = AlertPriority.Medium;
    }

    public override string GetLabel() =>
        targets.Count == 1
            ? "GW40K_AlertLowGaussEnergy_Single".Translate(targetLabels[0].Named("PAWN"))
            : "GW40K_AlertLowGaussEnergy_Multi".Translate(targets.Count.ToStringCached().Named("COUNT"));

    public override TaggedString GetExplanation() =>
        "GW40K_AlertLowGaussEnergyDesc".Translate(targetLabels.ToLineList("  - ").Named("CULPRITS"));

    public override AlertReport GetReport()
    {
        CalculateTargets();
        return AlertReport.CulpritsAre(targets);
    }

    private void CalculateTargets()
    {
        targets.Clear();
        targetLabels.Clear();

        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
        {
            if (pawn.Faction != Faction.OfPlayer) continue;
            if (!pawn.RaceProps.Humanlike) continue;

            ThingWithComps equip = pawn.equipment?.Primary;
            if (equip?.def == null) continue;

            ModExtension_GaussWeapon ext = equip.def.GetModExtension<ModExtension_GaussWeapon>();
            if (ext == null || ext.isMeleeGaussWeapon) continue; // melee degrades, doesn't disable

            float energy = GaussWeaponUtil.GaussEnergy(pawn);
            if (!GaussWeaponUtil.IsInsufficient(energy, ext)) continue;

            targets.Add(pawn);
            targetLabels.Add(pawn.NameShortColored.Resolve());
        }
    }
}
