using System.Linq;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Gene class for the Deathmark Oculus head gene.
/// Adds (and removes) the hidden GW_UD_DeathmarkOculus hediff anchored to the head,
/// which provides the green eye-glow and night-vision shooting accuracy.
/// </summary>
public class Gene_DeathmarkOculus : Gene
{
    public override void PostAdd()
    {
        base.PostAdd();
        if (pawn.health.hediffSet.HasHediff(NecronDefOfs.GW_UD_DeathmarkOculus))
            return;
        BodyPartRecord head = pawn.RaceProps.body.AllParts
            .FirstOrDefault(p => p.def == BodyPartDefOf.Head);
        pawn.health.AddHediff(NecronDefOfs.GW_UD_DeathmarkOculus, head);
    }

    public override void PostRemove()
    {
        base.PostRemove();
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW_UD_DeathmarkOculus);
        if (hediff != null)
            pawn.health.RemoveHediff(hediff);
    }
}
