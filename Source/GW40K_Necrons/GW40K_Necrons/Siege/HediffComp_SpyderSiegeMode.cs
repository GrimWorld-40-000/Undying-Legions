using System.Collections.Generic;
using Verse;

namespace GW40K_Necrons;

public class HediffCompProperties_SpyderSiegeMode : HediffCompProperties
{
    public HediffCompProperties_SpyderSiegeMode() => compClass = typeof(HediffComp_SpyderSiegeMode);
}

/// <summary>
/// Applied to the Canoptek Spyder when it enters Stage 3 of the Necron siege.
/// The orange pulsing visual is driven by HarmonyPatch_SpyderSiegeEffect (same approach as
/// the green uncontrolled effect) — this comp just acts as the flag for that patch to check.
/// Move-speed reduction is handled declaratively in the HediffDef stage statFactors.
/// Ability granting (GW_UL_SpyderParticleCannon) is handled by HediffCompProperties_GiveAbility.
/// </summary>
public class HediffComp_SpyderSiegeMode : HediffComp
{
    /// <summary>
    /// True if the pawn has any siege-mode hediff active — checks via the
    /// HediffComp_SpyderSiegeMode comp so it covers both GW_UL_SpyderSiegeMode
    /// (siege-lord, permanent) and GW_UL_SpyderSiegeModeManual (ability, timed).
    /// </summary>
    public static bool IsSiegeMode(Pawn p)
    {
        if (p?.health?.hediffSet == null) return false;
        foreach (Hediff h in p.health.hediffSet.hediffs)
            if (h.TryGetComp<HediffComp_SpyderSiegeMode>() != null)
                return true;
        return false;
    }

    /// <summary>
    /// Removes every siege-mode hediff from <paramref name="p"/> — covers both the
    /// siege-lord permanent variant and the manual timed variant.
    /// Called when the Spyder transitions to an assault phase.
    /// </summary>
    /// <summary>Resets the siege cannon cooldown whenever siege mode is applied
    /// so the first volley is available immediately.</summary>
    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        Pawn?.TryGetComp<Comp_SpyderSiegeCannon>()?.ResetCooldown();
    }

    public static void RemoveAll(Pawn p)
    {
        if (p?.health?.hediffSet == null) return;
        var toRemove = new List<Hediff>();
        foreach (Hediff h in p.health.hediffSet.hediffs)
            if (h.TryGetComp<HediffComp_SpyderSiegeMode>() != null)
                toRemove.Add(h);
        foreach (Hediff h in toRemove)
            p.health.RemoveHediff(h);
    }
}
