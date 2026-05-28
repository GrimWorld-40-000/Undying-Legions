using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Prevents Flayed Ones and Cryptothralls from being ordered to equip weapons via the
/// right-click float menu.
/// - Flayed One (GW40K_NecronFlayedOne): integrated claws, should never equip a weapon.
/// - Cryptothrall (GW40K_NecronCryptothrall): uses integrated talons, no weapon slot.
/// The AI already won't try to equip weapons on these pawns because their pawnkind has
/// isFighter=false; this patch covers the player-initiated float menu path.
/// </summary>
[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
internal static class HarmonyPatch_NecronWeaponEquip
{
    private static readonly HashSet<string> BlockedRaces = new()
    {
        "GW40K_NecronFlayedOne",
        "GW40K_NecronCryptothrall",
    };

    [HarmonyPostfix]
    internal static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
    {
        if (__result == null || __result.Count == 0) return;

        Pawn pawn = context.FirstSelectedPawn;
        if (pawn == null || !BlockedRaces.Contains(pawn.def?.defName)) return;

        List<Thing> clicked = context.ClickedThings;
        if (clicked == null) return;

        foreach (Thing thing in clicked)
        {
            if (thing?.def?.IsWeapon != true) continue;
            string equipLabel = "Equip".Translate(thing.LabelShort);
            __result.RemoveAll(opt => opt.Label == equipLabel);
        }
    }
}
