using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Skips the glow/lighting penalty applied by StatPart_Glow for pawns that carry the
/// Deathmark Oculus hediff. Only suppressed when the parent stat is ShootingAccuracyPawn
/// so work-speed and other glow-affected stats are unaffected.
/// </summary>
[HarmonyPatch(typeof(StatPart_Glow), nameof(StatPart_Glow.TransformValue))]
public static class HarmonyPatch_DeathmarkNightVision
{
    public static bool Prefix(StatPart_Glow __instance, StatRequest req)
    {
        if (__instance.parentStat != StatDefOf.ShootingAccuracyPawn)
            return true;
        if (!req.HasThing || req.Thing is not Pawn pawn)
            return true;
        if (pawn.health?.hediffSet?.HasHediff(NecronDefOfs.GW_UD_DeathmarkOculus) == true)
            return false; // skip: no darkness penalty
        return true;
    }
}
