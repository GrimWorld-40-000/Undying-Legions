using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// ── Flame damage immunity ─────────────────────────────────────────────────────
// Intercept at TakeDamage so DamageWorker_Flame never runs — no fire attaches.

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class HarmonyPatch_NecronFireImmunity
{
    [HarmonyPrefix]
    public static bool Prefix(Thing __instance, DamageInfo dinfo, ref DamageWorker.DamageResult __result)
    {
        if (dinfo.Def != DamageDefOf.Flame) return true;
        if (__instance is not Pawn pawn) return true;
        if (!NechEnergyUtility.IsNecronPawn(pawn)) return true;

        __result = new DamageWorker.DamageResult();
        return false;
    }
}

// ── Burn wound type redirect ───────────────────────────────────────────────────
// Necrons are inorganic — tissue burns don't apply. When RimWorld would assign a
// Burn injury hediff (e.g. from Tesla Carbine electrical damage), redirect it to
// Scratch so the wound heals normally and doesn't spam "fully healed" notifications
// caused by Burn's tender/severity cycle on a pawn with no natural healing rate.

[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.GetHediffDefFromDamage))]
public static class HarmonyPatch_NecronBurnRedirect
{
    private static HediffDef _burnDef;
    private static HediffDef _scratchDef;

    [HarmonyPostfix]
    public static void Postfix(ref HediffDef __result, Pawn pawn)
    {
        _burnDef   ??= DefDatabase<HediffDef>.GetNamed("Burn",    errorOnFail: false);
        _scratchDef ??= DefDatabase<HediffDef>.GetNamed("Scratch", errorOnFail: false);

        if (_burnDef == null || _scratchDef == null) return;
        if (__result != _burnDef) return;
        if (!NechEnergyUtility.IsNecronPawn(pawn)) return;

        __result = _scratchDef;
    }
}
