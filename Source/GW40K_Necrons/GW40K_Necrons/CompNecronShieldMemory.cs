using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Attached to all Necron humanlike pawns.
/// When the pawn is downed and drops their necron shield, this comp stores
/// the reference and re-issues a Wear job once the pawn stands back up.
/// </summary>
public class CompProperties_NecronShieldMemory : CompProperties
{
    public CompProperties_NecronShieldMemory() => compClass = typeof(CompNecronShieldMemory);
}

public class CompNecronShieldMemory : ThingComp
{
    public Thing droppedShield;

    private Pawn Pawn => parent as Pawn;

    public override void CompTickRare()
    {
        if (droppedShield == null) return;

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.Downed) return;

        // Already re-equipped
        if (pawn.apparel?.WornApparel.Any(a => a.def.defName == "GM40k_Necron_Shield") == true)
        {
            droppedShield = null;
            return;
        }

        // Shield was destroyed or hauled away
        if (!droppedShield.Spawned)
        {
            droppedShield = null;
            return;
        }

        // Only queue for player-controlled pawns — enemies don't need to reclaim
        if (pawn.Faction?.IsPlayer != true) { droppedShield = null; return; }

        // Don't interrupt an ongoing job of the same type
        if (pawn.CurJobDef == JobDefOf.Wear) return;

        Job job = JobMaker.MakeJob(JobDefOf.Wear, droppedShield);
        pawn.jobs.TryTakeOrderedJob(job, requestQueueing: false);
        droppedShield = null;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref droppedShield, "necronDroppedShield");
    }
}
