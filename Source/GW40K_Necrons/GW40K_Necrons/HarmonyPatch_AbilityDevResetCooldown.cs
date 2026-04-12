using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// Injects a "DEV: Reset cooldown" gizmo on any Ability that has a non-zero
// cooldown, visible whenever GodMode / Show Dev Gizmos is active.
[HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
public static class HarmonyPatch_AbilityDevResetCooldown
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Ability __instance)
    {
        foreach (Gizmo g in __result)
            yield return g;

        if (!DebugSettings.ShowDevGizmos) yield break;
        if (__instance.def.cooldownTicksRange.max <= 0) yield break;

        yield return new Command_Action
        {
            defaultLabel = "DEV: Reset cooldown",
            defaultDesc = $"Instantly resets the '{__instance.def.label}' cooldown.",
            action = () => __instance.StartCooldown(0)
        };
    }
}
