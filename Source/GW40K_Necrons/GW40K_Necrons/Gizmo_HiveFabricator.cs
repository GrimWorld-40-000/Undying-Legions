using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GW40K_Necrons;

[StaticConstructorOnStartup]
public class Gizmo_HiveFabricator : Gizmo
{
    private static readonly Color DarkGreenFill = new Color(0.06f, 0.30f, 0.12f);
    private static readonly Color FillOnColor  = new Color(0.22f, 0.88f, 0.30f);
    private static readonly Color ReplicateReady = new Color(0.3f, 0.72f, 1.0f);
    private static readonly Texture2D OverlayTex;

    static Gizmo_HiveFabricator()
    {
        OverlayTex = ContentFinder<Texture2D>.Get("UI/GW40K_Necron_overlayF", false);
    }

    private const float StandardGizmoRowH = 75f;
    private const float BarH = 30f;
    private const float BarStatusGap = 5f;
    private const float StatusH = 18f;
    private const float BarBottomPad = 10f;
    private const float HeaderH = 18f;
    private const float HeaderBarGap = 8f;
    private const float ButtonRowH = 18f;
    private const float ButtonBarGap = 4f;
    private const float DevBtnSize = 18f;
    private const float DevBtnGap = 2f;
    private const float DevHitSlop = 4f;
    private const float DevAdjustStep = 25f;
    // Dev strip is always reserved so toggling godmode never resizes the bar.
    private const float DevStripW = DevBtnSize + 4f;
    private const float GizmoW = 160f;

    private readonly HediffComp_HiveFabricator comp;
    private readonly Pawn pawn;

    public Gizmo_HiveFabricator(HediffComp_HiveFabricator comp, Pawn pawn)
    {
        this.comp = comp;
        this.pawn = pawn;
    }

    public override float GetWidth(float maxWidth) => Mathf.Min(GizmoW, maxWidth);

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        if (NecronDefOfs.GW_UD_Concept_HiveFabricator != null)
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(NecronDefOfs.GW_UD_Concept_HiveFabricator, KnowledgeAmount.FrameDisplayed);

        float w = GetWidth(maxWidth);
        bool showDev = DebugSettings.ShowDevGizmos && DebugSettings.godMode;

        // Dev strip is always reserved so the bar never resizes on godmode toggle.
        float innerH = HeaderH + HeaderBarGap + ButtonRowH + ButtonBarGap + BarH + BarStatusGap + StatusH + BarBottomPad;
        float totalH = innerH + 12f;
        Rect outRect = new Rect(topLeft.x, topLeft.y + StandardGizmoRowH - totalH, w, totalH);
        Widgets.DrawWindowBackground(outRect);

