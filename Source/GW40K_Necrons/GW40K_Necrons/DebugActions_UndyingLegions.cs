using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Dev-mode helpers for Undying Legions testing (category: Xeno).
/// Individual pawn kinds appear in the Xeno dev spawn menu automatically via
/// <see cref="HarmonyPatch_DebugToolsSpawning_XenoCategory"/> + <see cref="UdXenoDevTools"/>.
/// </summary>
public static class DebugActions_UndyingLegions
{
    /// <summary>
    /// Ordered list for bulk dev spawn — humanlike UD kinds, xeno Flayed colonist, active Nechs, scarab.
    /// Not the same as <see cref="UdXenoDevTools.IsUdXenoPawnKind"/> (that only matches races with Necron extensions).
    /// </summary>
    private static readonly string[] BulkTestPawnKindDefNames =
    [
        "UD_NecronCryptek",
        "UD_NecronOverlord",
        "UD_NecronLychguard",
        "UD_NecronLychguard_2",
        "UD_NecronDeathmark",
        "GW_UL_NecronFlayedOnePawnKind_Colonist",
        "GW_UL_NecronWarriorPawnKind_Colonist",
        "GW_UL_NecronImmortalPawnKind_Colonist",
        "GW_UL_CryptothrallPawnKind_Colonist",
        // "UD_Necron_Warrior",
        // "UD_Necron_Cryptothrall",
        // "GW_UD_Cryptothrall",
        // "UD_Necron_Immortal",
        // "UD_NecronImmortal",
        "UD_Necron_CanoptekSpyder",
        "GW_UL_ScarabSwarm",
        "GW_UL_ScarabSwarm",
        "GW_UL_ScarabSwarm",
        "GW_UL_ScarabSwarm",
        "GW_UL_ScarabSwarm",
        "GW_UL_ScarabSwarm",
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
    [DebugAction("Xeno", "Spawn all UD pawn kinds + join player (at mouse cell)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void SpawnAllUdPawns()
    {
        Map map = Find.CurrentMap;
        if (map == null)
            return;

        IntVec3 cell = UI.MouseCell();
        Faction player = Faction.OfPlayer;
        int tile = map.Tile;
        int i = 0;
        foreach (PawnKindDef kind in BulkTestPawnKinds())
        {
            Pawn pawn = PawnGenerator.GeneratePawn(kind, player, tile);
            IntVec3 offset = new((i % 10) - 4, 0, i / 10);
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

    /// <summary>
    /// Next time silhouettes are drawn, logs heuristics for <see cref="DynamicDrawManager.DrawThings"/> (missing texPath, Graphic throws).
    /// Zoom out first if the error only happens at max zoom.
    /// </summary>
    [DebugAction("Xeno", "Diagnose silhouette / dynamic draw list (next silhouette draw)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DiagnoseSilhouetteDrawList()
    {
        DynamicDrawSilhouetteDiagnostics.PendingScan = true;
        Messages.Message(
            "Undying Legions: will scan DynamicDrawManager.DrawThings on the next silhouette draw. Zoom out if needed, then check the log.",
            MessageTypeDefOf.CautionInput,
            false);
    }

    /// <summary>
    /// Forces a Necron siege at the mouse cell with guaranteed composition.
    /// Spyder + bodyguard scarabs drop via mechanoid drop pods at the siege center;
    /// perimeter warriors drop at the map edge — same visual as mech raids.
    /// </summary>
    [DebugAction("Xeno", "Force Necron siege at mouse", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ForceNecronSiege()
    {
        Map     map         = Find.CurrentMap;
        IntVec3 siegeCenter = UI.MouseCell();
        if (map == null) return;

        FactionDef facDef  = DefDatabase<FactionDef>.GetNamedSilentFail("UD_NecronFaction");
        Faction    faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.def == facDef);
        if (faction == null)
        {
            Messages.Message("No UD_NecronFaction on the world map.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Build guaranteed siege composition (pawns NOT yet spawned).
        List<Pawn> pawns = new List<Pawn>();
        RaidStrategyWorker_NecronSiege.EnsureSiegeComposition(pawns, faction);

        PawnKindDef warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("GW_UL_NecronWarriorPawnKind_Colonist");
        for (int i = 0; i < 6 && warrior != null; i++)
            pawns.Add(RaidStrategyWorker_NecronSiege.MakePawn(warrior, faction));

        // Register all pawns with the lord BEFORE spawning — this is how vanilla raids work.
        // Pawns remain associated with the lord even while inside a drop pod.
        Lord lord = LordMaker.MakeNewLord(faction, new LordJob_NecronSiege(siegeCenter), map, pawns);

        // Spyder + bodyguard scarabs drop at the siege center.
        var centerGroup = pawns
            .Where(p => p.kindDef?.defName == RaidStrategyWorker_NecronSiege.SpyderKindDef
                     || p.kindDef?.defName == RaidStrategyWorker_NecronSiege.ScarabKindDef)
            .Select(p => new List<Thing> { p })
            .ToList();
        DropPodUtility.DropThingGroupsNear(siegeCenter, map, centerGroup, 80, false, false, false);

        // Perimeter warriors drop at the map edge and walk to the staging area.
        if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, 0.5f))
            entry = CellFinder.RandomEdgeCell(map);

        var edgeGroup = pawns
            .Where(p => p.kindDef?.defName != RaidStrategyWorker_NecronSiege.SpyderKindDef
                     && p.kindDef?.defName != RaidStrategyWorker_NecronSiege.ScarabKindDef)
            .Select(p => new List<Thing> { p })
            .ToList();
        DropPodUtility.DropThingGroupsNear(entry, map, edgeGroup, 80, false, false, false);

        Messages.Message($"Necron siege incoming at {siegeCenter} — {pawns.Count} pawns in drop pods.", MessageTypeDefOf.TaskCompletion, false);
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
