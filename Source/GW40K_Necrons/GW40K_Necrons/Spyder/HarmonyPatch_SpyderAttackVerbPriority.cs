using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Controls which verb the Spyder uses for drafted right-click attacks based on the
/// particle beamer auto-attack toggle.
///
///   Auto mode ON  → default verb selection (ranged beamer preferred, as normal).
///   Auto mode OFF → override TryGetAttackVerb to return a melee verb so right-clicking
///                   an enemy charges with the power claws instead.
///
/// The gizmo's explicit beamer targeting calls TryStartCastOn directly and is unaffected.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb),
    typeof(Thing), typeof(bool), typeof(bool))]
public static class HarmonyPatch_SpyderAttackVerbPriority
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, Thing target, ref Verb __result)
    {
        var comp = __instance.TryGetComp<Comp_SpyderAutoAttack>();
        if (comp == null) return;

        if (!comp.autoAttackEnabled)
        {
            // Auto OFF → prefer melee (power claws) for right-click attacks.
            LocalTargetInfo lti = target != null ? new LocalTargetInfo(target) : LocalTargetInfo.Invalid;
            Verb meleeVerb = __instance.verbTracker?.AllVerbs?.Find(v =>
                v.IsMeleeAttack && v.Available() &&
                (lti.IsValid ? v.CanHitTarget(lti) : true));
            if (meleeVerb != null)
                __result = meleeVerb;
        }
        // Auto ON → default selection already prefers the ranged beamer; no override needed.
    }
}
