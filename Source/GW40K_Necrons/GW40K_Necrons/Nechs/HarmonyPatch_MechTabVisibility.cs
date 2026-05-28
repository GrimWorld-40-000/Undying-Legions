using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Forces the vanilla "Mechs" tab button to appear whenever the player owns any
/// Nech-controlled pawn (Warrior, Immortal, Cryptothrall, Flayed One, Spyder, Canoptek)
/// OR any Nechinator (pawn carrying the Command Protocol implant).
///
/// Vanilla <see cref="MainButtonWorker_ToggleMechTab.Disabled"/> only checks
/// <see cref="RaceProps.IsMechanoid"/>, which is false for all humanlike Necrons,
/// so the tab would otherwise stay hidden even when the player's colony is full of
/// Necron constructs.
///
/// <c>Visible</c> on <see cref="MainButtonWorker_ToggleMechTab"/> is defined as
/// <c>!Disabled</c>, so patching <c>Disabled</c> is sufficient.
/// </summary>
[HarmonyPatch(typeof(MainButtonWorker_ToggleMechTab),
              nameof(MainButtonWorker_ToggleMechTab.Disabled),
              MethodType.Getter)]
internal static class HarmonyPatch_MechTabVisibility
{
    [HarmonyPostfix]
    internal static void Postfix(ref bool __result)
    {
        // Tab already enabled by vanilla (e.g. player also has vanilla mechs). Nothing to do.
        if (!__result) return;

        Map map = Find.CurrentMap;
        if (map == null) return;

        foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
        {
            if (NechUtility.IsNechControlled(p) || IsNechinator(p))
            {
                __result = false;
                return;
            }
        }
    }

    /// <summary>
    /// A Nechinator is any pawn that bears the Command Protocol implant
    /// (has a live <see cref="HediffComp_NecronCommandTracker"/>).
    /// </summary>
    private static bool IsNechinator(Pawn p) =>
        HediffComp_NecronCommandTracker.GetTracker(p) != null;
}
