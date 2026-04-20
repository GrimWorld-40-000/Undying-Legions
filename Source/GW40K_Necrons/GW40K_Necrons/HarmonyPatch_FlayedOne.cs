using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// ── Retain claws when downed ──────────────────────────────────────────────────
// Flayed One claws are a biological extension of the curse — not held weapons.
// Suppress DropAllEquipment so they aren't left on the ground when downed.

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

// ── Ghostwind partial transparency ───────────────────────────────────────────
// visibleToPlayer:true returns 1f unconditionally from GetAlpha, so the pawn
// renders fully opaque while in Ghostwind. Intercept here to return a ghost
// alpha instead, making the pawn visibly ethereal to the player.

[HarmonyPatch(typeof(HediffComp_Invisibility), nameof(HediffComp_Invisibility.GetAlpha))]
public static class HarmonyPatch_GhostwindTransparency
{
    private const float GhostAlpha = 0.35f;

    public static void Postfix(HediffComp_Invisibility __instance, ref float __result)
    {
        if (__instance.parent?.def?.defName != "GW40K_FlayedOne_Stealth") return;
        __result = GhostAlpha;
    }
}
