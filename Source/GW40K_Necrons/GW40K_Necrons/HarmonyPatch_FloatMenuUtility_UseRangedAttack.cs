using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Vanilla <see cref="FloatMenuUtility.UseRangedAttack"/> can throw <see cref="NullReferenceException"/> for some pawns
/// when the squad attack gizmo is built (e.g. mass mixed selection). That aborts the entire <see cref="Pawn.GetGizmos"/> chain.
/// Undying Legions <c>GetGizmos</c> postfixes only consume gizmos — they do not call this API; the failure is in RimWorld code.
/// </summary>
[HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.UseRangedAttack))]
public static class HarmonyPatch_FloatMenuUtility_UseRangedAttack
{
    /// <summary>Scarabs: no vanilla ranged-attack probe — returns false without running the failing path.</summary>
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, ref bool __result)
    {
        if (pawn == null)
        {
            __result = false;
            return false;
        }

        if (NechEnergyUtility.IsScarab(pawn))
        {
            __result = false;
            return false;
        }

        return true;
    }

    /// <summary>Treat unexpected null derefs as &quot;not a ranged attack pawn&quot; so the UI can continue.</summary>
    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, ref bool __result)
    {
        if (__exception is NullReferenceException)
        {
            __result = false;
            return null;
        }

        return __exception;
    }
}
