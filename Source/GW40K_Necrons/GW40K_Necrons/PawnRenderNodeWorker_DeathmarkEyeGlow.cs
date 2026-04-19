using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Draws the Deathmark ocular glow overlay only when ambient light is low.
/// North facing is suppressed via <visibleFacing> in XML (back of head).
/// Offset nudges are authored at ~1× render scale; they are multiplied by
/// <see cref="PawnRenderNodeWorker.ScaleFor"/> so the glow stays on the eye when the
/// Deathmark chassis/head is drawn smaller (e.g. body <c>drawSize</c> 0.88).
/// </summary>
public class PawnRenderNodeWorker_DeathmarkEyeGlow : PawnRenderNodeWorker
{
    // Glow activates below this ambient light level (0 = dark, 1 = full daylight).
    private const float LightThreshold = 0.4f;

    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!base.CanDrawNow(node, parms)) return false;
        if (parms.Portrait) return false;
        if (parms.pawn.Map == null) return false;

        float light = parms.pawn.Map.glowGrid.GroundGlowAt(parms.pawn.Position);
        return light < LightThreshold;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Vector3 offset = base.OffsetFor(node, parms, out pivot);

        // Nudge glow to the ocular sensor position on the Deathmark head (1×-space).
        // Rot4: North=0, East=1, South=2, West=3.
        Vector3 nudge = parms.facing.AsInt switch
        {
            1 => new Vector3(0.35f, 0f, 0.04f), // East  – face points right, eye at +X
            3 => new Vector3(-0.35f, 0f, 0.04f), // West  – face points left, eye at -X
            _ => new Vector3(0f, 0f, 0.05f), // South – single centred oculus
        };

        Vector3 scale = base.ScaleFor(node, parms);
        offset += Vector3.Scale(nudge, scale);

        return offset;
    }
}
