using NecronGeneUtil;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Flayed chassis gene: same as <see cref="Gene_AddHediff"/> (Flayer virus) plus Dysphorakh hediff and forced trait from XML.
/// </summary>
public class Gene_FlayedOneChassis : Gene_AddHediff
{
    public override void PostAdd()
    {
        base.PostAdd();
        if (NecronDefOfs.GW40K_Dysphorakh != null && !pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_Dysphorakh))
            pawn.health.AddHediff(NecronDefOfs.GW40K_Dysphorakh);
    }

    public override void PostRemove()
    {
        Hediff dysphorakh = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Dysphorakh);
        if (dysphorakh != null)
            pawn.health.RemoveHediff(dysphorakh);
        base.PostRemove();
    }
}
