using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Returns true while the pawn is in the GW40K_NechRogue mental break.
    /// Rogue Nechs bypass all gauss energy checks — the psychic overload that
    /// drives the break also floods their capacitors with unregulated charge.
    /// </summary>
    internal static bool IsRogue(Pawn pawn) =>
        pawn != null
        && NecronDefOfs.GW40K_NechRogue != null
        && pawn.MentalStateDef == NecronDefOfs.GW40K_NechRogue;

    /// <summary>True when the pawn's equipped weapon has a gauss extension with showEnergyGizmo = true.</summary>
    internal static bool HasEquippedGaussWeapon(Pawn pawn)
    {
        ThingWithComps primary = pawn?.equipment?.Primary;
        if (primary?.def == null) return false;
        var ext = primary.def.GetModExtension<ModExtension_GaussWeapon>();
        return ext != null && ext.showEnergyGizmo;
    }

    internal static void SyncGaussControlsWithGizmo(Pawn pawn)
    {
        if (pawn == null)
            return;
        HediffComp_GaussCapacitor cap = NechEnergyUtility.GetCapacitorComp(pawn);
        if (cap == null)
            return;
        if (HasEquippedGaussWeapon(pawn))
            return;

        cap.allowBatteryCharge = false;
        cap.allowCoreCharge = false;
        cap.allowAutoConsume = false;
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
    }
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

        // Rogue Nechs have effectively infinite gauss charge — never block their shots.
        if (GaussWeaponUtil.IsRogue(pawn)) return;

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

        // Rogue break = effectively infinite charge; suppress drain so the break doesn't
        // stranded them at 0 energy the moment it ends.
        if (GaussWeaponUtil.IsRogue(pawn)) return;

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

        // Rogue break = effectively infinite charge; suppress drain.
        if (GaussWeaponUtil.IsRogue(pawn)) return;

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

        // Rogue break = full power regardless of stored charge.
        if (GaussWeaponUtil.IsRogue(attacker)) return;

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

// ── Colonist Necron bios — inject Gauss Energy gizmo when a gauss weapon is equipped ──
// Skipped for Nech-pipeline pawns (Flayed One, Warrior colonist) — their gizmo comes from HarmonyPatch_NechGizmos.

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
[HarmonyPriority(Priority.Last)]
public static class HarmonyPatch_GaussWeapon_ColonistGizmo
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
    {
        foreach (Gizmo g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "ColonistGauss"))
            yield return g;

        if (__instance?.def?.GetModExtension<NecronMechExtension>() != null)
            yield break;
        if (NechUtility.IsHumanlikeNechControlled(__instance))
            yield break;

        bool isNonOrganic = __instance?.def?.GetModExtension<NonOrganicPawn>() != null;
        bool hasCapacitorApparel = __instance?.apparel?.WornApparel
            .Any(a => a.GetComp<Comp_GaussCapacitorApparel>() != null) == true;
        if (!isNonOrganic && !hasCapacitorApparel)
            yield break;

        if (NechEnergyUtility.GetCapacitorComp(__instance) == null)
            yield break;
        if (__instance.Faction != Faction.OfPlayer)
            yield break;
        if (!GaussWeaponUtil.HasEquippedGaussWeapon(__instance))
            yield break;

        yield return new Gizmo_NechEnergy(__instance);
    }
}

// ── First-encounter info letter ───────────────────────────────────────────────
// Fires once per save the first time any gauss weapon lands on the map as a loose item.

[HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
public static class HarmonyPatch_GaussWeapon_IntroLetter
{
    public static void Postfix(Thing __instance, bool respawningAfterLoad)
    {
        if (respawningAfterLoad) return;
        if (__instance?.def?.GetModExtension<ModExtension_GaussWeapon>() == null) return;

        var tracker = GameComponent_NecronLetters.Current;
        if (tracker == null || tracker.GaussEnergyIntroShown) return;

        tracker.GaussEnergyIntroShown = true;

        Find.LetterStack?.ReceiveLetter(
            "About: Gauss Energy",
            "Gauss energy is a type of particle energy utilized by Necrons to charge their Archeotech weapons. " +
            "Without it, ranged weapons will cease to fire and melee weapons will decrease in potency.\n\n" +
            "Necrons have a built-in gauss capacitor that can be charged at a Monolith, a battery, or with " +
            "their own internal Core Flux.\n\n" +
            "Non-Necron pawns will need to equip a gauss capacitor in order to utilize gauss weapons.\n\n" +
            "You can review this tip again in the Learning Helper.",
            LetterDefOf.NeutralEvent,
            new LookTargets(__instance));
    }
}

// ── Ensure capacitor exists when a gauss weapon is equipped ──────────────────
// Guards against pawns that missed spawn-time setup (e.g. dev-spawned, scenario edge cases).

[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
public static class HarmonyPatch_GaussWeapon_EnsureCapacitorOnEquip
{
    public static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
    {
        var ext = eq?.def?.GetModExtension<ModExtension_GaussWeapon>();
        if (ext == null || !ext.showEnergyGizmo) return;

        Pawn pawn = __instance.pawn;
        if (!NechEnergyUtility.IsNecronPawn(pawn)) return;
        if (pawn?.def?.defName == "GW40K_ScarabSwarm") return;
        if (NechEnergyUtility.GetCapacitorComp(pawn) != null) return;

        HediffDef capacitorDef = NechEnergyUtility.IsOverlord(pawn)
            ? NechEnergyUtility.CapacitorLargeDef
            : NechEnergyUtility.CapacitorSmallDef;
        if (capacitorDef == null) return;

        pawn.health.AddHediff(capacitorDef);
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        Need energy = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        if (energy != null) energy.CurLevel = 1f;

        GaussWeaponUtil.SyncGaussControlsWithGizmo(pawn);
    }
}

[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
public static class HarmonyPatch_GaussWeapon_DisableControlsOnUnequip
{
    public static void Postfix(Pawn_EquipmentTracker __instance)
    {
        GaussWeaponUtil.SyncGaussControlsWithGizmo(__instance?.pawn);
    }
}

// ── Sentience Catalyst: rare natural spawn on Warriors and Immortals ──────────
// 3% chance per generated pawn. RimWorld 1.6 removed PawnGenerator.PostGeneratePawn;
// postfix after gear matches "post-generation" timing closely enough for this rare roll.

[HarmonyPatch(typeof(PawnGenerator), "GenerateGearFor")]
public static class HarmonyPatch_SentienceCatalyst_NaturalSpawn
{
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, PawnGenerationRequest request)
    {
        if (pawn?.def == null || pawn.health?.hediffSet == null)
            return;

        string race = pawn.def.defName;
        if (race != "GW40K_NecronWarrior" && race != "GW40K_NecronImmortal")
            return;

        if (NecronDefOfs.GW40K_SentienceCatalystImplant == null)
            return;
        if (pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_SentienceCatalystImplant))
            return;

        if (!Rand.Chance(0.03f))
            return;

        BodyPartRecord mindCore = pawn.RaceProps.body.AllParts
            .FirstOrDefault(p => p.def?.defName == "GW40K_Necron_MindCore");

        pawn.health.AddHediff(NecronDefOfs.GW40K_SentienceCatalystImplant, mindCore);
    }
}

// ── Sentience Catalyst: weapon equip restriction for Warriors and Immortals ────
// Without the catalyst, Warriors (GW40K_NecronWarrior) and Immortals
// (GW40K_NecronImmortal) can only equip weapons tagged GW_Necron_* (their
// issued gauss/tesla arms). Melee weapons are unrestricted as a fallback.
// Installing the Sentience Catalyst hediff lifts the restriction entirely.

[HarmonyPatch]
public static class HarmonyPatch_SentienceCatalyst_EquipRestriction
{
    // RimWorld 1.6 has multiple CanEquip overloads; resolve explicitly to avoid Harmony ambiguous match.
    [HarmonyTargetMethod]
    private static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(EquipmentUtility),
            nameof(EquipmentUtility.CanEquip),
            new[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) });

    [HarmonyPostfix]
    public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, bool checkBonded, ref bool __result)
    {
        if (!__result || pawn?.def == null || thing?.def == null)
            return;

        string race = pawn.def.defName;
        if (race != "GW40K_NecronWarrior" && race != "GW40K_NecronImmortal")
            return;

        // Melee weapons are always allowed as a combat fallback.
        if (!thing.def.IsRangedWeapon)
            return;

        // Sentience Catalyst lifts all restrictions.
        if (NecronDefOfs.GW40K_SentienceCatalystImplant != null &&
            pawn.health?.hediffSet?.HasHediff(NecronDefOfs.GW40K_SentienceCatalystImplant) == true)
            return;

        // Allow any weapon with a GW_Necron_ tag (gauss, tesla, etc.).
        List<string> tags = thing.def.weaponTags;
        if (tags != null)
        {
            foreach (string tag in tags)
            {
                if (tag.StartsWith("GW_Necron_"))
                    return;
            }
        }

        cantReason = "GW40K_SentienceCatalystRequired".Translate();
        __result = false;
    }
}
