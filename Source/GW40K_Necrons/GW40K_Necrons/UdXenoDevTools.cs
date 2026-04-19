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
    /// Pawn kinds that should appear under the Xeno dev spawn category (not Humanlike / Mechanoids).
    /// Matches any pawn whose race carries <see cref="NecronMechExtension"/> — covers all Nechs and
    /// Nech-controlled humanlike pawns (e.g. the converted Flayed One) without requiring a name prefix.
    /// </summary>
    public static bool IsUdXenoPawnKind(PawnKindDef pk)
    {
        if (pk?.defName == null || pk.race == null)
            return false;
        if (pk.defName.IndexOf("Unused", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (pk.race.GetModExtension<NecronMechExtension>() == null)
            return false;
        // Exclude non-humanlike races (Nechanoids etc.) — they can't be generated as standard pawns.
        return pk.race.race?.intelligence == Intelligence.Humanlike;
    }
}
