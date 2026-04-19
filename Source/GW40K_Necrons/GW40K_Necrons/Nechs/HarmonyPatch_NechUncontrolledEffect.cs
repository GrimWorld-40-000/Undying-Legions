using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Show uncontrolled flash only when a Nech is not actually linked in the Nechinator tracker.
/// This replaces vanilla CompOverseerSubject.needsOverseerEffect behavior, which only understands
/// vanilla mechanitors and can flash incorrectly for Necron-controlled constructs.
/// </summary>
// Pawn.Tick is protected — cannot use nameof from another assembly; Harmony resolves by name.
[HarmonyPatch(typeof(Pawn), "Tick")]
public static class HarmonyPatch_NechUncontrolledEffect
{
    private const int FlashIntervalTicks = 200;

    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (__instance?.def?.GetModExtension<NecronMechExtension>() == null) return;
        if (!__instance.Spawned || __instance.MapHeld == null || __instance.Destroyed || __instance.Dead) return;
        if (Find.TickManager.TicksGame % FlashIntervalTicks != 0) return;

        if (HediffComp_NecronCommandTracker.GetCommanderOf(__instance) != null) return;

        ThingDef mote = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_GW40K_MechUncontrolled_Nech");
        if (mote == null) return;

        // For humanlike pawns (Flayed One), DrawPos is at head level — the body graphic is drawn
        // below it. Spawn a second mote offset downward to cover the torso+legs.
        MoteMaker.MakeAttachedOverlay(__instance, mote, Vector3.zero);
        if (__instance.RaceProps.Humanlike)
            MoteMaker.MakeAttachedOverlay(__instance, mote, new Vector3(0f, 0f, -0.32f));
    }
}
