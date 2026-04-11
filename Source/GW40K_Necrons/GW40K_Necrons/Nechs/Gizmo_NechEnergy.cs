using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

[StaticConstructorOnStartup]
public class Gizmo_NechEnergy : Gizmo
{
    private readonly Pawn pawn;
    private static readonly Color DarkGreenFill = new Color(0.06f, 0.30f, 0.12f);
    private static readonly Texture2D _overlayTex;

    static Gizmo_NechEnergy()
    {
        _overlayTex = ContentFinder<Texture2D>.Get("UI/GW40K_Necron_overlayF", false);
    }

    /// <summary>Vanilla gizmo row cell height — bottom of this gizmo aligns with <c>topLeft.y + this</c>.</summary>
    private const float StandardGizmoRowH = 75f;

    private const float BarH = 36f;
    private const float BarBottomPad = 8f;
    private const float BarDevGap = 6f;
    private const float HeaderBarGap = 8f;
    private const float DevBtnSize = 18f;
    private const float DevBtnGap = 2f;
    private const float DevHitSlop = 4f;
    private const float ToggleIcon = 32f;
    private const float ToggleGap = 4f;
    private const float DevAdjustStep = 0.25f;

    private const string TexBattOn = "UI/Gizmo/GW40k_W_BattOn32";
    private const string TexBattOff = "UI/Gizmo/GW40k_W_BattOff32";
    private const string TexCoreOn = "UI/Gizmo/GW40k_W_CoreOn32";
    private const string TexCoreOff = "UI/Gizmo/GW40k_W_CoreOff32";

    public Gizmo_NechEnergy(Pawn pawn)
    {
        this.pawn = pawn;
    }

    public override float GetWidth(float maxWidth) => Mathf.Min(172f, maxWidth);

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        float w = GetWidth(maxWidth);

        Need n = pawn?.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        float level = n?.CurLevelPercentage ?? 0f;
        float capacity = NechEnergyUtility.CapacitorCapacity(pawn);
        if (capacity <= 0f)
            capacity = 1f;

        bool showDevAdjust = DebugSettings.ShowDevGizmos && DebugSettings.godMode;
        HediffComp_GaussCapacitor cap = NechEnergyUtility.GetCapacitorComp(pawn);
        bool hasCap = cap != null;

        float devStripOuter = showDevAdjust ? DevBtnSize + BarDevGap : 0f;
        float devStackH = showDevAdjust ? DevBtnSize * 2f + DevBtnGap : 0f;
        float barRowH = Mathf.Max(BarH, devStackH);
        float headerH = hasCap ? ToggleIcon : 16f;
        // Inner fits header + gap + bar row + bottom pad (taller panel + thicker bar).
        float innerContentH = headerH + HeaderBarGap + barRowH + BarBottomPad;
        float totalH = innerContentH + 12f;
        // Grow upward: share bottom edge with standard 75px gizmo row.
        Rect outRect = new Rect(topLeft.x, topLeft.y + StandardGizmoRowH - totalH, w, totalH);
        Widgets.DrawWindowBackground(outRect);
        if (_overlayTex != null)
        {
            float ow = _overlayTex.width * 0.25f;
            float oh = _overlayTex.height * 0.25f;
            Rect overlayRect = new Rect(outRect.x, outRect.y, ow, oh);
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(overlayRect, _overlayTex);
            GUI.color = prevColor;
        }
        Rect inner = outRect.ContractedBy(6f);

        float barRowBottom = inner.yMax - BarBottomPad;
        float barRowTop = barRowBottom - barRowH;

        Rect bar = new Rect(
            inner.x,
            barRowTop + (barRowH - BarH) * 0.5f,
            Mathf.Max(8f, inner.width - devStripOuter),
            BarH);

        Rect plusRect = Rect.zero;
        Rect minusRect = Rect.zero;
        if (showDevAdjust)
        {
            float devX = inner.xMax - DevBtnSize;
            plusRect = new Rect(devX, barRowTop, DevBtnSize, DevBtnSize);
            minusRect = new Rect(devX, barRowTop + DevBtnSize + DevBtnGap, DevBtnSize, DevBtnSize);
        }

        // One header row: label left, core/battery right (spaced above the bar row).
        Rect headerRow = new Rect(inner.x, barRowTop - HeaderBarGap - headerH, inner.width, headerH);
        float togglesW = hasCap ? ToggleIcon * 2f + ToggleGap : 0f;
        Rect titleLabelRect = new Rect(headerRow.x + 2f, headerRow.y, headerRow.width - togglesW - 4f, headerH);

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(titleLabelRect, "GW40K_NechEnergyLabel".Translate().Resolve());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        float right = headerRow.xMax;
        Rect battRect = default;
        Rect coreRect = default;
        if (hasCap)
        {
            right -= ToggleIcon;
            coreRect = new Rect(right, headerRow.y + (headerH - ToggleIcon) * 0.5f, ToggleIcon, ToggleIcon);
            right -= ToggleGap + ToggleIcon;
            battRect = new Rect(right, headerRow.y + (headerH - ToggleIcon) * 0.5f, ToggleIcon, ToggleIcon);
        }

