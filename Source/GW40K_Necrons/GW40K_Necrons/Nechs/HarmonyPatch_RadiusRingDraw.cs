using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Draws all Necron radius overlays during the map selection overlay phase (pre-render),
/// so GenDraw calls land before the camera renders. Each gizmo sets a static hover field
/// during GizmoOnGUI; this postfix reads those fields and issues the draw calls.
/// </summary>
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
public static class HarmonyPatch_BandwidthRingDraw
{
    public static void Postfix()
    {
        HediffComp_NecronCommandTracker tracker = Gizmo_NecronBandwidth.HoveredTracker;
        if (tracker != null)
        {
            Pawn commander = tracker.CommanderPawn;
            if (commander != null && commander.Spawned && commander.MapHeld == Find.CurrentMap)
                GenDraw.DrawRadiusRing(commander.Position, tracker.ControlRange);
        }

        // "Select commander" button: draw the same ring when the player hovers over it on any Nech.
        HediffComp_NecronCommandTracker selectCmdTracker = HarmonyPatch_NechGizmos.Command_SelectCommander.HoveredTracker;
        if (selectCmdTracker != null)
        {
            Pawn commander = selectCmdTracker.CommanderPawn;
            if (commander != null && commander.Spawned && commander.MapHeld == Find.CurrentMap)
                GenDraw.DrawRadiusRing(commander.Position, selectCmdTracker.ControlRange);
        }

        // Auto-attack gizmo (Comp_SpyderAutoAttack drafted button) — show beamer range on hover.
        Pawn autoSpyder = Command_SpyderAutoAttack.HoveredPawn;
        if (autoSpyder != null && autoSpyder.Spawned && autoSpyder.MapHeld == Find.CurrentMap)
        {
            float autoRange = Command_SpyderAutoAttack.HoveredRange;
            if (autoRange > 0f)
                GenDraw.DrawRadiusRing(autoSpyder.Position, autoRange, Color.white);
        }

        if (!Find.Targeter.IsTargeting)
            HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingPawn = null;

        bool spyderHovered   = HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.HoveredPawn != null;
        bool spyderTargeting = HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingPawn != null;
        Pawn spyder          = HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.HoveredPawn
                            ?? HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingPawn;
        if (spyder != null && spyder.Spawned && spyder.MapHeld == Find.CurrentMap)
        {
            float range = spyderHovered
                ? HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.HoveredRange
                : HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingRange;
            if (range > 0f)
                GenDraw.DrawRadiusRing(spyder.Position, range, Color.white);

            if (spyderTargeting)
            {
                float blast = HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingBlast;
                if (blast > 0f)
                    GenDraw.DrawRadiusRing(UI.MouseCell(), blast, new Color(1f, 0.5f, 0f));
            }
        }

        Pawn scarab = HarmonyPatch_ScarabDetonateRing.HoveredPawn;
        if (scarab != null && scarab.Spawned && scarab.MapHeld == Find.CurrentMap)
        {
            float radius = HarmonyPatch_ScarabDetonateRing.HoveredRadius;
            if (radius > 0f)
                GenDraw.DrawRadiusRing(scarab.Position, radius, new Color(1f, 0.5f, 0f));
        }
    }
}

/// <summary>
/// Sets hover state when the mouse is over the scarab detonate ability gizmo.
/// Radius is computed live from surviving scarab units so the ring reflects current blast potential.
/// </summary>
[HarmonyPatch(typeof(Command_Ability), "GizmoOnGUI")]
static class HarmonyPatch_ScarabDetonateRing
{
    internal static Pawn HoveredPawn;
    internal static float HoveredRadius;

    static void Postfix(Command_Ability __instance, Vector2 topLeft, float maxWidth)
    {
        Ability ability = Traverse.Create(__instance).Field("ability").GetValue<Ability>();
        if (ability?.def?.defName != "GW40K_ScarabSelfDestruct")
        {
            HoveredPawn = null;
            return;
        }

        Pawn pawn = ability.pawn;
        Rect rect = new(topLeft.x, topLeft.y, __instance.GetWidth(maxWidth), 75f);
        bool active = pawn?.Spawned == true && Mouse.IsOver(rect);
            // || ability.verb?.WarmingUp == true  — re-enable if ability ever requires targeting again
        if (active)
        {
            ScarabSelfDestructProperties props = GetProps(ability.def);
            int alive = ScarabSelfDestructUtility.AliveUnitCount(pawn);
            HoveredPawn = pawn;
            HoveredRadius = props != null ? props.baseRadius * (alive / 4f) : 0f;
        }
        else
        {
            HoveredPawn = null;
        }
    }

    private static ScarabSelfDestructProperties GetProps(AbilityDef def)
    {
        if (def?.comps == null)
            return null;
        for (int i = 0; i < def.comps.Count; i++)
            if (def.comps[i] is ScarabSelfDestructProperties p)
                return p;
        return null;
    }
}
