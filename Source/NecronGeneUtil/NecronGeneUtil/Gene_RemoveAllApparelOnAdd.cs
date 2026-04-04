using System.Linq;
using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class Gene_RemoveAllApparelOnAdd : Gene
{
    public GeneExtension_Necron modExtension => base.def.GetModExtension<GeneExtension_Necron>();

    public override void PostAdd()
    {
        base.PostAdd();
        if (base.pawn.apparel.WornApparelCount <= 0) return;
        foreach (Apparel item in base.pawn.apparel.WornApparel.ToList())
            if (!modExtension.allowedApparels.Contains(item.def))
                base.pawn.apparel.TryDrop(item);
    }
}
