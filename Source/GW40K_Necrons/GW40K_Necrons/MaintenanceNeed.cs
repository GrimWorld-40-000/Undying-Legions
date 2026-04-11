using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Core flux: Necron analogue of Biotech <see cref="Need_Deathrest"/>.
/// Drains while the pawn is active; refills during vanilla deathrest or while held in a cryptosleep casket (stasis crypt).
/// Rates match <see cref="Need_Deathrest"/> (1/30 max per day drain, 0.2/day gain while replenishing).
/// </summary>
public class MaintenanceNeed : Need
{
    public const float FallPerDay = 1f / 30f;
    public const float GainPerDayReplenishing = 0.2f;

    /// <summary>Need ticks every 150 game ticks; same 400 divisor as <see cref="Need_Deathrest.NeedInterval"/>.</summary>
    private const float IntervalsPerDay = 400f;

    /// <summary>
    /// Extra core flux loss per need interval, expressed as a multiple of (FallPerDay/IntervalsPerDay), while
    /// <see cref="NecronDefOfs.GW_UD_NecrodermisMaintenanceDeficit"/> is present. Tiers follow hediff stages (light→critical).
    /// </summary>
    private const float BodyDegradLightExtraFallMul = 3f;

    private const float BodyDegradModerateExtraFallMul = 8f;

    private const float BodyDegradSevereExtraFallMul = 16f;

    private const float BodyDegradCriticalExtraFallMul = 30f;

    public const float ThreshTired = 0.28f;
    public const float ThreshVeryTired = 0.14f;

    /// <summary>Matches vanilla deathrest alert band (10%).</summary>
    public const float LevelForCriticalAlert = 0.1f;

    public RestCategory CurCategory
    {
        get
        {
            if (CurLevel < 0.01f)
                return RestCategory.Exhausted;
            if (CurLevel < ThreshVeryTired)
                return RestCategory.VeryTired;
            if (CurLevel < ThreshTired)
                return RestCategory.Tired;
            return RestCategory.Rested;
        }
    }

    public MaintenanceNeed(Pawn pawn)
        : base(pawn)
    {
        threshPercents = new List<float> { ThreshTired, ThreshVeryTired };
    }

    public override int GUIChangeArrow
    {
        get
        {
            if (IsFrozen)
                return 0;
            return CoreFluxReplenishing() ? 1 : -1;
        }
    }

    public override void SetInitialLevel()
    {
        CurLevel = Rand.Range(0.9f, 1f);
    }

    protected override bool IsFrozen
    {
        get
        {
            if (CoreFluxReplenishing())
                return false;
            return base.IsFrozen;
        }
    }

    public override void NeedInterval()
    {
        if (IsFrozen)
            return;

        float eff = DeathrestReplenishmentEfficiency(pawn);
        float coreMul = NechEnergyUtility.CoreFluxCapacityMultiplier(pawn);
        if (coreMul <= 0f)
            coreMul = 1f;
        float delta = CoreFluxReplenishing()
            ? (GainPerDayReplenishing * eff / IntervalsPerDay) / coreMul
            : (-FallPerDay / IntervalsPerDay) / coreMul;

        CurLevel += delta;

        if (!CoreFluxReplenishing())
            CurLevel -= BodyDegradationExtraCoreFluxFallPerInterval() / coreMul;

        CheckForCoreFluxState();
    }

    public override string GetTipString()
    {
        string tip = base.GetTipString();
        if (CoreFluxReplenishing())
            return tip;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(tip);

        float extraPerDay = BodyDegradationExtraCoreFluxFallPerDay();
        if (extraPerDay > 0f)
            sb.Append("\n").Append("GW40K_CoreFluxBodyDegradationOffset"
                .Translate(extraPerDay.ToStringPercent())
                .Resolve());

        HediffComp_GaussCapacitor cap = NechEnergyUtility.GetCapacitorComp(pawn);
        if (cap != null && cap.allowCoreCharge)
        {
            float drainPerDay = Need_NechEnergy.CoreChargeGainPerDay * cap.Props.coreFluxCostFull;
            sb.Append("\n").Append("GW40K_CoreFluxCoreChargeDrain"
                .Translate(drainPerDay.ToStringPercent())
                .Resolve());
        }

        return sb.ToString();
    }

    /// <summary>Vanilla-style: at zero flux apply exhaustion + forced Eternal Slumber; clear when flux returns or while replenishing.</summary>
    private void CheckForCoreFluxState()
    {
        if (pawn.health?.hediffSet == null)
            return;
        if (NecronDefOfs.GW40K_CoreFluxExhaustion == null || NecronDefOfs.GW40K_EternalSlumberForced == null)
            return;

        bool depleted = CurLevel <= 0f;
        bool recovering = CoreFluxReplenishing();

        if (!depleted || recovering)
        {
            RemoveZeroFluxHediffs();
            return;
        }

        if (pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_CoreFluxExhaustion) == null)
            pawn.health.AddHediff(NecronDefOfs.GW40K_CoreFluxExhaustion);
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_EternalSlumberForced) == null)
            pawn.health.AddHediff(NecronDefOfs.GW40K_EternalSlumberForced);
    }

    private void RemoveZeroFluxHediffs()
    {
        RemoveHediffIfAny(NecronDefOfs.GW40K_CoreFluxExhaustion);
        RemoveHediffIfAny(NecronDefOfs.GW40K_EternalSlumberForced);
    }

    private void RemoveHediffIfAny(HediffDef def)
    {
        if (def == null)
            return;
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (h == null)
            return;
        if (def == NecronDefOfs.GW40K_EternalSlumberForced)
            h.TryGetComp<HediffComp_EternalSlumberInterruption>()?.MarkCleanRemovalFromNeed();
        pawn.health.RemoveHediff(h);
    }

    /// <summary>
    /// Additional core flux drain from body degradation while necrodermis is depleted (all visible stages).
    /// Does not apply while core flux is replenishing (deathrest or cryptosleep casket).
    /// </summary>
    private float BodyDegradationExtraCoreFluxFallPerInterval()
    {
        return BodyDegradationExtraCoreFluxFallPerDay() / IntervalsPerDay;
    }

    private float BodyDegradationExtraCoreFluxFallPerDay()
    {
        if (pawn.health?.hediffSet == null || NecronDefOfs.GW_UD_NecrodermisMaintenanceDeficit == null)
            return 0f;
        Hediff bd = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW_UD_NecrodermisMaintenanceDeficit);
        if (bd == null)
            return 0f;
        float sev = bd.Severity;
        if (sev < 0.2f)
            return 0f;
        float mul = sev >= 0.8f
            ? BodyDegradCriticalExtraFallMul
            : sev >= 0.6f
                ? BodyDegradSevereExtraFallMul
                : sev >= 0.4f
                    ? BodyDegradModerateExtraFallMul
                    : BodyDegradLightExtraFallMul;
        return FallPerDay * mul;
    }

    private static float DeathrestReplenishmentEfficiency(Pawn p)
    {
        Gene_Deathrest gene = p.genes?.GetFirstGeneOfType<Gene_Deathrest>();
        return gene?.DeathrestEfficiency ?? 1f;
    }

    /// <summary>True if pawn is in deathrest or inside a cryptosleep casket (including <see cref="NecronCasket"/>).</summary>
    public bool CoreFluxReplenishing()
    {
        if (pawn.Deathresting)
            return true;
        return ThingOwnerUtility.GetAnyParent<Building_CryptosleepCasket>(pawn) != null;
    }
}
