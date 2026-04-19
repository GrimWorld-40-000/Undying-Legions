using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

// ── Helper ────────────────────────────────────────────────────────────────────

internal static class GaussWeaponUtil
{
    internal static float GaussEnergy(Pawn pawn) =>
        (pawn?.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy)?.CurLevel ?? 0f;

    internal static ModExtension_GaussWeapon RangedExt(ThingWithComps equip)
    {
        if (equip?.def == null) return null;
        var ext = equip.def.GetModExtension<ModExtension_GaussWeapon>();
        return (ext != null && !ext.isMeleeGaussWeapon) ? ext : null;
    }

    internal static bool IsInsufficient(float energy, ModExtension_GaussWeapon ext) =>
        energy <= 0f || (ext.gaussConsumption > 0f && energy < ext.gaussConsumption);
}

// ── Ranged — block AI from picking a gauss verb when energy is too low ────────

[HarmonyPatch(typeof(Verb), "Available")]
public static class HarmonyPatch_GaussWeapon_VerbAvailable
{
    public static void Postfix(Verb __instance, ref bool __result)
    {
        if (!__result) return;

        ModExtension_GaussWeapon ext = GaussWeaponUtil.RangedExt(__instance.EquipmentSource);
        if (ext == null) return;

        Pawn pawn = __instance.CasterPawn;
        if (pawn == null) return;

        if (GaussWeaponUtil.IsInsufficient(GaussWeaponUtil.GaussEnergy(pawn), ext))
            __result = false;
    }
}

// ── Ranged — grey out the player attack gizmo and supply a rejection message ──
// Only touches Command_VerbTarget after gizmos exist; does not call FloatMenuUtility.UseRangedAttack.

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class HarmonyPatch_GaussWeapon_GizmoDisable
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
    {
        // Resolve energy once; avoids repeated need lookups per gizmo.
        float energy = GaussWeaponUtil.GaussEnergy(__instance);

        foreach (Gizmo g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "GaussWeapon"))
        {
            if (g is Command_VerbTarget cvt)
            {
                try
                {
                    ModExtension_GaussWeapon ext = GaussWeaponUtil.RangedExt(cvt.verb?.EquipmentSource);
                    if (ext != null && GaussWeaponUtil.IsInsufficient(energy, ext))
                        cvt.Disable("GW40K_GaussWeapon_Disabled".Translate());
                }
                catch
                {
                    // Bad verb/equipment from another mod; keep gizmo unchanged.
                }
            }

            yield return g;
        }
    }
}

// ── Ranged — drain gauss energy after each projectile is fired ────────────────

[HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
public static class HarmonyPatch_GaussWeapon_ConsumeOnShot
{
    public static void Postfix(Verb_Shoot __instance, bool __result)
    {
        if (!__result) return;

        ModExtension_GaussWeapon ext = GaussWeaponUtil.RangedExt(__instance.EquipmentSource);
        if (ext == null || ext.gaussConsumption <= 0f) return;

        Pawn pawn = __instance.CasterPawn;
        if (pawn == null) return;

        Need_NechEnergy need = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (need == null) return;

        need.CurLevel = Mathf.Max(0f, need.CurLevel - ext.gaussConsumption);
    }
}

// ── Melee — drain gauss energy after each successful melee strike ─────────────

[HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
public static class HarmonyPatch_GaussWeapon_MeleeConsume
{
    public static void Postfix(Verb_MeleeAttack __instance, bool __result)
    {
        if (!__result) return;

        ThingWithComps equip = __instance.EquipmentSource;
        if (equip?.def == null) return;

        var ext = equip.def.GetModExtension<ModExtension_GaussWeapon>();
        if (ext == null || !ext.isMeleeGaussWeapon || ext.gaussConsumption <= 0f) return;

        Pawn pawn = __instance.CasterPawn;
        if (pawn == null) return;

        Need_NechEnergy need = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (need == null) return;

        need.CurLevel = Mathf.Max(0f, need.CurLevel - ext.gaussConsumption);
    }
}

// ── Melee — reduce damage by 80% and armor penetration by 90% at zero energy ──

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_GaussWeapon_MeleeDamage
{
    private const float DamageRetained = 0.20f;   // 80% reduction
    private const float ArmorPenRetained = 0.10f; // 90% reduction

    // DamageInfo stores armor penetration in a private field; cache it once.
    private static readonly System.Reflection.FieldInfo s_apField =
        typeof(DamageInfo).GetField("armorPenetrationBase",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? typeof(DamageInfo).GetField("armorPenetration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    public static void Prefix(ref DamageInfo dinfo)
    {
        if (dinfo.Instigator is not Pawn attacker) return;

        ThingWithComps equip = attacker.equipment?.Primary;
        if (equip?.def == null) return;

        var ext = equip.def.GetModExtension<ModExtension_GaussWeapon>();
        if (ext == null || !ext.isMeleeGaussWeapon) return;

        if (GaussWeaponUtil.GaussEnergy(attacker) > 0f) return; // full power

        float origAP = s_apField != null ? (float)s_apField.GetValue(dinfo) : 0f;

        dinfo = new DamageInfo(
            dinfo.Def,
            dinfo.Amount * DamageRetained,
            armorPenetration: origAP * ArmorPenRetained,
            dinfo.Angle,
            dinfo.Instigator,
            dinfo.HitPart,
            dinfo.Weapon);
    }
}
