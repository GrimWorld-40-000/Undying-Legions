using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class Gene_NoInspirationAndPassion : Gene
    {
        public GeneExtension_Necron modExtension => def.GetModExtension<GeneExtension_Necron>();
        public override void PostAdd()
        {
            base.PostAdd();
            foreach (var item in pawn.skills.skills)
            {
                if (item.passion > 0)
                {
                    item.passion = 0;
                }
            }
            if (modExtension.hediffDef != null)
            {
                Hediff hediff = pawn.health.GetOrAddHediff(modExtension.hediffDef, pawn.health.hediffSet.GetBodyPartRecord(modExtension.part));
                hediff.Severity = modExtension.severityOnAdd;
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (modExtension.hediffDef != null)
            {
                Hediff hediff = pawn.health.GetOrAddHediff(modExtension.hediffDef);
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
