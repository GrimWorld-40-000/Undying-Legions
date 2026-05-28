using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Prevents stripping of Necron pawns unless they are Lychguard, Overlord, or Cryptek.
/// Detected by race ThingDef (covers shared GW40K_NecronWarrior base) + kindDef allow-list.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.AnythingToStrip))]
internal static class HarmonyPatch_NecronStrip
{
    // All ThingDef (race) defNames that identify a pawn as Necron.
    // GW40K_NecronWarrior is shared by Warriors, Overlords, Lychguard, Deathmarks, etc.
    private static readonly HashSet<string> NecronRaces = new()
    {
        "GW40K_NecronWarrior",
        "GW40K_NecronImmortal",
        "GW40K_NecronCryptek",
        "GW40K_NecronCryptothrall",
        "GW40K_NecronFlayedOne",
        "GW40K_NecronDeathmark",
        "GW40K_NecronLychGuard",
        "GW40K_NecronOverlord",
        "GW40K_ScarabSwarm",
        "UD_Necron_CanoptekSpyder",
    };

    // Only these kindDefs are allowed to be stripped.
    private static readonly HashSet<string> AllowedKinds = new()
    {
        "UD_NecronLychguard",
        "UD_NecronLychguard_2",
        "UD_NecronOverlord",
        "UD_NecronCryptek",
    };

    [HarmonyPostfix]
    internal static void Postfix(Pawn __instance, ref bool __result)
    {
        if (!__result) return;
        if (!NecronRaces.Contains(__instance.def?.defName)) return;
        if (AllowedKinds.Contains(__instance.kindDef?.defName)) return;

        __result = false;
    }
}
