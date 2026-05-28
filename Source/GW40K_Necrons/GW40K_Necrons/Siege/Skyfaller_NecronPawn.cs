using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Custom skyfaller for Necron siege raiders. Holds a Pawn reference directly instead
/// of wrapping in ActiveDropPod, avoiding the InvalidCastException that occurs when
/// DropPodIncomingMechanoidRapid.Impact() casts inner content to ActiveDropPod.
/// </summary>
public class Skyfaller_NecronPawn : Skyfaller
{
    public Pawn pawnToSpawn;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref pawnToSpawn, "pawnToSpawn");
    }

    protected override void Impact()
    {
        // Capture before base.Impact() destroys the skyfaller.
        IntVec3 landingCell = Position;
        Map     landingMap  = Map;
        Pawn    pawn        = pawnToSpawn;

        base.Impact();

        if (pawn == null || pawn.Spawned || pawn.Destroyed) return;
        if (!landingCell.InBounds(landingMap)) return;

        IntVec3 spawnCell = landingCell;
        if (!spawnCell.Standable(landingMap))
            CellFinder.TryFindRandomCellNear(landingCell, landingMap, 4,
                c => c.Standable(landingMap), out spawnCell);

        GenSpawn.Spawn(pawn, spawnCell, landingMap, Rot4.Random);
    }
}
