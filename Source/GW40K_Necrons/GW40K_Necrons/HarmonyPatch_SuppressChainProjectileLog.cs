using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Suppresses the verbose "launched with ChainLeft: ..." debug log that the
/// GrimWorld Framework's ChainProjectile.Launch emits every time the Tesla Carbine
/// fires a chain arc. The line is uncommented in the shipped binary and cannot be
/// changed without recompiling the framework.
/// </summary>
[HarmonyPatch(typeof(Log), nameof(Log.Warning))]
public static class HarmonyPatch_SuppressChainProjectileLog
{
    [HarmonyPrefix]
    public static bool Prefix(string text)
    {
        if (text != null
            && text.Contains("launched with ChainLeft:")
            && text.Contains("Tesla Carbine"))
            return false; // skip this specific debug line

        return true;
    }
}
