using HarmonyLib;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Makes Canoptek Scarab swarms ignore terrain path cost penalties.
/// Sets the total move cost per cell to the pawn's base cardinal movement ticks,
/// stripping any terrain overhead added by PathGrid.PerceivedPathCostAt.
/// </summary>
[HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(IntVec3) })]
public static class HarmonyPatch_ScarabTerrain
{
    private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnRef =
        AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

    [HarmonyPostfix]
    public static void Postfix(Pawn_PathFollower __instance, ref float __result)
    {
        Pawn pawn = PawnRef(__instance);
        if (pawn?.def?.defName != "GW40K_ScarabSwarm") return;

        __result = pawn.TicksPerMoveCardinal;
    }
}
