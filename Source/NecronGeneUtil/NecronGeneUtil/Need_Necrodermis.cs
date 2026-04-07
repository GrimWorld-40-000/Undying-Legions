using RimWorld;
using UnityEngine;
using Verse;

namespace NecronGeneUtil;

public class Need_Necrodermis : Need
{
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

    public override void NeedInterval()
    {
        float fallMul = MaintenanceDeficitFallMultiplier();
        if (!IsFrozen)
            CurLevel -= FallPerTick * 150f * fallMul;
        if (!Starving)
            lastNonStarvingTick = Find.TickManager.TicksGame;
        if (!IsFrozen)
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
