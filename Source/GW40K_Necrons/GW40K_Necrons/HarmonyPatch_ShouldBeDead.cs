using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
public class HarmonyPatch_ShouldBeDead
{
    /// <summary>
    /// Skip vanilla ShouldBeDead when it would NRE on incomplete pawn/body during generation,
    /// and when non-organic necrons have no brain hediff yet.
    /// </summary>
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn, ref bool __result)
    {
        if (___pawn == null)
            return true;

        if (___pawn.def?.GetModExtension<NecronMechExtension>() != null
            && ___pawn.RaceProps?.body == null)
        {
            __result = false;
            return false;
        }

        if (___pawn.def?.GetModExtension<NonOrganicPawn>() != null
            && ___pawn.health?.hediffSet?.GetBrain() == null)
        {
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn ___pawn, ref bool __result)
    {
        if (___pawn?.def?.GetModExtension<NonOrganicPawn>() == null)
            return;
        if (___pawn.health?.hediffSet?.GetBrain() == null)
            return;
        __result = false;
    }
}
