using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class Gene_AddHediff : Gene
    {
        public GeneExtension_Necron modExtension => def.GetModExtension<GeneExtension_Necron>();

        public override void PostAdd()
        {
            base.PostAdd();
            if (modExtension.hediffDef != null)
            {
                Hediff hediff = pawn.health.GetOrAddHediff(modExtension.hediffDef,pawn.health.hediffSet.GetBodyPartRecord(modExtension.part));
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
