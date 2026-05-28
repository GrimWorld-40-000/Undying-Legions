using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Necron siege strategy. Always guarantees a Canoptek Spyder and six bodyguard scarabs
/// in the pawn group regardless of which Combat group the random picker chose.
/// </summary>
public class RaidStrategyWorker_NecronSiege : RaidStrategyWorker
{
    internal const string SpyderKindDef = "UD_Necron_CanoptekSpyder";
    internal const string ScarabKindDef = "GW_UL_ScarabSwarm";
    internal const int    BodyguardScarabCount = 6;

    public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        // Skip base.CanUseWith — the base checks canSiege which we intentionally don't set.
        if (parms?.faction?.def == null) return false;
        if (parms.faction.def.defName != "UD_NecronFaction") return false;
        if (this.def?.arriveModes == null || this.def.arriveModes.Count == 0) return false;

        // Necron siege protocols are calibrated to the colony's tech posture and the
        // storyteller's threat pressure:
        //   Blood and Dust+ (threatScale >= 1.5) → Fabrication suffices
        //   Strive to Survive and below           → AdvancedFabrication required
        float threatScale = Find.Storyteller?.difficulty?.threatScale ?? 1f;
        string requiredResearch = threatScale >= 1.5f ? "Fabrication" : "AdvancedFabrication";
        ResearchProjectDef required = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(requiredResearch);
        if (required != null && !required.IsFinished) return false;

