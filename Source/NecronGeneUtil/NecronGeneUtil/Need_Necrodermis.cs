using RimWorld;
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
        if (!IsFrozen)
            CurLevel = CurLevel - FallPerTick * 150f;
        if (!Starving)
            lastNonStarvingTick = Find.TickManager.TicksGame;
        if (!IsFrozen)
        {
            if (Starving)
                HealthUtility.AdjustSeverity(base.pawn, modExtension.starvingHediffDef, modExtension.severityPerInterval);
            else
                HealthUtility.AdjustSeverity(base.pawn, modExtension.starvingHediffDef, -modExtension.severityPerInterval);
        }
    }
}
