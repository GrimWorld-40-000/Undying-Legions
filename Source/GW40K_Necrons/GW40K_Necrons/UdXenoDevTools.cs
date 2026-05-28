using System;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Dev-mode / <see cref="Verse.DebugToolsSpawning"/> helpers for UD_ xeno pawn kinds.
/// </summary>
public static class UdXenoDevTools
{
    public const string DevMenuCategory = "Xeno";

    /// <summary>
    /// Pawn kinds that appear under the Xeno dev spawn category instead of Humanlike / Mechanoids.
    /// Covers humanlike Necron races (<see cref="NonOrganicPawn"/> or <see cref="NecronMechExtension"/>)
    /// and Canoptek constructs (marked with <see cref="NecronGeneUtil.RaceExtension_Canoptek"/>).
    /// Excludes unused/test entries.
    /// </summary>
    public static bool IsUdXenoPawnKind(PawnKindDef pk)
    {
        if (pk?.defName == null || pk.race == null)
            return false;
        if (pk.defName.IndexOf("Unused", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        // Canoptek constructs (Scarab swarms etc.) — not humanlike, but belong in Xeno.
        if (pk.race.GetModExtension<NecronGeneUtil.RaceExtension_Canoptek>() != null)
            return true;

        // Humanlike Necron races.
        bool isNecronRace = pk.race.GetModExtension<NecronMechExtension>() != null
                         || pk.race.GetModExtension<NonOrganicPawn>() != null;
        return isNecronRace && pk.race.race?.intelligence == Intelligence.Humanlike;
    }
}
