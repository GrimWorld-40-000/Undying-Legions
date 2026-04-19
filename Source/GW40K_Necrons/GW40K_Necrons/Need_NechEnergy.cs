using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class Need_NechEnergy : Need
{
    private const float FallPerDay = 0.87f;
    private const float ChargerGainPerDayHalfRate = 0.5f;
    private const float MonolithGainPerDay = 1.0f;
    private const float IntervalsPerDay = 400f;

    /// <summary>How fast core charging fills the gauss meter (full 0→100% in half a game day).</summary>
    internal const float CoreChargeGainPerDay = 3.0f;
    /// <summary>Gauss % at or above which core charging is suppressed (no drain if already full enough).</summary>
    private const float CoreChargeCap = 0.99f;

    public Need_NechEnergy(Pawn pawn)
        : base(pawn)
    {
    }

    public override void SetInitialLevel()
    {
        CurLevel = 1f;
    }

    public override int GUIChangeArrow => IsChargingNow() ? 1 : -1;

    public override void NeedInterval()
    {
        float cap = NechEnergyUtility.CapacitorCapacity(pawn);
        if (cap <= 0f)
        {
            CurLevel = 0f;
            return;
        }

        float delta = -FallPerDay / IntervalsPerDay;
        if (IsUsingVanillaChargeJob())
            delta += ChargerGainPerDayHalfRate / IntervalsPerDay;
        if (IsNearPoweredMonolith())
            delta += MonolithGainPerDay / IntervalsPerDay;
        delta += TryCoreChargeGain();

        CurLevel += delta;
    }

    public bool IsChargingNow() =>
        IsUsingVanillaChargeJob() || IsNearPoweredMonolith()
        || IsUsingBatteryRechargeJob()
        || NechEnergyUtility.AllowCoreRecharge(pawn);

    private bool IsUsingBatteryRechargeJob()
    {
        if (NecronDefOfs.GW40K_Job_RechargeFromBattery == null)
            return false;
        return pawn?.CurJobDef == NecronDefOfs.GW40K_Job_RechargeFromBattery;
    }

    /// <summary>
    /// Applies passive core-flux-to-gauss charging. Returns the gauss gain delta for this interval.
    /// Drains core flux proportionally so a full charge (0→100%) costs exactly <c>coreFluxCostFull</c>.
    /// Auto-disables <see cref="HediffComp_GaussCapacitor.allowCoreCharge"/> when core flux is critical.
    /// </summary>
    private float TryCoreChargeGain()
    {
        HediffComp_GaussCapacitor cap = NechEnergyUtility.GetCapacitorComp(pawn);
        if (cap == null || !cap.allowCoreCharge)
            return 0f;

        MaintenanceNeed coreFlux = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_CoreFlux) as MaintenanceNeed;
        if (coreFlux == null)
            return 0f;

        // Auto-disable at critical core flux
        if (coreFlux.CurLevel <= MaintenanceNeed.LevelForCriticalAlert)
        {
            cap.allowCoreCharge = false;
            return 0f;
        }

        // Suppress if gauss is already at cap
        if (CurLevelPercentage >= CoreChargeCap)
            return 0f;

        float gainPerInterval = CoreChargeGainPerDay / IntervalsPerDay;
        // Don't overshoot CoreChargeCap
        gainPerInterval = Mathf.Min(gainPerInterval, CoreChargeCap - CurLevelPercentage);

        float fluxDrainPerInterval = gainPerInterval * cap.Props.coreFluxCostFull;

        // Don't drain core flux below the critical floor
        float availableFlux = coreFlux.CurLevel - MaintenanceNeed.LevelForCriticalAlert;
        if (availableFlux <= 0f)
            return 0f;

        // Scale down proportionally if we'd hit the floor this tick
        float scale = Mathf.Min(1f, availableFlux / fluxDrainPerInterval);
        coreFlux.CurLevel -= fluxDrainPerInterval * scale;
        return gainPerInterval * scale;
    }

    private bool IsUsingVanillaChargeJob()
    {
        string n = pawn?.CurJobDef?.defName;
        if (n.NullOrEmpty())
            return false;
        return n.IndexOf("Charge", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsNearPoweredMonolith()
    {
        if (!pawn.Spawned || pawn.Map == null)
            return false;
        foreach (Building b in pawn.Map.listerBuildings.allBuildingsColonist)
        {
            if (b?.def?.defName == null)
                continue;
            if (b.def.defName.IndexOf("Monolith", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (b.Position.DistanceTo(pawn.Position) > 6f)
                continue;
            CompPowerTrader p = b.GetComp<CompPowerTrader>();
            if (p == null || p.PowerOn)
                return true;
        }
        return false;
    }
}
