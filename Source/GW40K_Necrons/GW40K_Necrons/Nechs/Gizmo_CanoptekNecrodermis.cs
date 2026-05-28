using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

public class Gizmo_CanoptekNecrodermis : Gizmo
{
    private static readonly Color FillColor = new Color(0.22f, 0.88f, 0.30f);
    private static readonly Color EmptyColor = new Color(0.12f, 0.12f, 0.12f);

    private const float GizmoW = 140f;
    private const float MainH = 98f;
    private const float BarH = 14f;
    private const float ModeButtonH = 16f;
    private const float LabelW = 36f;
    private const float AutoButtonW = 30f;
    private const float ButtonGap = 4f;
    private static readonly Color AutoOnColor = new Color(0.22f, 0.88f, 0.30f);

    private readonly Pawn pawn;
    private readonly Need_Necrodermis need;

    public Gizmo_CanoptekNecrodermis(Pawn pawn, Need_Necrodermis need)
    {
        this.pawn = pawn;
        this.need = need;
    }

    public override float GetWidth(float maxWidth) => Mathf.Min(GizmoW, maxWidth);

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        if (NecronDefOfs.GW_UD_Concept_ControlNode != null)
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(NecronDefOfs.GW_UD_Concept_ControlNode, KnowledgeAmount.FrameDisplayed);

        ThingComp_CanoptekConsumePolicy consumeComp = pawn.TryGetComp<ThingComp_CanoptekConsumePolicy>();
        bool linkedByNode = consumeComp?.IsLinkedByCommandNode() == true;
        Rect main = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), MainH);
        Widgets.DrawWindowBackground(main);

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(main.x + 4f, main.y + 4f, main.width - 8f, 16f), "Necrodermis");
        Text.Anchor = TextAnchor.UpperLeft;

        float cur = Mathf.Max(0f, need.CurLevel);
        float max = Mathf.Max(0.001f, need.MaxLevel);
        float pct = Mathf.Clamp01(cur / max) * 100f;
        Widgets.Label(new Rect(main.x + 4f, main.y + 22f, main.width - 8f, 16f), $"{pct:0.0}%");

        Rect barRect = new Rect(main.x + 6f, main.y + 42f, main.width - 12f, BarH);
        Widgets.FillableBar(
            barRect,
            Mathf.Clamp01(cur / max),
            SolidColorMaterials.NewSolidColorTexture(FillColor),
            SolidColorMaterials.NewSolidColorTexture(EmptyColor),
            true);

        ControlNodeMode localMode = GetLocalMode();
        bool autoOn = GameComponent_CanoptekConstructModes.Current?.GetAutoMode(pawn) ?? false;
        Rect modeRow = new Rect(main.x + 4f, main.y + 62f, main.width - 8f, ModeButtonH);
        bool modeChanged = DrawModeRow(modeRow, localMode, linkedByNode);

        TooltipHandler.TipRegion(main, () => BuildPanelTip(localMode, linkedByNode, autoOn), main.GetHashCode());

        Text.Font = GameFont.Small;
        GizmoState state = modeChanged
            ? GizmoState.Interacted
            : Mouse.IsOver(main) ? GizmoState.Mouseover : GizmoState.Clear;
        return new GizmoResult(state);
    }

    private ControlNodeMode GetLocalMode()
    {
        return GameComponent_CanoptekConstructModes.Current?.GetMode(pawn, ControlNodeMode.Consume) ?? ControlNodeMode.Consume;
    }

    private static string BuildPanelTip(ControlNodeMode mode, bool linkedByNode, bool autoOn)
    {
        if (!linkedByNode)
            return "GW40K_CanoptekModeRequiresLink".Translate().Resolve();

        string tip = Gizmo_ControlNodeBandwidth.ModeDescription(mode);
        tip += "\n\n" + (autoOn
            ? "GW40K_CanoptekAutoOnDesc"
            : "GW40K_CanoptekAutoOffDesc").Translate().Resolve();
        return tip;
    }

    private bool DrawModeRow(Rect rect, ControlNodeMode localMode, bool linkedByNode)
    {
        const float gap = 4f;
        Rect labelRect = new Rect(rect.x, rect.y, LabelW, rect.height);
        float remainingW = rect.width - LabelW - gap;
        float modeW = remainingW - ButtonGap - AutoButtonW;
        Rect modeButtonRect = new Rect(labelRect.xMax + gap, rect.y, modeW, rect.height);
        Rect autoButtonRect = new Rect(modeButtonRect.xMax + ButtonGap, rect.y, AutoButtonW, rect.height);
        Widgets.Label(labelRect, "Mode:");

        bool changed = false;

        // Mode cycle button — left click: next mode, right click: previous mode.
        string modeLabel = Gizmo_ControlNodeBandwidth.ModeLabel(localMode);
        ControlNodeMode nextMode = Gizmo_ControlNodeBandwidth.NextMode(localMode);
        ControlNodeMode prevMode = Gizmo_ControlNodeBandwidth.PrevMode(localMode);
        bool produceBlockedNext = nextMode == ControlNodeMode.Produce && !IsAtFullLife(pawn);
        bool produceBlockedPrev = prevMode == ControlNodeMode.Produce && !IsAtFullLife(pawn);

        if (!linkedByNode)
            GUI.color = Color.gray;

        // Right click → previous mode (must be checked before ButtonText consumes MouseDown)
        if (linkedByNode
            && Event.current.type == EventType.MouseDown
            && Event.current.button == 1
            && modeButtonRect.Contains(Event.current.mousePosition)
            && !produceBlockedPrev)
        {
            GameComponent_CanoptekConstructModes.Current?.SetMode(pawn, prevMode);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            Event.current.Use();
            changed = true;
        }

        // Left-click handler via ButtonText.
        bool modeClicked = Widgets.ButtonText(modeButtonRect, modeLabel);
        GUI.color = Color.white;

        if (!linkedByNode)
            TooltipHandler.TipRegion(modeButtonRect, "GW40K_CanoptekModeRequiresLink".Translate());
        else if (produceBlockedNext && produceBlockedPrev)
            TooltipHandler.TipRegion(modeButtonRect, "GW40K_Produce_NotFullLife".Translate());

        if (linkedByNode)
        {
            // Left click → next mode
            if (modeClicked && !produceBlockedNext)
            {
                GameComponent_CanoptekConstructModes.Current?.SetMode(pawn, nextMode);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                changed = true;
            }
        }

        // Auto toggle button
        bool autoOn = GameComponent_CanoptekConstructModes.Current?.GetAutoMode(pawn) ?? false;
        if (!linkedByNode)
            GUI.color = Color.gray;
        else if (autoOn)
            GUI.color = AutoOnColor;
        bool autoClicked = Widgets.ButtonText(autoButtonRect, "Auto");
        GUI.color = Color.white;
        if (!linkedByNode)
            TooltipHandler.TipRegion(autoButtonRect, "GW40K_CanoptekModeRequiresLink".Translate());
        if (autoClicked && linkedByNode)
        {
            GameComponent_CanoptekConstructModes.Current?.SetAutoMode(pawn, !autoOn);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            changed = true;
        }

        return changed;
    }

    private static bool IsAtFullLife(Pawn pawn)
    {
        int total = HarmonyPatch_ScarabSwarmChassis.ScarabUnitSlotCount(pawn);
        if (total <= 0) return false;
        int present = 0;
        foreach (BodyPartRecord p in pawn.health.hediffSet.GetNotMissingParts())
            if (p.def.defName == HarmonyPatch_ScarabSwarmChassis.ScarabUnitPartDefName)
                present++;
        return present == total;
    }
}
