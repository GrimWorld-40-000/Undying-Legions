using System;
using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// When zoomed far out, RimWorld draws silhouettes instead of full meshes. Vanilla
/// <c>DynamicDrawManager.DrawSilhouettes</c> can throw <see cref="NullReferenceException"/> (e.g. null graphic/material
/// on a modded thing). The log line comes from <see cref="DynamicDrawManager.DrawDynamicThings"/> catching it — no mod
/// appears in the stack. This finalizer suppresses that failure for one frame so play continues without spam.
/// </summary>
[HarmonyPatch(typeof(DynamicDrawManager), "DrawSilhouettes")]
public static class HarmonyPatch_DynamicDrawManager_DrawSilhouettes
{
    [HarmonyPrefix]
    public static void Prefix(DynamicDrawManager __instance)
    {
        DynamicDrawSilhouetteDiagnostics.RunScanIfPending(__instance);
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
            return null;

        return __exception;
    }
}
