using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Custom bandwidth display gizmo for the Necron command protocol.
/// Main block sits at the standard gizmo row. Bound construct portraits
/// grow upward above it so they never go off-screen at the bottom.
/// </summary>
public class Gizmo_NecronBandwidth : Gizmo
{
    private static readonly Color NecronGreen = new Color(0.18f, 1f, 0.28f);
    private static readonly Color BlockEmpty  = new Color(0.18f, 0.18f, 0.18f);

    private readonly HediffComp_NecronCommandTracker tracker;

    private const float GizmoW      = 140f;
    private const float MainH       = 92f;   // taller than default row so bandwidth line is not clipped
    private const float PortraitSize = 36f;
    private const float PortraitGap  = 2f;
    private const float BlockSize    = 14f;
    private const float BlockGap     = 2f;
    private const float PortraitsPerRow = 3f;

    public Gizmo_NecronBandwidth(HediffComp_NecronCommandTracker tracker)
    {
        this.tracker = tracker;
    }

    public override float GetWidth(float maxWidth) => Mathf.Min(GizmoW, maxWidth);

    private float PortraitPanelHeight()
    {
        int count = tracker.controlledMechs.Count;
        if (count == 0) return 0f;
        int rows = Mathf.CeilToInt(count / PortraitsPerRow);
        return rows * (PortraitSize + PortraitGap);
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        float w = GetWidth(maxWidth);
        float portraitH = PortraitPanelHeight();

        // Main gizmo block at the standard row position
        Rect main = new Rect(topLeft.x, topLeft.y, w, MainH);
        Widgets.DrawWindowBackground(main);

        // Portrait panel grows upward above the main block
        bool portraitClickAbsorbed = false;
        if (portraitH > 0f)
        {
            Rect portraitPanel = new Rect(topLeft.x, topLeft.y - portraitH, w, portraitH);
            Widgets.DrawWindowBackground(portraitPanel);
            portraitClickAbsorbed = DrawPortraits(portraitPanel);
        }

        // Title
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(main.x + 4f, main.y + 4f, w - 8f, 16f), "Command Protocol");

        // Bandwidth fraction
        int used = Mathf.RoundToInt(tracker.BandwidthUsed);
        int max  = Mathf.RoundToInt(tracker.BandwidthMax);
        Widgets.Label(new Rect(main.x + 4f, main.y + 22f, w - 8f, 22f), $"{used} / {max} bandwidth");

        // Bandwidth blocks (below label with a little gap)
        int displayMax = Mathf.Min(max, 10);
        float blockStart = main.x + 6f;
        float blocksY = main.y + 46f;
        for (int i = 0; i < displayMax; i++)
        {
            Rect block = new Rect(blockStart + i * (BlockSize + BlockGap), blocksY, BlockSize, BlockSize);
            Widgets.DrawBoxSolid(block, i < used ? NecronGreen : BlockEmpty);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;

        TooltipHandler.TipRegion(main,
            $"Necron command protocol — bandwidth determines how many constructs this commander can directly control.\nCommand range: {tracker.ControlRange:0.#}");

        float hitTop = portraitH > 0f ? topLeft.y - portraitH : topLeft.y;
        float hitH = portraitH + MainH;
        Rect unionHit = new Rect(topLeft.x, hitTop, w, hitH);
        bool overBandwidth = Mouse.IsOver(unionHit);
        if (overBandwidth)
        {
            Pawn commander = tracker.CommanderPawn;
            if (commander != null && commander.Spawned && commander.MapHeld != null)
                GenDraw.DrawRadiusRing(commander.Position, tracker.ControlRange);
        }

        GizmoState state = portraitClickAbsorbed
            ? GizmoState.Interacted
            : overBandwidth
                ? GizmoState.Mouseover
                : GizmoState.Clear;
        return new GizmoResult(state);
    }

    /// <summary>Returns true if a portrait click was handled (so the gizmo absorbs the event).</summary>
    private bool DrawPortraits(Rect panel)
    {
        float x = panel.x + 4f;
        float y = panel.y + PortraitGap;

        for (int i = 0; i < tracker.controlledMechs.Count; i++)
        {
            Pawn mech = tracker.controlledMechs[i];
            if (mech == null || mech.Dead || mech.Destroyed) continue;

            if (x + PortraitSize > panel.xMax - 4f)
            {
                x  = panel.x + 4f;
                y += PortraitSize + PortraitGap;
            }

            Rect r = new Rect(x, y, PortraitSize, PortraitSize);
            RenderTexture portrait = PortraitsCache.Get(
                mech,
                new Vector2(PortraitSize, PortraitSize),
                Rot4.South,
                default,
                1.0f);
            GUI.DrawTexture(r, portrait);
            TooltipHandler.TipRegion(r, mech.LabelCap);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(r))
            {
                CameraJumper.TryJumpAndSelect(mech);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                Event.current.Use();
                return true;
            }

            x += PortraitSize + PortraitGap;
        }

        return false;
    }
}
