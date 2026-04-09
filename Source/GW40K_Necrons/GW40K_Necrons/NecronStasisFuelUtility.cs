using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Stasis crypt fuel: burn rate = (GW_UD_Necrodermis <see cref="NeedDef.fallPerDay"/> / nutrition per unit) × burn multiplier,
/// in the same “pieces per day” units as <see cref="CompRefuelable"/> storage.
/// </summary>
/// <remarks>
/// <para><b>Keep these in sync when you rebalance:</b></para>
/// <list type="bullet">
/// <item><description><c>Defs/Need_Necron.xml</c> → <c>GW_UD_Necrodermis</c> → <c>fallPerDay</c> (loaded at runtime via <see cref="DefDatabase{T}"/>).</description></item>
/// <item><description><c>Defs/ThingDefs/ThingDef_Items.xml</c> → <c>GW40k_Necron_Necrodermis</c> → <c>CompProperties_UseEffectNecrodermisPackConsume.nutritionPerUnit</c> must match
/// <see cref="NecronStasisSettingsDef.necrodermisNutritionPerConsumedUnit"/> (or change both together).</description></item>
/// </list>
/// <para><b>Why C# instead of only XML <see cref="CompProperties_Refuelable.fuelConsumptionRate"/>?</b>
/// Vanilla <see cref="CompRefuelable"/> with <c>consumeFuelOnlyWhenUsed == false</c> subtracts <c>fuelConsumptionRate / TicksPerDay</c> every tick while the building is on —
/// not tied to stasis cycle progress. We set <c>consumeFuelOnlyWhenUsed</c> true to turn that off, then <see cref="NecronCasket"/> calls
/// <see cref="CompStasisCryptNecrodermisRefuelable.BurnFuelForStasisProcessingTick"/> only when the crypt actually advances the cycle (and the formula above stays one place in code).</para>
/// </remarks>
public static class NecronStasisFuelUtility
{
    private const string NecrodermisNeedDefName = "GW_UD_Necrodermis";

    /// <summary>How many necrodermis stack units per day a pawn needs to break even on <see cref="NeedDef.fallPerDay"/>.</summary>
    public static float PawnNecrodermisUnitsPerDay()
    {
        NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(NecrodermisNeedDefName);
        float fallPerDay = need?.fallPerDay ?? 0.15f;
        float nutrition = NecrodermisNutritionPerUnitFromSettings();
        if (nutrition <= 0f)
            nutrition = 0.05f;
        return fallPerDay / nutrition;
    }

    /// <summary>Stasis crypt necrodermis consumed per in-game day while a cycle is actively processing.</summary>
    public static float StasisNecrodermisUnitsBurnedPerDay()
    {
        NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
        float mul = s != null && s.stasisNecrodermisBurnMultiplier > 0f ? s.stasisNecrodermisBurnMultiplier : 1.33f;
        return PawnNecrodermisUnitsPerDay() * mul;
    }

    public static float NecrodermisNutritionPerUnitFromSettings()
    {
        NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
        if (s != null && s.necrodermisNutritionPerConsumedUnit > 0f)
            return s.necrodermisNutritionPerConsumedUnit;
        return 0.05f;
    }

    /// <summary>Fuel consumed per game tick while stasis processing runs (power on, flick on, has fuel).</summary>
    public static float StasisFuelConsumedPerTick()
    {
        return StasisNecrodermisUnitsBurnedPerDay() / GenDate.TicksPerDay;
    }
}
