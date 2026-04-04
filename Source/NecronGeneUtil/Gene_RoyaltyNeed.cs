using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class Gene_RoyaltyNeed : Gene
    {
        public GeneExtension_Necron modExtensions => def.GetModExtension<GeneExtension_Necron>();
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(250))
            {
                CheckNeed();
            }
        }

        public void CheckNeed()
        {
            if (modExtensions == null)
            {
                Log.Error($"GeneExtension_Necron is null for {def.LabelCap}");
                return;
            }
            if (modExtensions.needDefs.NullOrEmpty())
            {
                Log.Error($"GeneExtension_Necron is lacking <needDefs> field for {def.LabelCap}");
                return;
            }
            bool pass = false;
            if (modExtensions.isGiveBuffIfAnyPassThreshold)
            {
                foreach (var item in modExtensions.needDefs)
                {
                    Need need = pawn.needs.TryGetNeed(item);
                    if (need == null) continue;
                    if (need.CurLevelPercentage >= modExtensions.threshold)
                    {
                        pass = true;
                        break;
                    }
                }
            }
            else
            {
                pass = true;
                foreach (var item in modExtensions.needDefs)
                {
                    Need need = pawn.needs.TryGetNeed(item);
                    if (need == null) continue;
                    if (need.CurLevelPercentage < modExtensions.threshold)
                    {
                        pass = false;
                        break;
                    }
                }
            }
            if (pass)
            {
                Hediff hediff = GrantBuff();
            }
            else
            {
                RemoveHediffWhenFail();
            }
        }

        public Hediff GrantBuff()
        {
            Hediff hediff = HediffMaker.MakeHediff(modExtensions.hediffDef,pawn,pawn.health.hediffSet.GetBodyPartRecord(modExtensions.part));
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        public void RemoveHediffWhenFail()
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(modExtensions.hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
