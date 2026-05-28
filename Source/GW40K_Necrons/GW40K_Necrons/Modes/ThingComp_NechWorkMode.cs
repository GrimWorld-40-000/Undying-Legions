using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Mode-switch command that re-evaluates disabled state on every draw call
/// (Gizmo.disabled is protected and can only be set from within a subclass).
/// </summary>
internal sealed class Command_NechMode : Command_Action
{
    private readonly Func<bool> _isDisabledGetter;
    private readonly Func<string> _disabledReasonGetter;

    public Command_NechMode(Func<bool> isDisabled, Func<string> disabledReason)
    {
        _isDisabledGetter = isDisabled;
        _disabledReasonGetter = disabledReason;
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        disabled = _isDisabledGetter?.Invoke() ?? false;
        disabledReason = _disabledReasonGetter?.Invoke() ?? string.Empty;
        return base.GizmoOnGUI(topLeft, maxWidth, parms);
    }
}

/// <summary>
/// Per-pawn work mode tracker for commandable Necron pawns.
/// Mirrors the concept of <c>MechanitorControlGroup.WorkMode</c> but is stored directly
/// on the pawn as a ThingComp — no group layer needed.
/// <para>
/// Mode switching is locked when no commander is linked (via Command Protocol).
/// The gizmo is only shown for player-faction pawns.
/// </para>
/// </summary>
public class ThingComp_NechWorkMode : ThingComp
{
    private NechWorkModeDef _curMode;

    /// <summary>Position stored when the pawn enters <see cref="NechWorkModeDefOf.GW40K_NechMode_Hold"/>.</summary>
    private IntVec3 _heldPosition = IntVec3.Invalid;

    public NechWorkModeDef CurMode => _curMode;
    public IntVec3 HeldPosition => _heldPosition;
    public CompProperties_NechWorkMode Props => (CompProperties_NechWorkMode)props;
    private Pawn Pawn => (Pawn)parent;

    /// <summary>True when a Nechinator commander has this pawn in its <c>controlledMechs</c> list.</summary>
    public bool HasCommander => HediffComp_NecronCommandTracker.GetCommanderOf(Pawn) != null;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (_curMode == null)
            _curMode = Props.defaultMode
                       ?? (Props.availableModes.Count > 0 ? Props.availableModes[0] : null);
    }

    // ── API ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to switch to <paramref name="mode"/>.
    /// Returns false and does nothing if there is no commander or the mode is not available.
    /// </summary>
    public bool TrySetMode(NechWorkModeDef mode)
    {
        if (mode == null) return false;
        if (!HasCommander) return false;
        if (!Props.availableModes.Contains(mode)) return false;

        _curMode = mode;

        // Capture position when entering Hold mode.
        if (mode == NechWorkModeDefOf.GW40K_NechMode_Hold && Pawn.Spawned)
            _heldPosition = Pawn.Position;

        Pawn.jobs?.CheckForJobOverride();
        return true;
    }

    // ── Gizmo ───────────────────────────────────────────────────────────────

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (Pawn.Faction != Faction.OfPlayer) yield break;
        if (Props.availableModes.NullOrEmpty()) yield break;

        yield return new Command_NechMode(
            isDisabled: () => !HasCommander,
            disabledReason: () => "GW40K_NechWorkMode_NoCommander".Translate()
        )
        {
            defaultLabel = _curMode?.LabelCap ?? "Mode",
            defaultDesc = "GW40K_NechWorkMode_GizmoDesc".Translate(),
            icon = _curMode?.UIIcon ?? BaseContent.BadTex,
            action = delegate
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (NechWorkModeDef m in Props.availableModes)
                {
                    NechWorkModeDef captured = m;
                    bool isCurrent = captured == _curMode;
                    string label = isCurrent ? (captured.LabelCap + " ✓") : captured.LabelCap;
                    opts.Add(new FloatMenuOption(label, () => TrySetMode(captured)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        };
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref _curMode, "curMode");
        Scribe_Values.Look(ref _heldPosition, "heldPosition", IntVec3.Invalid);
    }
}
