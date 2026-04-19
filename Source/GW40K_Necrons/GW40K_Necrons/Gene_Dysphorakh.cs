using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Adds (and removes) the <c>GW40K_Dysphorakh</c> hediff that drives all episode mechanics.
/// The gene also grants the <c>GW40K_Dysphorakh</c> trait via <c>forcedTraits</c> in the def XML.
/// </summary>
public class Gene_Dysphorakh : Gene
{
    public override void PostAdd()
    {
        base.PostAdd();
        if (!pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_Dysphorakh))
            pawn.health.AddHediff(NecronDefOfs.GW40K_Dysphorakh);
    }

    public override void PostRemove()
    {
        base.PostRemove();
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Dysphorakh);
        if (hediff != null)
            pawn.health.RemoveHediff(hediff);
    }
}
