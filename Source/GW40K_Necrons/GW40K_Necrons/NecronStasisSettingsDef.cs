using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Global tuning for stasis crypt cycle length. Per-pawn multiplier comes from race <see cref="NonOrganicPawn"/> / <see cref="NecronMechExtension"/>.</summary>
public class NecronStasisSettingsDef : Def
{
    /// <summary>Base duration of one full stasis cycle in in-game hours before the race multiplier (scaled by <see cref="NecronStasisUtility.GetEternalSlumberLengthFactor"/>).</summary>
    public float baseStasisHours = 12f;

    /// <summary>Extra stasis hours added per point of summed <see cref="Hediff_Injury"/> severity on entry (clamped by <see cref="maxInjuryExtraHours"/>).</summary>
    public float injuryHoursPerTotalSeverity = 6f;

    /// <summary>Cap on extra hours from injuries.</summary>
    public float maxInjuryExtraHours = 48f;

    /// <summary>How often the crypt applies a healing pulse while occupied.</summary>
    public int healIntervalTicks = 600;

    /// <summary>Hit points of injury healing budget per in-game day while in stasis (scaled by deathrest efficiency when <see cref="Gene_Deathrest"/> is present).</summary>
    public float healPointsPerDayWhileInStasis = 80f;

    /// <summary>Stasis crypt burns this × (pawn daily necrodermis need in stack units).</summary>
    public float stasisNecrodermisBurnMultiplier = 1.33f;

    /// <summary>
    /// Must stay equal to <c>GW40k_Necron_Necrodermis</c> → <c>CompProperties_UseEffectNecrodermisPackConsume.nutritionPerUnit</c> in ThingDefs.
    /// Burn math also uses <c>GW_UD_Necrodermis.fallPerDay</c> from Need defs — change both together when rebalancing.
    /// </summary>
    public float necrodermisNutritionPerConsumedUnit = 0.05f;
}
