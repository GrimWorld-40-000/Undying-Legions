using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

[StaticConstructorOnStartup]
public class Gizmo_ControlNodeBandwidth : Gizmo
{
    private static readonly Color NodeGreen = new Color(0.12f, 0.86f, 0.20f);
    private static readonly Color SegmentEmpty = new Color(0.18f, 0.18f, 0.18f);
    private static readonly Color SegmentReserved = new Color(0.9f, 0.65f, 0.1f);
    private static readonly Texture2D OverlayTex;

    private const float StandardGizmoRowH = 75f;
    private const float GizmoW = 140f;
    /// <summary>Panel height: title, bandwidth, bar, Return (half-width right), mode row, caption with bottom padding.</summary>
    private const float MainH = 144f;
    private const float PortraitSize = 36f;
    private const float PortraitGap = 2f;
    private const float PortraitsPerRow = 3f;
    private const float SegmentGap = 2f;
    private const float ModeButtonH = 16f;

    static Gizmo_ControlNodeBandwidth()
    {
        OverlayTex = ContentFinder<Texture2D>.Get("UI/GW40K_Necron_overlayF", false);
    }

    private readonly HediffComp_ControlNodeTracker tracker;
    private const string SpyderUncontrolledReason = "Spyder must be under command to issue control-node orders.";

    public Gizmo_ControlNodeBandwidth(HediffComp_ControlNodeTracker tracker)
    {
        this.tracker = tracker;
    }

    public override float GetWidth(float maxWidth) => Mathf.Min(GizmoW, maxWidth);

    private float PortraitPanelHeight()
    {
        int count = tracker.controlledScarabs.Count;
        if (count == 0)
            return 0f;
        int rows = Mathf.CeilToInt(count / PortraitsPerRow);
        return rows * (PortraitSize + PortraitGap);
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        if (NecronDefOfs.GW_UD_Concept_ControlNode != null)
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(NecronDefOfs.GW_UD_Concept_ControlNode, KnowledgeAmount.FrameDisplayed);

        float w = GetWidth(maxWidth);
        float portraitH = PortraitPanelHeight();

        Rect main = new Rect(topLeft.x, topLeft.y + StandardGizmoRowH - MainH, w, MainH);
        Widgets.DrawWindowBackground(main);
        DrawOverlay(main);

        bool portraitClickAbsorbed = false;
        if (portraitH > 0f)
        {
            Rect portraitPanel = new Rect(topLeft.x, main.y - portraitH, w, portraitH);
            Widgets.DrawWindowBackground(portraitPanel);
            portraitClickAbsorbed = DrawPortraits(portraitPanel);
        }

        bool modeChanged = DrawMainPanel(main, tracker);

        float hitTop = portraitH > 0f ? main.y - portraitH : main.y;
        float hitH = portraitH + MainH;
        Rect unionHit = new Rect(topLeft.x, hitTop, w, hitH);

        GizmoState state = (portraitClickAbsorbed || modeChanged)
            ? GizmoState.Interacted
            : Mouse.IsOver(unionHit)
                ? GizmoState.Mouseover
                : GizmoState.Clear;

        return new GizmoResult(state);
    }

    internal static float StackedPanelHeight() => MainH;

    internal static bool DrawMainPanel(Rect main, HediffComp_ControlNodeTracker tracker)
    {
        bool changed = false;
        bool commandsDisabled = ControlNodeCommandsDisabled(tracker);

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(main.x + 4f, main.y + 4f, main.width - 8f, 16f), "Control Node");
        Text.Anchor = TextAnchor.UpperLeft;

        int used = Mathf.RoundToInt(tracker.BandwidthUsed);
        int max = Mathf.Max(1, tracker.BandwidthMax);
        bool replicating = GetFabricatorReplicating(tracker.CommanderPawn);
        int reserved = replicating ? 1 : 0;
        string bwLabel = replicating
            ? $"{used} / {max} bandwidth (+1)"
            : $"{used} / {max} bandwidth";
        Widgets.Label(new Rect(main.x + 4f, main.y + 22f, main.width - 8f, 16f), bwLabel);

        float segmentsY = main.y + 42f;
        DrawSegmentedBandwidthBar(new Rect(main.x + 6f, segmentsY, main.width - 12f, 14f), used, max, reserved);

