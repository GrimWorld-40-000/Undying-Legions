using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Full Necron oversight panel — mirrors the vanilla Mechs tab in structure.
/// Opened via the "Necrons" button injected into the vanilla Mechs tab.
///
/// Layout (top → bottom inside the window):
///   ┌─────────────────────────────────────────────────────────┐
///   │ [Mechs] │ [All Necrons] │ [Group 1] │ [Group 2]        │  ← filter tabs
///   │ Name    │ Core Flux     │ Necrodermis │ … columns …     │  ← header row
///   ├─────────────────────────────────────────────────────────┤
///   │ ▸ Commander: Urakmeth                                   │  scroll
///   │     Warrior  ████░░░ ████░░░ ✓ Urakmeth  Grp1  Work  … │
///   │     Immortal ████░░░ ████░░░ □ Urakmeth  –     Hold  … │
///   │ ▸ Unbound                                               │
///   │     …                                                   │
///   └─────────────────────────────────────────────────────────┘
///
/// Close behaviour: click outside (closeOnClickedOutside=true) or right-click outside.
/// </summary>
public class Window_NechPanel : Window
{
    // ── Singleton ────────────────────────────────────────────────────────────

    private static Window_NechPanel _current;

    public static new bool IsOpen =>
        _current != null && Find.WindowStack != null && Find.WindowStack.IsOpen(_current);

    public static void Toggle()
    {
        if (IsOpen)
            _current.Close();
        else
            Find.WindowStack.Add(_current = new Window_NechPanel());
    }

    public override void Close(bool doCloseSound = true)
    {
        _current = null;
        base.Close(doCloseSound);
    }

    // ── Position / size ───────────────────────────────────────────────────────

    private static MainTabWindow MechTab =>
        DefDatabase<MainButtonDef>.GetNamedSilentFail("Mechs")?.TabWindow;

    public override Vector2 InitialSize =>
        MechTab?.RequestedTabSize ?? new Vector2(UI.screenWidth, 260f);

    protected override void SetInitialSizeAndPosition()
    {
        MainTabWindow mech = MechTab;
        if (mech != null)
            windowRect = mech.windowRect;
        else
        {
            Vector2 sz = InitialSize;
            windowRect = new Rect(0f, UI.screenHeight - 35f - sz.y, sz.x, sz.y);
        }
    }

    // ── UI state ──────────────────────────────────────────────────────────────

    /// <summary>-1 = all necrons; 0/1 = command group index filter.</summary>
    private int _filterGroup = -1;
    private Vector2 _scrollPos = Vector2.zero;

    // ── Column widths ─────────────────────────────────────────────────────────

    private const float RowH       = 32f;
    private const float HeaderH    = 26f;
    private const float TabH       = 28f;
    private const float CmdHeaderH = 26f;
    private const float ColGap     = 4f;

    // Fixed-width columns — totals to ~820px; remaining goes to margins/area col.
    private const float ColPortrait    = 28f;
    private const float ColName        = 155f;
    private const float ColEnergy      = 88f;
    private const float ColNecrodermis = 88f;
    private const float ColDraft       = 38f;
    private const float ColCommander   = 130f;
    private const float ColGroup       = 80f;
    private const float ColMode        = 88f;
    private const float ColArea        = 100f;

    // Derived total for inner-content rows
    private static float TotalColsWidth =>
        ColPortrait + ColName + ColEnergy + ColNecrodermis + ColDraft +
        ColCommander + ColGroup + ColMode + ColArea + ColGap * 8f;

    // ── Bar colours ──────────────────────────────────────────────────────────

    /// <summary>Deep green fill for Core Flux bar (user spec).</summary>
    private static readonly Color CoreFluxFill    = new Color(0.08f, 0.72f, 0.16f);
    /// <summary>Deep green background track for Core Flux bar.</summary>
    private static readonly Color CoreFluxBg      = new Color(0.03f, 0.22f, 0.06f);
    /// <summary>Gray-green fill for Necrodermis bar (user spec).</summary>
    private static readonly Color NecrodermsFill  = new Color(0.33f, 0.60f, 0.28f);
    /// <summary>Gray-green background track for Necrodermis bar.</summary>
    private static readonly Color NecrodermisBg   = new Color(0.12f, 0.22f, 0.10f);

