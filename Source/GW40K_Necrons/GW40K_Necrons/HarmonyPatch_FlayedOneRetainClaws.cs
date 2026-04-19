using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Prevents FlayedOne claws from being dropped when downed.
/// Claws are a biological extension of the FlayedOne's curse — not a held weapon.
/// </summary>
[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.DropAllEquipment))]
public static class HarmonyPatch_FlayedOneRetainClaws
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn_EquipmentTracker __instance)
    {
        string defName = __instance.pawn?.def?.defName;
        if (defName == "UD_Necron_FlayedOne" || defName == "GW40K_NecronFlayedOne")
            return false;
        return true;
    }
}
