using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class Need_Necrodermis : Need
    {
        public Need_Necrodermis(Pawn pawn) : base(pawn)
        {
        }
        public NeedExtension_Necron modExtension => def.GetModExtension<NeedExtension_Necron>();
        public NecrodermisHungerCategory CurCategory
        {
            get
            {
                if (base.CurLevelPercentage <= 0f)
                {
                    return NecrodermisHungerCategory.Starving;
                }
                if (base.CurLevelPercentage < base.MaxLevel * 0.4f)
                {
                    return NecrodermisHungerCategory.UrgentlyHungry;
                }
                if (base.CurLevelPercentage < base.MaxLevel * 0.8f)
                {
                    return NecrodermisHungerCategory.Hungry;
                }
                return NecrodermisHungerCategory.Full;
            }
        }
        public bool Starving => CurCategory == NecrodermisHungerCategory.Starving;

        public int lastNonStarvingTick = -99999;

        public float FallPerTick => def.fallPerDay / 60000f;
        protected override bool IsFrozen
        {
            get
            {
                if (!base.IsFrozen && !pawn.Deathresting)
                {
                    return false;
                }
                return true;
            }
        }
        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                CurLevel -= FallPerTick * 150f;
            }
            if (!Starving)
            {
                lastNonStarvingTick = Find.TickManager.TicksGame;
            }
            if (!IsFrozen || pawn.Deathresting)
            {
                if (Starving)
                {
                    HealthUtility.AdjustSeverity(pawn, modExtension.starvingHediffDef, modExtension.severityPerInterval);
                }
                else
                {
                    HealthUtility.AdjustSeverity(pawn, modExtension.starvingHediffDef, 0f - modExtension.severityPerInterval);
                }
            }
        }
    }
}
