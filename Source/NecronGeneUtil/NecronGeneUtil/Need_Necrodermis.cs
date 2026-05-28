using RimWorld;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NecronGeneUtil;

public class Need_Necrodermis : Need
{
    private const int NeedIntervalTicks = 150;

    public int lastNonStarvingTick = -99999;

    public NeedExtension_Necron modExtension => base.def.GetModExtension<NeedExtension_Necron>();

    public NecrodermisHungerCategory CurCategory
    {
        get
        {
            if (CurLevelPercentage <= 0f) return NecrodermisHungerCategory.Starving;
            if (CurLevelPercentage < MaxLevel * 0.4f) return NecrodermisHungerCategory.UrgentlyHungry;
            if (CurLevelPercentage < MaxLevel * 0.8f) return NecrodermisHungerCategory.Hungry;
            return NecrodermisHungerCategory.Full;
        }
    }

    public bool Starving => CurCategory == NecrodermisHungerCategory.Starving;
    public float FallPerTick => base.def.fallPerDay / 60000f;

    protected override bool IsFrozen => base.IsFrozen || base.pawn.Deathresting;

    public Need_Necrodermis(Pawn pawn) : base(pawn) { }

    public override int GUIChangeArrow
    {
        get
        {
            if (GainingNecrodermis())
                return 1;
            if (IsFrozen)
                return 0;
            if (EffectiveFallPerInterval() <= 0f)
                return 0;
            return -1;
        }
    }

    public override string GetTipString()
    {
        StringBuilder sb = new StringBuilder(base.GetTipString());
        if (!IsFrozen && def.fallPerDay > 0.0001f)
        {
            float fallPerDay = EffectiveFallPerDay();
            sb.Append("\n\n").Append("GW40K_NecrodermisDrainPerDay".Translate(fallPerDay.ToStringPercent()).Resolve());
        }

        return sb.ToString();
    }

    /// <summary>Base <see cref="NeedDef.fallPerDay"/> scaled by maintenance-deficit hediff.</summary>
    internal float EffectiveFallPerDay() => def.fallPerDay * MaintenanceDeficitFallMultiplier();

    public override void NeedInterval()
    {
        float fallMul = MaintenanceDeficitFallMultiplier();
        if (!IsFrozen)
            CurLevel -= FallPerTick * 150f * fallMul;
        if (!Starving)
            lastNonStarvingTick = Find.TickManager.TicksGame;
        if (!IsFrozen && !NecrodermisIngestionUtility.IsCanoptek(base.pawn))
        {
            HediffDef deficit = modExtension?.maintenanceDeficitHediffDef;
            if (deficit != null)
            {
                if (Starving)
                    HealthUtility.AdjustSeverity(base.pawn, deficit, modExtension.severityPerInterval);
                else
                    HealthUtility.AdjustSeverity(base.pawn, deficit, -modExtension.severityPerInterval);
            }
        }
    }

    /// <summary>Vanilla-style gain arrow while using a necrodermis pack or injector (<see cref="JobDriver_UseItem"/>).</summary>
    private bool GainingNecrodermis()
    {
        if (pawn?.jobs?.curDriver is not JobDriver_UseItem)
            return false;
        Job job = pawn.CurJob;
        if (job == null)
            return false;
        Thing target = job.GetTarget(TargetIndex.A).Thing;
        if (target == null)
            return false;
        CompUsable usable = target.TryGetComp<CompUsable>();
        if (usable == null || !usable.CanBeUsedBy(pawn))
            return false;
        return target.TryGetComp<CompUseEffect_NecrodermisPackConsume>() != null
            || target.TryGetComp<CompUseEffect_NecrodermisInjectorIngest>() != null;
    }

    private float EffectiveFallPerInterval() =>
        FallPerTick * NeedIntervalTicks * MaintenanceDeficitFallMultiplier();

    /// <summary>Higher necrodermis drain while body degradation hediff is present (replaces hunger rate offset).</summary>
    private float MaintenanceDeficitFallMultiplier()
    {
        NeedExtension_Necron ext = modExtension;
        if (ext?.maintenanceDeficitHediffDef == null || base.pawn?.health?.hediffSet == null)
            return 1f;
        Hediff hd = base.pawn.health.hediffSet.GetFirstHediffOfDef(ext.maintenanceDeficitHediffDef);
        if (hd == null || hd.Severity <= 0.001f)
            return 1f;
        float k = ext.extraNecrodermisFallPerSeverity;
        return 1f + k * Mathf.Clamp01(hd.Severity);
    }
}