    // ── Constructor ──────────────────────────────────────────────────────────

    public Window_NechPanel()
    {
        doCloseX                = false;
        closeOnClickedOutside   = true;
        absorbInputAroundWindow = false;
        preventCameraMotion     = false;
        resizeable              = false;
        draggable               = false;
        doWindowBackground      = true;
    }

    // ── Right-click close ─────────────────────────────────────────────────────

    public override void ExtraOnGUI()
    {
        base.ExtraOnGUI();
        // Close on right-click *outside* the window (matches vanilla float menu behaviour).
        if (Event.current.type == EventType.MouseDown
            && Event.current.button == 1
            && !windowRect.Contains(UI.MousePositionOnUIInverted))
        {
            Close();
        }
    }

    // ── DoWindowContents ─────────────────────────────────────────────────────

    public override void DoWindowContents(Rect inRect)
    {
        NecronCommandGroupManager mgr = NecronCommandGroupManager.Instance;

        // ── Top bar ──────────────────────────────────────────────────────────
        float topY = inRect.y;
        DrawTopBar(new Rect(inRect.x, topY, inRect.width, TabH));
        topY += TabH + 2f;

        // ── Column header ────────────────────────────────────────────────────
        Rect headerRect = new Rect(inRect.x, topY, inRect.width, HeaderH);
        Widgets.DrawMenuSection(headerRect);
        DrawColumnHeaders(headerRect);
        topY += HeaderH;

        // ── Scroll area ───────────────────────────────────────────────────────
        Rect scrollOuter = new Rect(inRect.x, topY, inRect.width, inRect.yMax - topY);
        DrawScrollContent(scrollOuter, mgr);
    }

    // ── Top bar (tabs + Mechs back-button) ────────────────────────────────────

    private void DrawTopBar(Rect bar)
    {
        float btnW = 120f;
        float gap  = 4f;
        float x    = bar.x + 4f;

        // [← Mechs] — closes the necrons panel, revealing the vanilla mechs tab
        Rect mechsBtn = new Rect(x, bar.y + (bar.height - 24f) * 0.5f, btnW, 24f);
        if (Widgets.ButtonText(mechsBtn, "GW40K_NechPanel_MechsBack".Translate()))
            Close();
        TooltipHandler.TipRegion(mechsBtn, "GW40K_NechPanel_MechsBackTip".Translate());
        x += btnW + gap * 2f;

        // Separator
        Widgets.DrawLineVertical(x, bar.y + 4f, bar.height - 8f);
        x += gap * 2f;

        // [All Necrons]
        DrawFilterTab(ref x, bar, -1, "GW40K_NechPanel_AllNecrons".Translate(), btnW, gap);

        // [Group 1] [Group 2]
        for (int i = 0; i < NecronCommandGroupManager.GroupCount; i++)
        {
            NecronCommandGroupManager mgr = NecronCommandGroupManager.Instance;
            string label = mgr?.GetLabel(i) ?? $"Group {i + 1}";
            DrawFilterTab(ref x, bar, i, label, btnW, gap);
        }
    }

    private void DrawFilterTab(ref float x, Rect bar, int groupIndex, string label, float btnW, float gap)
    {
        Rect btn = new Rect(x, bar.y + (bar.height - 24f) * 0.5f, btnW, 24f);
        bool active = _filterGroup == groupIndex;
        if (active)
            Widgets.DrawHighlight(btn);
        if (Widgets.ButtonText(btn, label))
        {
            _filterGroup = groupIndex;
            _scrollPos   = Vector2.zero;
        }
        x += btnW + gap;
    }

    // ── Column headers ────────────────────────────────────────────────────────

