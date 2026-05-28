using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GW40K_Necrons;

public class CompProperties_SpyderSiegeCannon : CompProperties
{
    public CompProperties_SpyderSiegeCannon() => compClass = typeof(Comp_SpyderSiegeCannon);
}

/// <summary>
/// Gizmo comp for the Spyder's siege cannon. Cooldown disabled — warmup alone gates fire rate.
/// </summary>
public class Comp_SpyderSiegeCannon : ThingComp
{
    private int lastCastTick = -999999;

    // public const int CooldownTicks = 2500; // 1 game-hour — disabled, warmup is sufficient

    public bool  IsReady          => true; // no cooldown
    public float CooldownFraction => 1f;   // always "full" — overlay never draws
    public float RemainingFraction => 0f;

    public void NotifyCastStarted()  { } // no-op

    public void ResetCooldown() { } // no-op

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastCastTick, "siegeCannonLastCastTick", -999999);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent is not Pawn pawn) yield break;
        if (pawn.Faction == null || !pawn.Faction.IsPlayer) yield break;
        if (!HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) yield break;

        Verb verb = FindSiegeCannonVerb(pawn);
        if (verb == null) yield break;

        yield return new Command_SpyderSiegeCannon(verb, this);

        // Dev: reset cooldown instantly
        if (DebugSettings.ShowDevGizmos && DebugSettings.godMode)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: reset siege cannon cooldown",
                defaultDesc  = "Sets lastCastTick so the cannon fires immediately.",
                action       = () => { ResetCooldown(); SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(); }
            };
        }
    }

    private static Verb FindSiegeCannonVerb(Pawn pawn) =>
        pawn.verbTracker?.AllVerbs?.Find(v => v is Verb_SpyderSiegeCannon);
}

/// <summary>
/// Gizmo for the siege cannon verb. Draws a shrinking dark overlay over the button
/// that represents remaining cooldown — full coverage = just fired, no coverage = ready.
/// </summary>
[StaticConstructorOnStartup]
public class Command_SpyderSiegeCannon : Command_VerbTarget
{
    private static readonly Texture2D Tex;

    static Command_SpyderSiegeCannon()
    {
        Tex = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K_SpyderAttack");
    }

    private readonly Comp_SpyderSiegeCannon comp;

    public Command_SpyderSiegeCannon(Verb verb, Comp_SpyderSiegeCannon comp)
    {
        this.verb = verb;
        this.comp = comp;
        icon         = Tex;
        defaultLabel = "GW40K_SpyderSiegeCannonLabel".Translate();
        defaultDesc  = "GW40K_SpyderSiegeCannonDesc".Translate();
    }

    public override void ProcessInput(Event ev)
    {
        // If the Spyder is undrafted, draft it first so the targeting UI and warmup work.
        Pawn pawn = verb?.CasterPawn;
        if (pawn != null && !pawn.Drafted && pawn.drafter != null)
            pawn.drafter.Drafted = true;

        base.ProcessInput(ev);
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

        float remaining = comp.RemainingFraction;
        if (remaining > 0.001f)
        {
            // Shrinking dark band from the top: full height = just fired, zero = ready.
            float w    = Mathf.Min(GetWidth(maxWidth), maxWidth);
            Rect  rect = new Rect(topLeft.x, topLeft.y, w, 75f);
            float coverH = rect.height * remaining;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, coverH),
                new Color(0f, 0f, 0f, 0.65f));

            // Timer label removed — cooldown disabled, warmup alone gates fire rate.
        }

        return result;
    }
}
