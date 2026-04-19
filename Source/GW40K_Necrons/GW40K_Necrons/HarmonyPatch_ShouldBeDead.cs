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
            // If any core is destroyed the Necron must die so CompResurrectible can trigger reanimation.
            // If the cooldown is already active and any core is destroyed, it's permanent — stay dead.
            if (CompResurrectible.IsAnyCoreDestroyed(___pawn))
            {
                __result = true;
                return false;
            }
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

        // Both cores gone: permanent death — leave vanilla __result (usually true) so Kill can run.
        if (CompResurrectible.IsBothCoresDestroyed(___pawn))
            return;

        // Reanimation protocol: allow lethal ShouldBeDead → Kill → corpse → CompResurrectible (do not force immortal).
        if (CompResurrectible.CanEnterResurrectionProtocol(___pawn))
            return;

        __result = false;
    }
}