        // At least one Combat group must have a Spyder option.
        return parms.faction.def.pawnGroupMakers?.Any(gm =>
            gm.kindDef == groupKind &&
            gm.options.Any(o => o.kind?.defName == SpyderKindDef)) ?? false;
    }

    protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        // Guarantee the Spyder and its bodyguard scarabs are in the group.
        // pawns is a List<T> (reference type) — additions here are reflected in spawning.
        EnsureSiegeComposition(pawns, parms.faction);

        IntVec3 siegeCenter = ChooseSiegeCenter(map, pawns);
        return new LordJob_NecronSiege(siegeCenter, parms.points);
    }

    /// <summary>
    /// Ensures the pawn list contains exactly one Spyder and at least
    /// <see cref="BodyguardScarabCount"/> scarabs, adding any that are missing.
    /// </summary>
    internal static void EnsureSiegeComposition(List<Pawn> pawns, Faction faction)
    {
        // Guarantee 1 Spyder.
        if (!pawns.Any(p => p.kindDef?.defName == SpyderKindDef))
        {
            PawnKindDef spyderDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(SpyderKindDef);
            if (spyderDef != null)
                pawns.Add(MakePawn(spyderDef, faction));
        }

        // Guarantee bodyguard scarab count.
        int existing = pawns.Count(p => p.kindDef?.defName == ScarabKindDef);
        PawnKindDef scarabDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(ScarabKindDef);
        for (int i = existing; i < BodyguardScarabCount && scarabDef != null; i++)
            pawns.Add(MakePawn(scarabDef, faction));
    }

    internal static Pawn MakePawn(PawnKindDef kind, Faction faction) =>
        PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            kind, faction, PawnGenerationContext.NonPlayer,
            forceGenerateNewPawn: true, canGeneratePawnRelations: false,
            colonistRelationChanceFactor: 0f));

    internal static IntVec3 ChooseSiegeCenter(Map map, List<Pawn> pawns)
    {
        // Find an approach vector: map-edge entry cell → colony center.
        // Pawns are unspawned here so we use the map entry system, not pawn positions.
        if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 edgeCell, map, 0.5f))
            edgeCell = CellFinder.RandomEdgeCell(map);

        IntVec3 colonyCenter = FindColonyCenter(map);

        // Anchor the siege camp near the MAP EDGE (12–22 cells inward).
        // Colony-center-relative standoffs caused sieges to land adjacent to outer walls
        // when the colony was dense or terrain limited valid cells. Edge-anchoring gives
        // a guaranteed long standoff regardless of colony size or map layout.
        Vector3 toColony  = (colonyCenter - edgeCell).ToVector3Shifted().normalized;
        float   inward    = Rand.Range(12f, 22f);
        IntVec3 candidate = (edgeCell.ToVector3Shifted() + toColony * inward).ToIntVec3()
                             .ClampInsideMap(map);

        // Collect Void Monolith positions once (Anomaly DLC; null-safe if not installed).
        // Siege must never land near it — GetNamedSilentFail returns null without Anomaly.
        var voidMonolithPositions = new System.Collections.Generic.List<IntVec3>();
        ThingDef voidMonolithDef = DefDatabase<ThingDef>.GetNamedSilentFail("VoidMonolith");
        if (voidMonolithDef != null)
            foreach (Thing t in map.listerThings.ThingsOfDef(voidMonolithDef))
                if (t.Spawned) voidMonolithPositions.Add(t.Position);

        const float MonolithExclusionSq = 20f * 20f; // 20-tile exclusion radius

        // Basic validity: standable, unfogged, unroofed, not on map edge, not near Void Monolith.
        // The 8-tile border buffer keeps the center far enough from the edge that all
        // siege walls (halfSize=6) plus a 2-tile buildable-area margin fit inside the map.
        const int EdgeBuffer = 8;
        bool IsValidSiegeCell(IntVec3 c)
        {
            if (!c.Standable(map) || c.Fogged(map) || map.roofGrid.Roofed(c)) return false;
            if (c.x < EdgeBuffer || c.z < EdgeBuffer ||
                c.x >= map.Size.x - EdgeBuffer || c.z >= map.Size.z - EdgeBuffer) return false;
            foreach (IntVec3 mp in voidMonolithPositions)
                if ((c - mp).LengthHorizontalSquared <= MonolithExclusionSq) return false;
            return true;
        }

        if (!IsValidSiegeCell(candidate))
            CellFinder.TryFindRandomCellNear(candidate, map, 24, IsValidSiegeCell, out candidate);

        // Try to find a spot with no colony structures within clearRadius.
        // Shrink clearRadius once if 30 fails, then give up the check entirely.
        if (!TryFindClearOfColony(map, candidate, clearRadius: 30, out IntVec3 clear, IsValidSiegeCell)
         && !TryFindClearOfColony(map, candidate, clearRadius: 15, out clear, IsValidSiegeCell))
            return candidate; // fallback: use best-effort candidate

        return clear;
    }

    /// <summary>
    /// Searches within <paramref name="searchRadius"/> of <paramref name="near"/> for a cell
    /// that passes <paramref name="baseValid"/> AND has no player-faction buildings within
    /// <paramref name="clearRadius"/> tiles. Returns false if none found.
    /// </summary>
    private static bool TryFindClearOfColony(
        Map map, IntVec3 near, float clearRadius,
        out IntVec3 result, System.Func<IntVec3, bool> baseValid)
    {
        float clearRadiusSq = clearRadius * clearRadius;

        bool IsClearOfStructures(IntVec3 c)
        {
            if (!baseValid(c)) return false;
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                if (!b.Spawned || b.Destroyed) continue;
                if ((b.Position - c).LengthHorizontalSquared <= clearRadiusSq)
                    return false;
            }
            return true;
        }

        return CellFinder.TryFindRandomCellNear(near, map, 30, IsClearOfStructures, out result);
    }

    internal static IntVec3 FindColonyCenter(Map map)
    {
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
        if (colonists.Count > 0)
            return new IntVec3(
                (int)colonists.Average(p => p.Position.x),
                0,
                (int)colonists.Average(p => p.Position.z));

        List<IntVec3> home = map.areaManager.Home?.ActiveCells.ToList();
        if (home?.Count > 0)
            return new IntVec3((int)home.Average(c => c.x), 0, (int)home.Average(c => c.z));

        return map.Center;
    }
}
