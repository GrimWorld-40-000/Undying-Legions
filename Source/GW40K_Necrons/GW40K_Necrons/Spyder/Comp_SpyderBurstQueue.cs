using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Tick-based shot queue for the Spyder's particle cannon burst.
/// AbilityComp_SpyderParticleCannon fires the first shot immediately and queues the
/// remaining shots here with their scheduled fire ticks. CompTick fires each one on time.
/// </summary>
public class CompProperties_SpyderBurstQueue : CompProperties
{
    public CompProperties_SpyderBurstQueue() => compClass = typeof(Comp_SpyderBurstQueue);
}

public class Comp_SpyderBurstQueue : ThingComp
{
    private readonly List<PendingShot> _queue = new();

    public struct PendingShot
    {
        public int      fireTick;
        public IntVec3  aimCell;
        public ThingDef projDef;
    }

    public void Enqueue(int fireTick, IntVec3 aimCell, ThingDef projDef)
    {
        _queue.Add(new PendingShot { fireTick = fireTick, aimCell = aimCell, projDef = projDef });
    }

    public override void CompTick()
    {
        if (_queue.Count == 0) return;
        if (parent is not Pawn pawn || pawn.Dead || !pawn.Spawned) return;
        Map map = pawn.Map;
        if (map == null) return;

        int now = Find.TickManager.TicksGame;
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            PendingShot shot = _queue[i];
            if (now < shot.fireTick) continue;

            IntVec3 aimCell = shot.aimCell.InBounds(map) ? shot.aimCell : pawn.Position;
            Projectile proj = (Projectile)GenSpawn.Spawn(shot.projDef, pawn.Position, map);
            proj.Launch(pawn, pawn.DrawPos,
                new LocalTargetInfo(aimCell),
                new LocalTargetInfo(aimCell),
                ProjectileHitFlags.NonTargetWorld);

            _queue.RemoveAt(i);
        }
    }
}