    private void DrawColumnHeaders(Rect rect)
    {
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        float x = rect.x + 6f;

        x += ColPortrait + ColGap; // portrait column has no label

        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColName".Translate(),        ColName);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColCoreFlux".Translate(),    ColEnergy);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColNecrodermis".Translate(), ColNecrodermis);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColDraft".Translate(),       ColDraft);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColCommander".Translate(),   ColCommander);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColGroup".Translate(),       ColGroup);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColMode".Translate(),        ColMode);
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColArea".Translate(),        ColArea);

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;
    }

    private static void DrawHeaderLabel(ref float x, Rect parent, string label, float colW)
    {
        Widgets.Label(new Rect(x + 2f, parent.y, colW - 4f, parent.height), label);
        x += colW + ColGap;
    }

    // ── Scroll content ────────────────────────────────────────────────────────

    private void DrawScrollContent(Rect outer, NecronCommandGroupManager mgr)
    {
        // Build grouped data
        var grouped = BuildGroupedData(mgr);

        // Calculate virtual content height
        float contentH = 0f;
        foreach (var (_, mechs) in grouped)
            contentH += CmdHeaderH + mechs.Count * RowH;

        if (contentH < outer.height)
            contentH = outer.height;

        Rect view = new Rect(0f, 0f, outer.width - 16f, contentH); // -16 for scrollbar
        Widgets.BeginScrollView(outer, ref _scrollPos, view);

        float y = 0f;
        bool alt = false; // alternating row shading

        foreach (var (commander, mechs) in grouped)
        {
            // ── Commander header row ──────────────────────────────────────────
            Rect cmdHdr = new Rect(view.x, y, view.width, CmdHeaderH);
            Widgets.DrawMenuSection(cmdHdr);
            DrawCommanderHeader(cmdHdr, commander);
            y += CmdHeaderH;

            // ── Pawn rows ────────────────────────────────────────────────────
            foreach (Pawn nech in mechs)
            {
                Rect row = new Rect(view.x, y, view.width, RowH);
                if (alt) Widgets.DrawAltRect(row);
                DrawNechRow(row, nech, mgr);
                y   += RowH;
                alt  = !alt;
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawCommanderHeader(Rect rect, Pawn commander)
    {
        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        float indent = 8f;

        string headerLabel;
        if (commander != null)
        {
            HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
            string bwStr = tracker != null
                ? $"  ({(int)tracker.BandwidthUsed}/{(int)tracker.BandwidthMax} bandwidth)"
                : string.Empty;
            headerLabel = $"▸ {commander.LabelShortCap}{bwStr}";
        }
        else
        {
            headerLabel = "▸ " + "GW40K_NechPanel_Unbound".Translate();
        }

        Widgets.Label(new Rect(rect.x + indent, rect.y, rect.width - indent * 2f, rect.height), headerLabel);

        // Click on commander header → select/jump to commander
        if (commander != null && Widgets.ButtonInvisible(rect))
            CameraJumper.TryJumpAndSelect(commander);

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;
    }

    // ── Per-pawn row ─────────────────────────────────────────────────────────

    private void DrawNechRow(Rect row, Pawn nech, NecronCommandGroupManager mgr)
    {
        // Hover highlight
        if (Mouse.IsOver(row))
            Widgets.DrawHighlight(row);

        float x       = row.x + 6f;
        float rowMidY = row.y + row.height * 0.5f;

        // ── Portrait ─────────────────────────────────────────────────────────
        Rect pr = new Rect(x, rowMidY - ColPortrait * 0.5f, ColPortrait, ColPortrait);
        if (nech.Spawned)
        {
            RenderTexture portrait = PortraitsCache.Get(
                nech, new Vector2(ColPortrait, ColPortrait), Rot4.South, default, 1.0f);
            GUI.DrawTexture(pr, portrait);
        }
        // Click portrait → jump to pawn
        if (Widgets.ButtonInvisible(pr))
            CameraJumper.TryJumpAndSelect(nech);
        TooltipHandler.TipRegion(pr, nech.LabelCap);
        x += ColPortrait + ColGap;

        // ── Name ─────────────────────────────────────────────────────────────
        Rect nameRect = new Rect(x, row.y + 2f, ColName, row.height - 4f);
        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(nameRect, nech.LabelShortCap);
        if (Widgets.ButtonInvisible(nameRect))
            CameraJumper.TryJumpAndSelect(nech);
        Text.Anchor = TextAnchor.UpperLeft;
        x += ColName + ColGap;

        // ── Core Flux bar ────────────────────────────────────────────────────
        Rect efRect = new Rect(x, rowMidY - 7f, ColEnergy, 14f);
        DrawNeedBar(efRect, nech, NecronDefOfs.GW40K_CoreFlux, CoreFluxFill, CoreFluxBg,
                    "GW40K_NechPanel_CoreFluxTip".Translate());
        x += ColEnergy + ColGap;

        // ── Necrodermis bar ──────────────────────────────────────────────────
        Rect ndRect = new Rect(x, rowMidY - 7f, ColNecrodermis, 14f);
        DrawNeedBar(ndRect, nech, NecronDefOfs.GW_UD_Necrodermis, NecrodermsFill, NecrodermisBg,
                    "GW40K_NechPanel_NecrodermisTip".Translate());
        x += ColNecrodermis + ColGap;

        // ── Draft toggle ──────────────────────────────────────────────────────
        Rect draftRect = new Rect(x + (ColDraft - 24f) * 0.5f, rowMidY - 12f, 24f, 24f);
        DrawDraftToggle(draftRect, nech);
        x += ColDraft + ColGap;

        // ── Commander ────────────────────────────────────────────────────────
        Rect cmdRect = new Rect(x, row.y + 2f, ColCommander, row.height - 4f);
        DrawCommanderCell(cmdRect, nech);
        x += ColCommander + ColGap;

        // ── Command Group dropdown ────────────────────────────────────────────
        Rect grpRect = new Rect(x, row.y + 3f, ColGroup, row.height - 6f);
        DrawGroupCell(grpRect, nech, mgr);
        x += ColGroup + ColGap;

        // ── Work Mode button ──────────────────────────────────────────────────
        Rect modeRect = new Rect(x, row.y + 3f, ColMode, row.height - 6f);
        DrawModeCell(modeRect, nech);
        x += ColMode + ColGap;

        // ── Allowed Area ─────────────────────────────────────────────────────
        Rect areaRect = new Rect(x, row.y + 3f, ColArea, row.height - 6f);
        DrawAreaCell(areaRect, nech);
    }

    // ── Column drawers ────────────────────────────────────────────────────────

    private static void DrawNeedBar(Rect rect, Pawn pawn, NeedDef needDef,
                                    Color fill, Color bg, string tip)
    {
        if (needDef == null) return;
        Need need = pawn?.needs?.TryGetNeed(needDef);
        float pct = need?.CurLevelPercentage ?? 0f;

        Widgets.FillableBar(rect, Mathf.Clamp01(pct),
                            SolidColorMaterials.NewSolidColorTexture(fill),
                            SolidColorMaterials.NewSolidColorTexture(bg),
                            true);

        // Percentage label centred on the bar
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, $"{pct * 100f:0.#}%");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;

        TooltipHandler.TipRegion(rect, tip + $"\n{pct * 100f:0.#}%");
    }

    private static void DrawDraftToggle(Rect rect, Pawn nech)
    {
        if (nech.drafter == null) return;

        bool drafted  = nech.Drafted;
        bool hasLink  = HediffComp_NecronCommandTracker.GetCommanderOf(nech) != null
                     || HediffComp_ControlNodeTracker.GetControllerOfConstruct(nech) != null;

        Texture2D icon = drafted ? TexCommand.Draft : TexCommand.Draft;
        Color col      = drafted ? Color.white : new Color(1f, 1f, 1f, 0.35f);

        if (!hasLink) col = Color.gray;

        Color prev = GUI.color;
        GUI.color  = col;

        if (Widgets.ButtonImage(rect, TexCommand.Draft) && hasLink && !nech.InMentalState)
        {
            bool nowDrafted = !drafted;
            nech.drafter.Drafted = nowDrafted;
            if (!nowDrafted)
                nech.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced, false);
            SoundDefOf.DraftOn.PlayOneShotOnCamera();
        }

        GUI.color = prev;

        string tipKey = drafted ? "GW40K_NechPanel_DraftedTip" : "GW40K_NechPanel_UndraftedTip";
        TooltipHandler.TipRegion(rect, tipKey.Translate());
    }

    private static void DrawCommanderCell(Rect rect, Pawn nech)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(nech);
        string label   = commander?.LabelShortCap ?? "–";

        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (commander != null && Widgets.ButtonInvisible(rect))
            CameraJumper.TryJumpAndSelect(commander);
        if (commander != null)
            TooltipHandler.TipRegion(rect, commander.LabelCap);
    }

    private static void DrawGroupCell(Rect rect, Pawn nech, NecronCommandGroupManager mgr)
    {
        if (mgr == null)
        {
            Widgets.Label(rect, "–");
            return;
        }

        int groupIdx = mgr.GetGroupOf(nech);
        string label = groupIdx >= 0 ? mgr.GetLabel(groupIdx) : "–";

        if (Widgets.ButtonText(rect, label))
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            for (int i = 0; i < NecronCommandGroupManager.GroupCount; i++)
            {
                int captured = i;
                string opt   = mgr.GetLabel(i);
                if (groupIdx == i) opt += " ✓";
                opts.Add(new FloatMenuOption(opt, () => mgr.AssignToGroup(nech, captured)));
            }
            if (groupIdx >= 0)
                opts.Add(new FloatMenuOption("GW40K_CmdGroup_Remove".Translate(), () => mgr.RemoveFromAllGroups(nech)));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }

    private static void DrawModeCell(Rect rect, Pawn nech)
    {
        ThingComp_NechWorkMode wm = nech.TryGetComp<ThingComp_NechWorkMode>();
        if (wm == null)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, "–");
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        bool hasCommander = wm.HasCommander;
        string modeLabel  = wm.CurMode?.LabelCap ?? "–";

        if (!hasCommander) GUI.color = Color.gray;

        if (Widgets.ButtonText(rect, modeLabel) && hasCommander)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (NechWorkModeDef m in wm.Props.availableModes)
            {
                NechWorkModeDef captured = m;
                bool isCurrent = captured == wm.CurMode;
                string optLabel = isCurrent ? captured.LabelCap + " ✓" : captured.LabelCap;
                opts.Add(new FloatMenuOption(optLabel, () => wm.TrySetMode(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        GUI.color = Color.white;
    }

    private static void DrawAreaCell(Rect rect, Pawn nech)
    {
        if (nech.playerSettings == null || nech.Map == null)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, "–");
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        Area current = nech.playerSettings.AreaRestrictionInPawnCurrentMap;
        string label = current?.Label ?? "Unrestricted".Translate().ToString();

        if (Widgets.ButtonText(rect, label))
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            opts.Add(new FloatMenuOption(
                "Unrestricted".Translate(),
                () => nech.playerSettings.AreaRestrictionInPawnCurrentMap = null));
            foreach (Area area in nech.Map.areaManager.AllAreas)
            {
                Area cap = area;
                opts.Add(new FloatMenuOption(area.Label, () => nech.playerSettings.AreaRestrictionInPawnCurrentMap = cap));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }

    // ── Data helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an ordered sequence of (commander, mechs-under-commander) pairs.
    /// Null commander = "Unbound" group, drawn last.
    /// Respects the active <see cref="_filterGroup"/> filter.
    /// </summary>
    private List<(Pawn commander, List<Pawn> mechs)> BuildGroupedData(NecronCommandGroupManager mgr)
    {
        // Collect candidate nechs (optionally filtered by command group)
        List<Pawn> candidates = new List<Pawn>();
        foreach (Map map in Find.Maps)
        {
            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (!NechUtility.IsNechControlled(p)) continue;
                if (_filterGroup >= 0 && mgr != null && mgr.GetGroupOf(p) != _filterGroup) continue;
                candidates.Add(p);
            }
        }

        // Group by commander
        Dictionary<Pawn, List<Pawn>> byCommander = new Dictionary<Pawn, List<Pawn>>();
        List<Pawn> unbound = new List<Pawn>();

        foreach (Pawn nech in candidates)
        {
            Pawn cmd = HediffComp_NecronCommandTracker.GetCommanderOf(nech);
            if (cmd != null)
            {
                if (!byCommander.ContainsKey(cmd))
                    byCommander[cmd] = new List<Pawn>();
                byCommander[cmd].Add(nech);
            }
            else
            {
                unbound.Add(nech);
            }
        }

        // Build result list: commanders first (sorted by name), then unbound
        var result = new List<(Pawn, List<Pawn>)>();
        List<Pawn> commanders = new List<Pawn>(byCommander.Keys);
        commanders.Sort((a, b) => string.Compare(a?.LabelShortCap, b?.LabelShortCap, System.StringComparison.OrdinalIgnoreCase));

        foreach (Pawn cmd in commanders)
            result.Add((cmd, byCommander[cmd]));

        if (unbound.Count > 0)
            result.Add((null, unbound));

        return result;
    }
}
