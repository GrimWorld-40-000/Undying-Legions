using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class Gene_RemoveAllApparelOnAdd : Gene
    {
        public GeneExtension_Necron modExtension => def.GetModExtension<GeneExtension_Necron>();
        public override void PostAdd()
        {
            base.PostAdd();
            if (pawn.apparel.WornApparelCount > 0)
            {
                foreach (var item in pawn.apparel.WornApparel.ToList())
                {
                    if (!modExtension.allowedApparels.Contains(item.def))
                    {
                        pawn.apparel.TryDrop(item);
                    }
                }
            }
        }
    }
}
