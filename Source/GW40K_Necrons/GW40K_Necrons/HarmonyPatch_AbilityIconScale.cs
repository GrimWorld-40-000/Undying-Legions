using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// Reads AbilityExtension_GW40K.iconScale and applies it to the gizmo button
// so the icon renders larger/smaller without changing the button size.
[HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
public static class HarmonyPatch_AbilityIconScale
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Ability __instance)
    {
        float scale = __instance.def.GetModExtension<AbilityExtension_GW40K>()?.iconScale ?? 1f;
        foreach (Gizmo g in __result)
        {
            if (scale != 1f && g is Command cmd)
                cmd.iconDrawScale = scale;
            yield return g;
        }
    }
}
