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
    /// </summary>
    public static bool IsUdXenoPawnKind(PawnKindDef pk)
    {
        if (pk?.defName == null || pk.race == null)
            return false;
        if (!pk.defName.StartsWith("UD_", StringComparison.Ordinal))
            return false;
        if (pk.defName.StartsWith("UD_Unused", StringComparison.OrdinalIgnoreCase))
            return false;
        if (pk.defName.IndexOf("Unused", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return true;
    }
}
