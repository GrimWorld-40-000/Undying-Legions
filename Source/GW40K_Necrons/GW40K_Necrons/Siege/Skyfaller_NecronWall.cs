using System.Linq;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// A skyfaller that carries no inner pawn/item cargo.
/// Stores the building def to place on landing so the wall "arrives" from a drop pod
/// without any InvalidCastException from cargo type checks during descent.
/// </summary>
public class Skyfaller_NecronWall : Skyfaller
{
    public ThingDef buildingDef;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref buildingDef, "buildingDef");
    }

    protected override void Impact()
    {
        // Capture before base.Impact() — that call destroys the skyfaller,
        // making Position and Map invalid for any code that runs after it.
        IntVec3 landingCell = Position;
        Map     landingMap  = Map;

        base.Impact();

        if (buildingDef == null) return;
        if (!landingCell.InBounds(landingMap)) return;
        if (landingMap.thingGrid.ThingAt(landingCell, buildingDef) != null) return;

        // Clear any small items that would block the building.
        foreach (Thing t in landingMap.thingGrid.ThingsAt(landingCell)
                     .Where(t => t.def.category == ThingCategory.Item).ToList())
            t.DeSpawn();

        Thing building = ThingMaker.MakeThing(buildingDef, GenStuff.DefaultStuffFor(buildingDef));
        GenSpawn.Spawn(building, landingCell, landingMap);
    }
}
