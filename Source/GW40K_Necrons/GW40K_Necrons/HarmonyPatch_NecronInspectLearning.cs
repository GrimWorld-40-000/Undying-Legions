using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Learning-helper triggers for humanlike Necron xenotypes (no <see cref="NecronMechExtension"/> inspect rewrite).</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class HarmonyPatch_NecronInspectLearning
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (__instance?.def?.GetModExtension<NecronMechExtension>() != null)
            return;
        if (__instance.RaceProps?.Humanlike != true || !NechEnergyUtility.IsNecronPawn(__instance))
            return;

        NecronLearning.OnInspectHumanlikeNecron(__instance);
    }
}
