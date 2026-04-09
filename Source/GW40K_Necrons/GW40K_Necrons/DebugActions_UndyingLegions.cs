using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Dev-mode helpers for Undying Legions testing (category: Undying Legions).
/// </summary>
public static class DebugActions_UndyingLegions
{
    private static IEnumerable<PawnKindDef> UdTestPawnKinds()
    {
        return DefDatabase<PawnKindDef>.AllDefsListForReading
            .Where(pk =>
                pk.defName.StartsWith("UD_")
                && pk.defName.IndexOf("Unused", System.StringComparison.OrdinalIgnoreCase) < 0
                && !pk.defName.StartsWith("UD_Unused", System.StringComparison.OrdinalIgnoreCase)
                && pk.race != null);
    }

    /// <summary>
    /// Use <see cref="DebugActionType.Action"/> — RimWorld 1.6's ToolMap delegate signature does not match
    /// <c>void Method(IntVec3)</c> here and breaks the Actions debug menu.
    /// </summary>
    [DebugAction("Undying Legions", "Spawn all UD_ kinds + join player (at mouse cell)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void SpawnAllUdPawns()
    {
        Map map = Find.CurrentMap;
        if (map == null)
            return;

        IntVec3 cell = UI.MouseCell();
        Faction player = Faction.OfPlayer;
        int tile = map.Tile;
        List<PawnKindDef> kinds = UdTestPawnKinds().OrderBy(pk => pk.defName).ToList();
        int i = 0;
        foreach (PawnKindDef kind in kinds)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(kind, player, tile);
            IntVec3 offset = new IntVec3((i % 10) - 4, 0, i / 10);
            IntVec3 tryCell = cell + offset;
            if (!tryCell.InBounds(map))
                tryCell = cell;

            IntVec3 spawn = tryCell;
            if (!spawn.Walkable(map) && !CellFinder.TryFindRandomCellNear(tryCell, map, 14, c => c.Walkable(map) && !c.Fogged(map), out spawn))
                spawn = CellFinder.RandomClosewalkCellNear(tryCell, map, 12);

            GenSpawn.Spawn(pawn, spawn, map, Rot4.Random);
            JoinPlayerColony(pawn);
            i++;
        }

        Messages.Message($"Spawned {i} UD_ pawn kinds near {cell}.", MessageTypeDefOf.TaskCompletion, false);
    }

    private static void JoinPlayerColony(Pawn pawn)
    {
        if (pawn == null || pawn.Dead)
            return;

        if (pawn.Faction != Faction.OfPlayer)
            pawn.SetFaction(Faction.OfPlayer);
        pawn.guest?.SetGuestStatus(null);
    }
}