        if (OverlayTex != null)
        {
            float ow = OverlayTex.width * 0.25f;
            float oh = OverlayTex.height * 0.25f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(outRect.x, outRect.y, ow, oh), OverlayTex);
            GUI.color = prev;
        }

        Rect inner = outRect.ContractedBy(6f);
        // Title anchored to top; everything else shifted 4px down toward the bottom edge.
        float titleTop     = inner.y;
        float statusBottom = inner.yMax - BarBottomPad + 4f;
        float statusTop    = statusBottom - StatusH;
        float barBottom    = statusTop - BarStatusGap;
        float barTop       = barBottom - BarH;
        float btnTop       = barTop - ButtonBarGap - ButtonRowH;

        // Title
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(new Rect(inner.x, titleTop, inner.width, HeaderH),
            "GW40K_HiveFabricator_Title".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        // Buttons above the bar
        float halfW = (inner.width - DevStripW - 3f) / 2f;
        Rect fillRect      = new Rect(inner.x, btnTop, halfW, ButtonRowH);
        Rect replicateRect = new Rect(inner.x + halfW + 3f, btnTop, halfW, ButtonRowH);

        bool absorbed = false;
        bool fillOn = comp.autoRefuel;
        GUI.color = fillOn ? FillOnColor : Color.white;
        if (Widgets.ButtonText(fillRect, "GW40K_HiveFabricator_Fill".Translate()))
        {
            comp.autoRefuel = !comp.autoRefuel;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            absorbed = true;
        }
        GUI.color = Color.white;
        TooltipHandler.TipRegion(fillRect, "GW40K_HiveFabricator_Fill_Tip".Translate());

        HediffComp_ControlNodeTracker tracker = HediffComp_ControlNodeTracker.GetTracker(pawn);
        bool canReplicate = !comp.IsReplicating
            && comp.stored >= comp.Props.replicateCost
            && tracker != null
            && tracker.BandwidthUsed + tracker.BandwidthCostPerScarab <= tracker.BandwidthMax;

        GUI.color = canReplicate ? ReplicateReady : Color.gray;
        if (Widgets.ButtonText(replicateRect, "GW40K_HiveFabricator_Replicate".Translate()) && canReplicate)
        {
            if (comp.TryStartReplication(out string fail))
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            else
                Messages.Message(fail, MessageTypeDefOf.RejectInput, false);
            absorbed = true;
        }
        GUI.color = Color.white;
        TooltipHandler.TipRegion(replicateRect, canReplicate
            ? "GW40K_HiveFabricator_Replicate_Tip".Translate(comp.Props.replicateCost.ToString("F0"))
            : BuildReplicateDisabledTip());

        // Bar — always leave DevStripW on the right so width never changes with godmode
        float barW = inner.width - DevStripW;
        Rect barRect = new Rect(inner.x, barTop, barW, BarH);
        float pct = Mathf.Clamp01(comp.stored / comp.Props.maxStored);
        Widgets.FillableBar(barRect, pct,
            SolidColorMaterials.NewSolidColorTexture(DarkGreenFill),
            BaseContent.BlackTex, true);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(barRect, $"{comp.stored:F0} / {comp.Props.maxStored:F0}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // Status below bar
        string status = comp.IsReplicating
            ? "GW40K_HiveFabricator_Replicating".Translate(comp.TicksUntilReplicate.ToStringTicksToPeriod())
            : "GW40K_HiveFabricator_Idle".Translate();
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = comp.IsReplicating ? new Color(1f, 0.85f, 0.3f) : Color.gray;
        Widgets.Label(new Rect(inner.x + 2f, statusTop, inner.width - 4f, StatusH), status);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // Dev +/- buttons — always positioned in the reserved strip; only drawn in godmode
        Rect plusRect  = Rect.zero;
        Rect minusRect = Rect.zero;
        if (showDev)
        {
            float devX = inner.xMax - DevBtnSize;
            plusRect  = new Rect(devX, barTop, DevBtnSize, DevBtnSize);
            minusRect = new Rect(devX, barTop + DevBtnSize + DevBtnGap, DevBtnSize, DevBtnSize);

            Rect plusHit  = plusRect.ExpandedBy(DevHitSlop);
            Rect minusHit = minusRect.ExpandedBy(DevHitSlop);

            if (TryHandleDevClick(plusHit, minusHit))
            {
                Event.current.Use();
                return new GizmoResult(Mouse.IsOver(outRect) ? GizmoState.Mouseover : GizmoState.Clear);
            }

            if (Mouse.IsOver(plusRect)) Widgets.DrawHighlight(plusRect);
            Widgets.DrawTextureFitted(plusRect, TexButton.Plus, 1f);
            TooltipHandler.TipRegion(plusHit, $"DEV: +{DevAdjustStep} necrodermis");

            if (Mouse.IsOver(minusRect)) Widgets.DrawHighlight(minusRect);
            Widgets.DrawTextureFitted(minusRect, TexButton.Minus, 1f);
            TooltipHandler.TipRegion(minusHit, $"DEV: -{DevAdjustStep} necrodermis");
        }

        GizmoState state = absorbed
            ? GizmoState.Interacted
            : Mouse.IsOver(outRect) ? GizmoState.Mouseover : GizmoState.Clear;
        return absorbed ? new GizmoResult(state, Event.current) : new GizmoResult(state);
    }

    private bool TryHandleDevClick(Rect plusHit, Rect minusHit)
    {
        if (!DebugSettings.ShowDevGizmos || !DebugSettings.godMode) return false;
        Event ev = Event.current;
        if (ev == null || ev.button != 0) return false;
        if (ev.type != EventType.MouseDown && ev.rawType != EventType.MouseDown) return false;

        Vector2 mp = ev.mousePosition;
        if (plusHit.Contains(mp))
        {
            comp.AddNecrodermis(DevAdjustStep);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            return true;
        }
        if (minusHit.Contains(mp))
        {
            comp.stored = Mathf.Max(0f, comp.stored - DevAdjustStep);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            return true;
        }
        return false;
    }

    private string BuildReplicateDisabledTip()
    {
        if (comp.IsReplicating) return "GW40K_Replicate_AlreadyReplicating".Translate();
        if (comp.stored < comp.Props.replicateCost) return "GW40K_Replicate_NoNecrodermis".Translate(comp.Props.replicateCost.ToString("F0"));
        return "GW40K_Replicate_NoBandwidth".Translate();
    }
}
