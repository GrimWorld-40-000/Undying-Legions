using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Think-tree gate: passes only when the pawn is player-faction, has a
/// <see cref="ThingComp_NechWorkMode"/>, and its current mode equals <see cref="workMode"/>.
/// <para>
/// Mirrors <c>ThinkNode_ConditionalWorkMode</c> from vanilla Biotech but uses our own
/// command-tracker component instead of the vanilla overseer/control-group system.
/// Enemy Necrons always fail the faction check and fall through to their lord duties.
/// </para>
/// </summary>
public class ThinkNode_ConditionalNechWorkMode : ThinkNode_Conditional
{
    /// <summary>The mode this node gates on. Set from XML: <c>&lt;workMode&gt;GW40K_NechMode_Patrol&lt;/workMode&gt;</c>.</summary>
    public NechWorkModeDef workMode;

    public override ThinkNode DeepCopy(bool resolve = true)
    {
        ThinkNode_ConditionalNechWorkMode copy = (ThinkNode_ConditionalNechWorkMode)base.DeepCopy(resolve);
        copy.workMode = workMode;
        return copy;
    }

    protected override bool Satisfied(Pawn pawn)
    {
        if (pawn.Faction != Faction.OfPlayer) return false;
        ThingComp_NechWorkMode comp = pawn.TryGetComp<ThingComp_NechWorkMode>();
        if (comp == null) return false;
        return comp.CurMode == workMode;
    }
}
