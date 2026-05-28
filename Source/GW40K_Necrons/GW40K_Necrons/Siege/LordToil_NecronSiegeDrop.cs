using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Stage 1 of the Necron siege.
/// All raiders AND all wall/embrasure cells drop from mechanoid pods targeted at the
/// siege center. Each wall cell gets a <see cref="Skyfaller_NecronWall"/> — a custom
/// skyfaller with no inner cargo — so the pod visuals work without InvalidCastException.
/// </summary>
public class LordToil_NecronSiegeDrop : LordToil
{
    private IntVec3 center;
    private float   raidPoints;
    private bool    initialized;

    public LordToil_NecronSiegeDrop() { }
    public LordToil_NecronSiegeDrop(IntVec3 center, float raidPoints = 0f)
    {
        this.center     = center;
        this.raidPoints = raidPoints;
    }

    public override void Init()
    {
        base.Init();
        if (initialized) return;
        initialized = true;

        Map map = Map;
        if (map == null) return;

        EnsureSpyderAndScarabs(map);
        DropPawnsAtCenter(map);
        DropStructures(map);
    }

    public override void UpdateAllDuties()
    {
        if (center == IntVec3.Zero) return;
        DutyDef noNeedsDuty = DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabDefend") ?? DutyDefOf.Defend;
        foreach (Pawn p in lord.ownedPawns)
        {
            bool hasFoodNeed = p.needs?.food != null;
            p.mindState.duty = new PawnDuty(hasFoodNeed ? DutyDefOf.Defend : noNeedsDuty, center, 0f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureSpyderAndScarabs(Map map)
    {
        Faction faction = lord.faction;

        if (!lord.ownedPawns.Any(p => p.kindDef?.defName == RaidStrategyWorker_NecronSiege.SpyderKindDef))
        {
            PawnKindDef spyderDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(RaidStrategyWorker_NecronSiege.SpyderKindDef);
            if (spyderDef != null)
                lord.AddPawn(RaidStrategyWorker_NecronSiege.MakePawn(spyderDef, faction));
        }

        int have = lord.ownedPawns.Count(p => p.kindDef?.defName == RaidStrategyWorker_NecronSiege.ScarabKindDef);
        PawnKindDef scarabDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(RaidStrategyWorker_NecronSiege.ScarabKindDef);
        for (int i = have; i < RaidStrategyWorker_NecronSiege.BodyguardScarabCount && scarabDef != null; i++)
            lord.AddPawn(RaidStrategyWorker_NecronSiege.MakePawn(scarabDef, faction));
    }

    private void DropPawnsAtCenter(Map map)
    {
        // Each pawn rides in a Skyfaller_NecronPawn — mechanoid pod visuals,
        // no ActiveDropPod wrapper, no InvalidCastException.
        ThingDef pawnPodDef = DefDatabase<ThingDef>.GetNamedSilentFail("GW_UL_NecronPawnPod");
        if (pawnPodDef == null) return;

        foreach (Pawn p in lord.ownedPawns.ToList())
        {
            if (p.Spawned)
                p.DeSpawn(DestroyMode.Vanish);

            IntVec3 dropCell = CellFinder.RandomClosewalkCellNear(center, map, 3,
                c => c.Standable(map) && !c.Fogged(map));

            Skyfaller_NecronPawn pod = (Skyfaller_NecronPawn)ThingMaker.MakeThing(pawnPodDef);
            pod.pawnToSpawn = p;
            GenSpawn.Spawn(pod, dropCell, map);
        }
    }

    private void DropStructures(Map map)
    {
        ThingDef wallDef = DefDatabase<ThingDef>.GetNamedSilentFail("GW_UL_NecronWall");
        ThingDef embDef  = DefDatabase<ThingDef>.GetNamedSilentFail("GW_UL_NecronEmbrasure");
        ThingDef wallPod = DefDatabase<ThingDef>.GetNamedSilentFail("GW_UL_NecronWallPod");
        if (wallDef == null || embDef == null || wallPod == null) return;

        // Randomize size and gap each raid for layout variation.
        int halfSize   = Rand.RangeInclusive(5, 8);
        int gapHalf    = Rand.RangeInclusive(1, 2); // 3- or 5-cell gap on each closed side

        // One side is always left fully open — guaranteed no closed box.
        // 0=west(dx=-H)  1=east(dx=+H)  2=south(dz=-H)  3=north(dz=+H)
        int openSide   = Rand.RangeInclusive(0, 3);

        // Cells trimmed from each end of a closed side adjacent to the open side,
        // so corners near the opening stay clear.
        int cornerTrim = Rand.RangeInclusive(2, 3);

        for (int dx = -halfSize; dx <= halfSize; dx++)
        for (int dz = -halfSize; dz <= halfSize; dz++)
        {
            bool onWest  = dx == -halfSize;
            bool onEast  = dx ==  halfSize;
            bool onSouth = dz == -halfSize;
            bool onNorth = dz ==  halfSize;
            if (!onWest && !onEast && !onSouth && !onNorth) continue;

            // Skip the entire open side (and its corners).
            if (openSide == 0 && onWest)  continue;
            if (openSide == 1 && onEast)  continue;
            if (openSide == 2 && onSouth) continue;
            if (openSide == 3 && onNorth) continue;

            // Trim corners of the two sides adjacent to the open side so the
            // entrance doesn't narrow to a single-cell pinch point.
            if (openSide == 0 && (onSouth || onNorth) && dx <= -halfSize + cornerTrim) continue;
            if (openSide == 1 && (onSouth || onNorth) && dx >=  halfSize - cornerTrim) continue;
            if (openSide == 2 && (onWest  || onEast)  && dz <= -halfSize + cornerTrim) continue;
            if (openSide == 3 && (onWest  || onEast)  && dz >=  halfSize - cornerTrim) continue;

            // Gap at the centre of each closed side.
            bool inEWGap = (onEast || onWest) && dz >= -gapHalf && dz <= gapHalf;
            bool inNSGap = (onNorth || onSouth) && dx >= -gapHalf && dx <= gapHalf;
            if (inEWGap || inNSGap) continue;

            // Embrasures flank each gap.
            bool embrasure = ((onEast || onWest)   && System.Math.Abs(dz) == gapHalf + 1)
                          || ((onNorth || onSouth)  && System.Math.Abs(dx) == gapHalf + 1);
            ThingDef buildingDef = embrasure ? embDef : wallDef;

            IntVec3 cell = center + new IntVec3(dx, 0, dz);
            if (!cell.InBounds(map)) continue;
            if (cell.x < 2 || cell.z < 2 || cell.x >= map.Size.x - 2 || cell.z >= map.Size.z - 2) continue;
            if (map.roofGrid.Roofed(cell)) continue;
            if (map.thingGrid.ThingsAt(cell).Any(t => t is Building)) continue;
            if (map.thingGrid.ThingAt(cell, buildingDef) != null) continue;

            Skyfaller_NecronWall pod = (Skyfaller_NecronWall)ThingMaker.MakeThing(wallPod);
            pod.buildingDef = buildingDef;
            GenSpawn.Spawn(pod, cell, map);
        }

        // 50% chance to drop phase barriers inside the staging area.
        // Count scales with raid points: 1 per 5 000 pts (min 1 when any spawn).
        // Barriers project a radius-6 green shield bubble that intercepts incoming fire
        // for the Spyder and bodyguard scarabs during the bombard phase.
        DropPhaseBarriers(map, wallPod);
    }

    private void DropPhaseBarriers(Map map, ThingDef wallPod)
    {
        if (!Rand.Chance(0.5f)) return;

        ThingDef barrierDef = DefDatabase<ThingDef>.GetNamedSilentFail("GW_UL_NecronPhaseBarrier");
        if (barrierDef == null) return;

        // 1 barrier per 5 000 raid points, minimum 1, maximum 5.
        // Examples: 0-4 999 → 1 | 5 000-9 999 → 2 | 10 000-14 999 → 3 …
        int count = Mathf.Clamp(Mathf.FloorToInt(raidPoints / 5000f) + 1, 1, 5);
        for (int i = 0; i < count; i++)
        {
            // Place barriers inside the staging area, 3–5 tiles from center.
            if (!CellFinder.TryFindRandomCellNear(center, map, 5,
                    c => c.InBounds(map)
                      && c.Standable(map)
                      && !c.Roofed(map)
                      && (c.x >= 2 && c.z >= 2 && c.x < map.Size.x - 2 && c.z < map.Size.z - 2)
                      && !map.thingGrid.ThingsAt(c).Any(t => t is Building)
                      && (c - center).LengthHorizontalSquared >= 9, // at least 3 tiles from center
                    out IntVec3 barrierCell))
                continue;

            Skyfaller_NecronWall barrierPod = (Skyfaller_NecronWall)ThingMaker.MakeThing(wallPod);
            barrierPod.buildingDef = barrierDef;
            GenSpawn.Spawn(barrierPod, barrierCell, map);
        }
    }
}
