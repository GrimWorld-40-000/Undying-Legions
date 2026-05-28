using RimWorld;
using System;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class JumpAbility : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn    pawn      = parent.pawn;
        IntVec3 startCell = pawn.Position; // capture before the lambda runs
        Map     map       = pawn.Map;

        LongEventHandler.QueueLongEvent((Action)(() =>
        {
            // Land exactly at the targeted cell.
            // The original code computed target + (pawn→target backwards)*2, which landed
            // the scarab 2 cells SHORT of the target — placing it in or before walls.
            GenSpawn.Spawn(
                (Thing)PawnFlyer.MakeFlyer(
                    ThingDefOf.PawnFlyer, pawn, target.Cell,
                    EffecterDefOf.ForcedVisible, (SoundDef)null,
                    overrideStartVec: null,
                    triggeringAbility: parent,
                    target: dest),
                startCell, // spawn flyer at pawn's launch position for correct arc
                map);
        }), "necronFlyAbility", false, (Action<Exception>)null);
    }
}
