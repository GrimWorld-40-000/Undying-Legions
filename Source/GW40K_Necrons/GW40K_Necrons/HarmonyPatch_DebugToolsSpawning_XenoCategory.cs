using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Puts <c>UD_*</c> pawn kinds in dev-mode spawn menu under <see cref="UdXenoDevTools.DevMenuCategory"/>
/// instead of Humanlike / Mechanoids.
/// </summary>
[HarmonyPatch(typeof(DebugToolsSpawning), nameof(DebugToolsSpawning.GetCategoryForPawnKind))]
public static class HarmonyPatch_DebugToolsSpawning_XenoCategory
{
    [HarmonyPostfix]
    public static void Postfix(PawnKindDef kindDef, ref string __result)
    {
        if (UdXenoDevTools.IsUdXenoPawnKind(kindDef))
            __result = UdXenoDevTools.DevMenuCategory;
    }
}
