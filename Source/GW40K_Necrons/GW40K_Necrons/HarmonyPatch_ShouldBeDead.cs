using System;
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

    // ThreadStatic so each thread has its own flag — Mono may call ShouldBeDead from the main sim thread only,
    // but this is safe for any hypothetical background health checks too.
    [ThreadStatic]
    private static bool _postfixRunning;

    [HarmonyPostfix]
    public static void Postfix(Pawn ___pawn, ref bool __result)
    {
        // Guard 1: skip when pawn is not marked dead — CanEnterResurrectionProtocol iterates Gene.Active
        // which can add hediffs, re-entering ShouldBeDead and causing a stack overflow.
        if (!__result) return;

        // Guard 2: reentrancy — if a hediff added inside CanEnterResurrectionProtocol triggers another
        // ShouldBeDead check, skip the postfix on the inner call to break the recursion.
        if (_postfixRunning) return;
        _postfixRunning = true;
        try
        {
            PostfixCore(___pawn, ref __result);
        }
        finally
        {
            _postfixRunning = false;
        }
    }

    private static void PostfixCore(Pawn ___pawn, ref bool __result)
    {

        // Scarab swarm: stay alive while any unit survives, die only when all units are gone.
        // Must override vanilla in both directions — chassis alone is not a death trigger.
        if (HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(___pawn)
            && ___pawn.health?.hediffSet != null
            && !___pawn.Dead)
        {
            __result = HarmonyPatch_ScarabSwarmChassis.ScarabUnitSlotCount(___pawn) > 0
                       && !HarmonyPatch_ScarabSwarmChassis.AnyLivingScarabUnit(___pawn);
            return;
        }

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
