using System.Collections.Generic;
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
    /// <summary>
    /// Ordered list for bulk dev spawn — humanlike UD kinds, xeno Flayed colonist, all playable Nechs, scarab.
    /// Not the same as <see cref="UdXenoDevTools.IsUdXenoPawnKind"/> (that only matches races with <c>NecronMechExtension</c>).
    /// </summary>
    private static readonly string[] BulkTestPawnKindDefNames =
    [
        "UD_NecronCryptek",
        "UD_NecronOverlord",
        "UD_NecronLychguard",
        "UD_NecronLychguard_2",
        "UD_NecronDeathmark",
        "GW40K_NecronFlayedOnePawnKind_Colonist",
        "UD_Necron_Warrior",
        "UD_Necron_Cryptothrall",
        "UD_Necron_Immortal",
        "GW40K_ScarabSwarm",
    ];

    private static IEnumerable<PawnKindDef> BulkTestPawnKinds()
    {
        foreach (string defName in BulkTestPawnKindDefNames)
        {
            PawnKindDef def = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            if (def != null)
                yield return def;
        }
    }

    /// <summary>
    /// Use <see cref="DebugActionType.Action"/> — RimWorld 1.6's ToolMap delegate signature does not match
    /// <c>void Method(IntVec3)</c> here and breaks the Actions debug menu.
    /// </summary>
    [DebugAction("Undying Legions", "Spawn all test pawn kinds + join player (at mouse cell)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void SpawnAllUdPawns()
    {
        Map map = Find.CurrentMap;
        if (map == null)
            return;

        IntVec3 cell = UI.MouseCell();
        Faction player = Faction.OfPlayer;
        int tile = map.Tile;
        List<PawnKindDef> kinds = new List<PawnKindDef>(BulkTestPawnKinds());
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

        Messages.Message($"Spawned {i} Undying Legions test pawn kinds near {cell}.", MessageTypeDefOf.TaskCompletion, false);
    }

    private static void JoinPlayerColony(Pawn pawn)
    {
        if (pawn == null || pawn.Dead)
            return;

        if (pawn.Faction != Faction.OfPlayer)
            pawn.SetFaction(Faction.OfPlayer);
        pawn.guest?.SetGuestStatus(null);
    }

    /// <summary>
    /// Next time silhouettes are drawn, logs heuristics for <see cref="DynamicDrawManager.DrawThings"/> (missing texPath, Graphic throws).
    /// Zoom out first if the error only happens at max zoom.
    /// </summary>
    [DebugAction("Undying Legions", "Diagnose silhouette / dynamic draw list (next silhouette draw)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnoseSilhouetteDrawList()
    {
        DynamicDrawSilhouetteDiagnostics.PendingScan = true;
        Messages.Message(
            "Undying Legions: will scan DynamicDrawManager.DrawThings on the next silhouette draw. Zoom out if needed, then check the log.",
            MessageTypeDefOf.CautionInput,
            false);
    }
}
