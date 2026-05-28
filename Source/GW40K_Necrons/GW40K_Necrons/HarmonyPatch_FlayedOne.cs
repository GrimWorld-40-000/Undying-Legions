using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// ── Ghostwind partial transparency ───────────────────────────────────────────
// visibleToPlayer:true returns 1f unconditionally from GetAlpha, so the pawn
// renders fully opaque while in Ghostwind. Intercept here to return a ghost
// alpha instead, making the pawn visibly ethereal to the player.

[HarmonyPatch(typeof(HediffComp_Invisibility), nameof(HediffComp_Invisibility.GetAlpha))]
public static class HarmonyPatch_GhostwindTransparency
{
    private const float GhostAlpha = 0.55f;

    public static void Postfix(HediffComp_Invisibility __instance, ref float __result)
    {
        if (__instance.parent?.def?.defName != "GW40K_FlayedOne_Stealth") return;
        __result = GhostAlpha;
    }
}

// ── Flayed One cannot equip weapons ──────────────────────────────────────────
// Flayed Ones fight exclusively with their claws — the Flayer Virus has stripped
// them of the cognitive capacity to operate weapons. Block all equipment changes.

internal static class FlayedOneConstants
{
    internal const string RaceDefName    = "GW40K_NecronFlayedOne";
    internal const string ClawsOnlyMsg   = "Flayed Ones fight only with their claws.";
}

[HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
    new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
public static class HarmonyPatch_FlayedOne_CannotEquip
{
    public static bool Prefix(Pawn pawn, out string cantReason, ref bool __result)
    {
        if (pawn?.def?.defName != FlayedOneConstants.RaceDefName)
        {
            cantReason = null;
            return true;
        }
        cantReason = FlayedOneConstants.ClawsOnlyMsg;
        __result = false;
        return false;
    }
}

// ── Flayed One cannot drop weapons ───────────────────────────────────────────
// The Drop option surfaces via FloatMenuOptionProvider_DropEquipment whenever
// primary equipment is set. Replace it with a disabled entry so the player
// can see why rather than wondering where the option went.

[HarmonyPatch(typeof(FloatMenuOptionProvider_DropEquipment), "GetSingleOptionFor",
    new[] { typeof(Pawn), typeof(FloatMenuContext) })]
public static class HarmonyPatch_FlayedOne_NoDropWeapon
{
    public static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
    {
        if (clickedPawn?.def?.defName != FlayedOneConstants.RaceDefName)
            return true;
        if (clickedPawn.equipment?.Primary == null)
            return true;

        ThingWithComps primary = clickedPawn.equipment.Primary;
        string label = "Drop".Translate(primary.Label, primary)
                       + " (" + FlayedOneConstants.ClawsOnlyMsg + ")";
        __result = new FloatMenuOption(label, null);
        return false;
    }
}
