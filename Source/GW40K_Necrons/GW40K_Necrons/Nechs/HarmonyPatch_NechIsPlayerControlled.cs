using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Pawn.IsPlayerControlled returns false for Nechs because they lack CompOverseerSubject.
/// Vanilla uses this property to enable/disable weapon gizmos and ordered jobs ("Cannot order
/// characters you do not control"). Patch it to return true for Nechs that have an active
/// commander in HediffComp_NecronCommandTracker.
/// </summary>
[HarmonyPatch(typeof(Pawn), "IsPlayerControlled", MethodType.Getter)]
public static class HarmonyPatch_NechIsPlayerControlled
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, ref bool __result)
    {
        if (__result) return;
        if (__instance == null) return;
        if (__instance.Faction != Faction.OfPlayer) return;

        if (NechUtility.IsHumanlikeNechControlled(__instance)
            && HediffComp_NecronCommandTracker.GetCommanderOf(__instance) != null)
        {
            __result = true;
            return;
        }

        if (__instance.def?.GetModExtension<NecronMechExtension>() != null
            && HediffComp_NecronCommandTracker.GetCommanderOf(__instance) != null)
        {
            __result = true;
            return;
        }

        // Control-node bearers (Canoptek Spyder, etc.): they command scarabs but are not listed in
        // controlledScarabs — without this, IsPlayerControlled stays false and draft attack gizmos never appear.
        if (__instance.def?.GetModExtension<NecronMechExtension>() != null
            && HediffComp_ControlNodeTracker.GetTracker(__instance) != null)
        {
            __result = true;
            return;
        }

        if (ControlNodeUtility.IsCanoptek(__instance)
            && HediffComp_ControlNodeTracker.GetControllerOfScarab(__instance) != null)
            __result = true;
    }
}