        bool absorbed = false;
        if (hasCap)
        {
            bool allowBattery = cap.allowBatteryCharge;
            bool allowCore = cap.allowCoreCharge;
            Texture2D battTex = ContentFinder<Texture2D>.Get(allowBattery ? TexBattOn : TexBattOff, false) ?? BaseContent.BadTex;
            Texture2D coreTex = ContentFinder<Texture2D>.Get(allowCore ? TexCoreOn : TexCoreOff, false) ?? BaseContent.BadTex;

            if (Widgets.ButtonImage(battRect, battTex))
            {
                cap.allowBatteryCharge = !cap.allowBatteryCharge;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                absorbed = true;
            }

            TooltipHandler.TipRegion(battRect, "GW40K_NechEnergyAllowBatteryDesc".Translate().Resolve());

            if (Widgets.ButtonImage(coreRect, coreTex))
            {
                cap.allowCoreCharge = !cap.allowCoreCharge;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                absorbed = true;
            }

            TooltipHandler.TipRegion(coreRect, "GW40K_NechEnergyAllowCoreDesc".Translate().Resolve());
        }

        Rect plusHit = showDevAdjust ? plusRect.ExpandedBy(DevHitSlop) : Rect.zero;
        Rect minusHit = showDevAdjust ? minusRect.ExpandedBy(DevHitSlop) : Rect.zero;

        // Apply dev +/- here: GizmoGridDrawer's ProcessInput + Event.mousePosition can disagree with
        // these rects (GUI scale / event reuse). Commands work because they are the interacted gizmo.
        if (TryHandleDevGaussClick(n, plusHit, minusHit))
        {
            Event.current.Use();
            return new GizmoResult(Mouse.IsOver(outRect) ? GizmoState.Mouseover : GizmoState.Clear);
        }

        if (showDevAdjust)
        {
            if (Mouse.IsOver(plusRect))
                Widgets.DrawHighlight(plusRect);
            Widgets.DrawTextureFitted(plusRect, TexButton.Plus, 1f);
            TooltipHandler.TipRegion(plusHit, "DEV: +25% gauss reserve");
            if (Mouse.IsOver(minusRect))
                Widgets.DrawHighlight(minusRect);
            Widgets.DrawTextureFitted(minusRect, TexButton.Minus, 1f);
            TooltipHandler.TipRegion(minusHit, "DEV: -25% gauss reserve");
        }

        Widgets.FillableBar(bar, level, SolidColorMaterials.NewSolidColorTexture(DarkGreenFill), BaseContent.BlackTex, true);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        float current = Mathf.Round(capacity * Mathf.Clamp01(level));
        Widgets.Label(bar, $"{current:0}/{capacity:0}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        bool overBatt = hasCap && Mouse.IsOver(battRect);
        bool overCore = hasCap && Mouse.IsOver(coreRect);
        bool overDev = showDevAdjust && (Mouse.IsOver(plusHit) || Mouse.IsOver(minusHit));
        if (!overBatt && !overCore && !overDev && !absorbed)
        {
            string reserveText = $"{current:0}/{capacity:0}";
            string tip = "GW40K_NechEnergyTip".Translate(reserveText, capacity.ToString("0")).Resolve();
            TooltipHandler.TipRegion(outRect, tip);
        }

        GizmoState endState = absorbed
            ? GizmoState.Interacted
            : Mouse.IsOver(outRect)
                ? GizmoState.Mouseover
                : GizmoState.Clear;
        return absorbed
            ? new GizmoResult(endState, Event.current)
            : new GizmoResult(endState);
    }

    /// <summary>True if this event was a left-click on dev +/- and we applied a change (or would have if need existed).</summary>
    private static bool TryHandleDevGaussClick(Need need, Rect plusHit, Rect minusHit)
    {
        if (!DebugSettings.ShowDevGizmos || !DebugSettings.godMode)
            return false;
        if (plusHit.width <= 0f)
            return false;
        Event ev = Event.current;
        if (ev == null || ev.button != 0)
            return false;
        if (ev.type != EventType.MouseDown && ev.rawType != EventType.MouseDown)
            return false;

        Vector2 mp = ev.mousePosition;
        if (plusHit.Contains(mp))
        {
            if (need != null)
            {
                AdjustGaussEnergy(need, DevAdjustStep);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            return true;
        }

        if (minusHit.Contains(mp))
        {
            if (need != null)
            {
                AdjustGaussEnergy(need, -DevAdjustStep);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            return true;
        }

        return false;
    }

    private static void AdjustGaussEnergy(Need need, float delta)
    {
        if (need == null)
            return;
        need.CurLevel = Mathf.Clamp01(need.CurLevel + delta);
    }
}