        float controlsY = segmentsY + 18f;
        float innerPad = 4f;
        float innerW = main.width - innerPad * 2f;
        float returnW = innerW * 0.5f;
        Rect commandCaptionRect = new Rect(main.x + innerPad, controlsY, innerW, 16f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(commandCaptionRect, "Command:");
        Text.Anchor = TextAnchor.UpperLeft;

        Rect returnRect = new Rect(main.xMax - innerPad - returnW, commandCaptionRect.yMax + 2f, returnW, ModeButtonH);
        changed |= DrawReturnButton(returnRect, tracker, commandsDisabled);

        Rect modeCaptionRect = new Rect(main.x + innerPad, returnRect.yMax + 2f, innerW, 16f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(modeCaptionRect, "Mode:");
        Text.Anchor = TextAnchor.UpperLeft;

        Rect modeButtonRect = new Rect(main.x + innerPad, modeCaptionRect.yMax + 2f, innerW, ModeButtonH);
        changed |= DrawModeCycleControl(modeButtonRect, tracker, commandsDisabled);
        Text.Font = GameFont.Small;

        TooltipHandler.TipRegion(main,
            $"Control Node links Canoptek scarabs.\nRange: {tracker.ControlRange:0.#}\nBandwidth tiers: standard 3, Cryptek 4, Spyder 6.");

        return changed;
    }

    private static void DrawOverlay(Rect main)
    {
        if (OverlayTex == null)
            return;

        float ow = OverlayTex.width * 0.25f;
        float oh = OverlayTex.height * 0.25f;
        Rect overlayRect = new Rect(main.x, main.y, ow, oh);
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(overlayRect, OverlayTex);
        GUI.color = prev;
    }

    private static void DrawSegmentedBandwidthBar(Rect rect, int used, int max, int reserved = 0)
    {
        max = Mathf.Max(1, max);
        float totalGap = (max - 1) * SegmentGap;
        float segmentW = Mathf.Max(4f, (rect.width - totalGap) / max);
        for (int i = 0; i < max; i++)
        {
            Color color = i < used ? NodeGreen
                : i < used + reserved ? SegmentReserved
                : SegmentEmpty;
            Rect seg = new Rect(rect.x + i * (segmentW + SegmentGap), rect.y, segmentW, rect.height);
            Widgets.DrawBoxSolid(seg, color);
        }
    }

    private static bool GetFabricatorReplicating(Pawn spyder)
    {
        if (spyder == null) return false;
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_HiveFabricator");
        if (def == null) return false;
        HediffComp_HiveFabricator fab = spyder.health.hediffSet
            .GetFirstHediffOfDef(def)?.TryGetComp<HediffComp_HiveFabricator>();
        return fab?.IsReplicating == true;
    }

    internal static bool DrawModeCycleControl(Rect rect, HediffComp_ControlNodeTracker tracker)
    {
        return DrawModeCycleControl(rect, tracker, disabled: false);
    }

    internal static bool DrawModeCycleControl(Rect rect, HediffComp_ControlNodeTracker tracker, bool disabled)
    {
        string buttonLabel = ModeLabel(tracker.mode);
        if (disabled)
        {
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.ButtonText(rect, buttonLabel);
            GUI.color = prevColor;
            TooltipHandler.TipRegion(rect, SpyderUncontrolledReason);
            return false;
        }

        // Right click → previous mode (must be checked before ButtonText consumes MouseDown)
        if (Event.current.type == EventType.MouseDown
            && Event.current.button == 1
            && rect.Contains(Event.current.mousePosition))
        {
            tracker.SetMode(PrevMode(tracker.mode));
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            Event.current.Use();
            return true;
        }

        // Left click → next mode
        if (Widgets.ButtonText(rect, buttonLabel))
        {
            tracker.SetMode(NextMode(tracker.mode));
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            return true;
        }

        return false;
    }

    private static bool DrawReturnButton(Rect rect, HediffComp_ControlNodeTracker tracker, bool disabled)
    {
        if (disabled)
        {
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.ButtonText(rect, "Return");
            GUI.color = prevColor;
            TooltipHandler.TipRegion(rect, SpyderUncontrolledReason);
            return false;
        }
        if (!Widgets.ButtonText(rect, "Return"))
            return false;

        Pawn commander = tracker.CommanderPawn;
        if (commander == null || !commander.Spawned || commander.Map == null)
            return false;

        IntVec3 rallyCell = commander.Position;
        for (int i = 0; i < tracker.controlledScarabs.Count; i++)
        {
            Pawn scarab = tracker.controlledScarabs[i];
            if (scarab == null || scarab.Dead || scarab.Destroyed || !scarab.Spawned || scarab.Map != commander.Map)
                continue;

            Job job = JobMaker.MakeJob(JobDefOf.Goto, rallyCell);
            job.playerForced = true;
            // Match Swarm: execute immediately instead of queueing behind current job.
            scarab.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
        }

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        return true;
    }

    private static bool ControlNodeCommandsDisabled(HediffComp_ControlNodeTracker tracker)
    {
        Pawn commander = tracker?.CommanderPawn;
        if (commander == null)
            return false;
        if (!ControlNodeUtility.IsSpyder(commander))
            return false;
        return HediffComp_NecronCommandTracker.GetCommanderOf(commander) == null;
    }

    internal static ControlNodeMode NextMode(ControlNodeMode mode) =>
        mode switch
        {
            ControlNodeMode.Consume => ControlNodeMode.Repair,
            ControlNodeMode.Repair  => ControlNodeMode.Work,
            ControlNodeMode.Work    => ControlNodeMode.Produce,
            ControlNodeMode.Produce => ControlNodeMode.Combat,
            ControlNodeMode.Combat  => ControlNodeMode.Defend,
            ControlNodeMode.Defend  => ControlNodeMode.Consume,
            _ => ControlNodeMode.Consume
        };

    internal static ControlNodeMode PrevMode(ControlNodeMode mode) =>
        mode switch
        {
            ControlNodeMode.Repair   => ControlNodeMode.Consume,
            ControlNodeMode.Work     => ControlNodeMode.Repair,
            ControlNodeMode.Produce  => ControlNodeMode.Work,
            ControlNodeMode.Combat   => ControlNodeMode.Produce,
            ControlNodeMode.Defend   => ControlNodeMode.Combat,
            ControlNodeMode.Consume  => ControlNodeMode.Defend,
            _ => ControlNodeMode.Consume
        };

    internal static string ModeLabel(ControlNodeMode mode) =>
        mode switch
        {
            ControlNodeMode.Consume => "Consume",
            ControlNodeMode.Repair  => "Repair",
            ControlNodeMode.Work    => "Work",
            ControlNodeMode.Produce => "Produce",
            ControlNodeMode.Combat  => "Combat",
            ControlNodeMode.Defend  => "Defend",
            _ => "Consume"
        };

    internal static string ModeDescription(ControlNodeMode mode) =>
        (mode switch
        {
            ControlNodeMode.Consume => "GW40K_CanoptekModeConsumeDesc",
            ControlNodeMode.Repair  => "GW40K_CanoptekModeRepairDesc",
            ControlNodeMode.Work    => "GW40K_CanoptekModeWorkDesc",
            ControlNodeMode.Produce => "GW40K_CanoptekModeProduceDesc",
            ControlNodeMode.Combat  => "GW40K_CanoptekModeCombatDesc",
            ControlNodeMode.Defend  => "GW40K_CanoptekModeDefendDesc",
            _ => "GW40K_CanoptekModeConsumeDesc"
        }).Translate().Resolve();

    private bool DrawPortraits(Rect panel)
    {
        float x = panel.x + 4f;
        float y = panel.y + PortraitGap;

        for (int i = 0; i < tracker.controlledScarabs.Count; i++)
        {
            Pawn scarab = tracker.controlledScarabs[i];
            if (scarab == null || scarab.Dead || scarab.Destroyed)
                continue;

            if (x + PortraitSize > panel.xMax - 4f)
            {
                x = panel.x + 4f;
                y += PortraitSize + PortraitGap;
            }

            Rect r = new Rect(x, y, PortraitSize, PortraitSize);
            RenderTexture portrait = PortraitsCache.Get(
                scarab,
                new Vector2(PortraitSize, PortraitSize),
                Rot4.South,
                default,
                1f);
            GUI.DrawTexture(r, portrait);
            TooltipHandler.TipRegion(r, scarab.LabelCap);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(r))
            {
                CameraJumper.TryJumpAndSelect(scarab);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                Event.current.Use();
                return true;
            }

            x += PortraitSize + PortraitGap;
        }

        return false;
    }
}
