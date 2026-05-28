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
        {
            // Close all currently open main tab windows before opening ours.
            // Use .Close() (not TryRemove) so the tab system's internal state is updated.
            foreach (MainButtonDef def in DefDatabase<MainButtonDef>.AllDefsListForReading)
            {
                MainTabWindow tw = def.TabWindow;
                if (tw != null && Find.WindowStack.IsOpen(tw))
                    tw.Close(doCloseSound: false);
            }
            Find.WindowStack.Add(_current = new Window_NechPanel());
        }
    }

    public override void Close(bool doCloseSound = true)
    {
        _current = null;
        base.Close(doCloseSound);
    }

    // ── Position / size ───────────────────────────────────────────────────────

    private static MainTabWindow MechTab =>
        DefDatabase<MainButtonDef>.GetNamedSilentFail("Mechs")?.TabWindow;

    // 35f = bottom tab bar height (MainButtonDef.ButtonHeight).
    private const float TabBarH = 35f;

    // Width = window margins (Margin*2) + content left indent (6) + all columns + scrollbar (16) + buffer (24).
    // The Margin*2 term accounts for the window chrome that is NOT part of inRect.
    public override Vector2 InitialSize =>
        new Vector2(Mathf.Min(Margin * 2f + 6f + TotalColsWidth + 16f + 24f, UI.screenWidth),
                    Mathf.Min(350f, UI.screenHeight - TabBarH));

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 sz = InitialSize;
        windowRect = new Rect(0f, UI.screenHeight - TabBarH - sz.y, sz.x, sz.y);
    }

    // ── UI state ──────────────────────────────────────────────────────────────

    /// <summary>-1 = all necrons; 0/1 = command group index filter.</summary>
    private int _filterGroup    = -1;
    private Vector2 _scrollPos  = Vector2.zero;
    private int _cachedAreaCount = -1; // resize window only when area count changes

    // ── Column widths ─────────────────────────────────────────────────────────

    private const float RowH       = 32f;
    private const float HeaderH    = 26f;
    private const float TabH       = 40f;   // matches vanilla top-bar height
    private const float CmdHeaderH = 26f;
    private const float ColGap     = 4f;

    // Fixed-width columns — totals to ~820px; remaining goes to margins/area col.
    private const float ColPortrait    = 28f;
    private const float ColName        = 155f;
    private const float ColEnergy      = 88f;
    private const float ColNecrodermis = 88f;
    private const float ColDraft       = 38f;
    private const float ColCommander   = 130f;
    private const float ColGroup       = 120f;
    private const float ColMode        = 130f;
    // Fixed per-button width for area buttons — column grows to fit, buttons never shrink.
    private const float AreaBtnW = 90f;

    // Dynamic area column width: count player-defined areas on the current map × AreaBtnW.
    private static float ColArea
    {
        get
        {
            int count = 1; // always Unrestricted
            Map map = Find.CurrentMap;
            if (map == null && Find.Maps?.Count > 0) map = Find.Maps[0];
            if (map != null)
                foreach (Area a in map.areaManager.AllAreas)
                    if (a is Area_Allowed || a is Area_Home) count++;
            return Mathf.Max(count, 2) * AreaBtnW; // minimum 2 slots
        }
    }

    // Derived total for inner-content rows
    private static float TotalColsWidth =>
        ColPortrait + ColName + ColEnergy + ColNecrodermis + ColDraft +
        ColCommander + ColGroup + ColMode + ColArea + ColGap * 8f;

    // X offset from the left edge of a row rect to each mergeable column.
    private const float ColCommanderX_Offset =
        6f + ColPortrait + ColGap + ColName + ColGap +
        ColEnergy + ColGap + ColNecrodermis + ColGap + ColDraft + ColGap;
    private const float ColModeX_Offset =
        ColCommanderX_Offset + ColCommander + ColGap + ColGroup + ColGap;

    // ── Bar colours ──────────────────────────────────────────────────────────

    /// <summary>Deep green fill for Core Flux bar (user spec).</summary>
    private static readonly Color CoreFluxFill    = new Color(0.08f, 0.72f, 0.16f);
    /// <summary>Deep green background track for Core Flux bar.</summary>
    private static readonly Color CoreFluxBg      = new Color(0.03f, 0.22f, 0.06f);
    /// <summary>Dark muted olive-green fill for Necrodermis bar.</summary>
    private static readonly Color NecrodermsFill  = new Color(0.26f, 0.44f, 0.22f);
    /// <summary>Dark olive-green background track for Necrodermis bar.</summary>
    private static readonly Color NecrodermisBg   = new Color(0.09f, 0.15f, 0.08f);

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

    // Returns the number of player-assignable areas on the current map (Area_Allowed + Area_Home).
    private static int CountCurrentAreas()
    {
        Map map = Find.CurrentMap;
        if (map == null && Find.Maps?.Count > 0) map = Find.Maps[0];
        if (map == null) return 0;
        int n = 0;
        foreach (Area a in map.areaManager.AllAreas)
            if (a is Area_Allowed || a is Area_Home) n++;
        return n;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Resize window when a region is added (each extra region adds AreaBtnW to the width).
        int areaCount = CountCurrentAreas();
        if (areaCount != _cachedAreaCount)
        {
            _cachedAreaCount = areaCount;
            float requiredW = Mathf.Min(Margin * 2f + 6f + TotalColsWidth + 16f + 24f, UI.screenWidth);
            if (windowRect.width < requiredW)
                windowRect = new Rect(windowRect.x, windowRect.y, requiredW, windowRect.height);
        }

        NecronCommandGroupManager mgr = NecronCommandGroupManager.Instance;

        // ── Top bar ──────────────────────────────────────────────────────────
        float topY = inRect.y;
        DrawTopBar(new Rect(inRect.x, topY, inRect.width, TabH));
        topY += TabH + 2f;

        // ── Column header ────────────────────────────────────────────────────
        Rect headerRect = new Rect(inRect.x, topY, inRect.width, HeaderH);
        Widgets.DrawMenuSection(headerRect);
        DrawColumnHeaders(headerRect, IsScrolledIntoControlNodeSection(mgr));
        topY += HeaderH;

        // ── Scroll area ───────────────────────────────────────────────────────
        Rect scrollOuter = new Rect(inRect.x, topY, inRect.width, inRect.yMax - topY);
        DrawScrollContent(scrollOuter, mgr);
    }

    // ── Top bar ────────────────────────────────────────────────────────────────

    private void DrawTopBar(Rect bar)
    {
        Widgets.DrawMenuSection(bar);

        const float BtnH   = 30f;
        const float CtrW   = 170f;  // centered Mechs button
        const float DropW  = 160f;  // filter dropdown
        const float LabelW = 48f;   // "Filter:" text

        float btnY = bar.y + (bar.height - BtnH) * 0.5f;

        // Left: "Filter:" + dropdown
        float x = bar.x + 8f;
        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, btnY, LabelW, BtnH), "Filter:");
        Text.Anchor = TextAnchor.UpperLeft;
        x += LabelW + 4f;

        NecronCommandGroupManager mgr = NecronCommandGroupManager.Instance;
        string dropLabel = _filterGroup < 0
            ? "All"
            : (mgr?.GetLabel(_filterGroup) ?? $"Group {_filterGroup + 1}");

        if (Widgets.ButtonText(new Rect(x, btnY, DropW, BtnH), dropLabel))
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            opts.Add(new FloatMenuOption("GW40K_NechPanel_AllNecrons".Translate(),
                () => { _filterGroup = -1; _scrollPos = Vector2.zero; }));
            for (int i = 0; i < NecronCommandGroupManager.GroupCount; i++)
            {
                int captured = i;
                string lbl = mgr?.GetLabel(i) ?? $"Group {i + 1}";
                opts.Add(new FloatMenuOption(lbl,
                    () => { _filterGroup = captured; _scrollPos = Vector2.zero; }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // Center: [← Mechs] — matches vanilla "Necrons" button position and size
        Rect mechsBtn = new Rect(bar.x + (bar.width - CtrW) * 0.5f, btnY, CtrW, BtnH);
        if (Widgets.ButtonText(mechsBtn, "GW40K_NechPanel_MechsBack".Translate()))
        {
            Close();
            // Close all open tabs then re-open the vanilla Mechs tab.
            foreach (MainButtonDef def in DefDatabase<MainButtonDef>.AllDefsListForReading)
            {
                MainTabWindow tw = def.TabWindow;
                if (tw != null && Find.WindowStack.IsOpen(tw))
                    tw.Close(doCloseSound: false);
            }
            MainButtonDef mechsDef = DefDatabase<MainButtonDef>.GetNamedSilentFail("Mechs");
            if (mechsDef?.TabWindow != null)
                Find.WindowStack.Add(mechsDef.TabWindow);
        }
        TooltipHandler.TipRegion(mechsBtn, "GW40K_NechPanel_MechsBackTip".Translate());

        // Right: [Manage areas...] — mirrors vanilla mechs tab
        const float ManageW = 170f;
        Rect manageBtn = new Rect(bar.xMax - ManageW - 8f, btnY, ManageW, BtnH);
        if (Widgets.ButtonText(manageBtn, "ManageAreas".Translate()))
        {
            Map map = Find.CurrentMap;
            if (map == null && Find.Maps?.Count > 0) map = Find.Maps[0];
            if (map != null)
                Find.WindowStack.Add(new Dialog_ManageAreas(map));
        }
    }

    // ── Column headers ────────────────────────────────────────────────────────

    // Returns true when the scroll position has advanced past all humanlike rows
    // and into the Control Node (scarab) section, so "Core Flux" should read "Health".
    private bool IsScrolledIntoControlNodeSection(NecronCommandGroupManager mgr)
    {
        if (_filterGroup >= 0) return false; // scarabs aren't shown when filtering by group
        float y = 0f;
        foreach (var (_, mechs, isControlNode) in BuildGroupedData(mgr))
        {
            if (isControlNode) return _scrollPos.y >= y;
            y += CmdHeaderH + mechs.Count * RowH;
        }
        return false;
    }

    private void DrawColumnHeaders(Rect rect, bool scarabSection = false)
    {
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        float x = rect.x + 6f;

        x += ColPortrait + ColGap; // portrait column has no label

        string coreFluxHeader = scarabSection ? "Health" : "GW40K_NechPanel_ColCoreFlux".Translate();
        DrawHeaderLabel(ref x, rect, "GW40K_NechPanel_ColName".Translate(), ColName);
        DrawHeaderLabel(ref x, rect, coreFluxHeader,                        ColEnergy);
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
        foreach (var (_, mechs, _) in grouped)
            contentH += CmdHeaderH + mechs.Count * RowH;

        if (contentH < outer.height)
            contentH = outer.height;

        Rect view = new Rect(0f, 0f, outer.width - 16f, contentH); // -16 for scrollbar
        Widgets.BeginScrollView(outer, ref _scrollPos, view);

        float y = 0f;
        bool alt = false; // alternating row shading

        foreach (var (commander, mechs, isControlNode) in grouped)
        {
            // ── Commander header row ──────────────────────────────────────────
            Rect cmdHdr = new Rect(view.x, y, view.width, CmdHeaderH);
            Widgets.DrawMenuSection(cmdHdr);
            DrawCommanderHeader(cmdHdr, commander, isControlNode);
            y += CmdHeaderH;

            if (mechs.Count == 0) continue;

            float groupStartY    = y;
            bool  sectionStartAlt = alt; // capture before rows so merged cells match first row

            // ── Pawn rows (commander + mode skipped; drawn merged below) ──────
            foreach (Pawn nech in mechs)
            {
                Rect row = new Rect(view.x, y, view.width, RowH);
                if (alt) Widgets.DrawAltRect(row);
                DrawNechRow(row, nech, mgr, skipCommander: true, skipMode: true);
                y   += RowH;
                alt  = !alt;
            }

            float baseX = view.x;

            // ── Merged Commander cells ────────────────────────────────────────
            {
                float colX = baseX + ColCommanderX_Offset;
                int rs = 0;
                while (rs < mechs.Count)
                {
                    Pawn runCmd = HediffComp_NecronCommandTracker.GetCommanderOf(mechs[rs])
                               ?? HediffComp_ControlNodeTracker.GetControllerOfConstruct(mechs[rs]);
                    int re = rs + 1;
                    while (re < mechs.Count)
                    {
                        Pawn next = HediffComp_NecronCommandTracker.GetCommanderOf(mechs[re])
                                 ?? HediffComp_ControlNodeTracker.GetControllerOfConstruct(mechs[re]);
                        if (next != runCmd) break;
                        re++;
                    }
                    int len = re - rs;
                    // Cover alternating row shading with consistent background for this run
                    bool firstAlt = sectionStartAlt ^ (rs % 2 == 1);
                    Rect cmdMerged = new Rect(colX, groupStartY + rs * RowH + 2f, ColCommander, len * RowH - 4f);
                    DrawMergedBackground(cmdMerged, firstAlt);
                    DrawCommanderCell(cmdMerged, mechs[rs]);
                    rs = re;
                }
            }

            // ── Merged Mode cells ─────────────────────────────────────────────
            {
                float colX = baseX + ColModeX_Offset;
                int rs = 0;
                while (rs < mechs.Count)
                {
                    int len;
                    bool firstAlt;

                    if (isControlNode)
                    {
                        // Scarab/control-node constructs: use ControlNodeMode
                        ControlNodeMode runMode = GameComponent_CanoptekConstructModes.Current?.GetMode(mechs[rs]) ?? ControlNodeMode.Consume;
                        int re = rs + 1;
                        while (re < mechs.Count)
                        {
                            ControlNodeMode next = GameComponent_CanoptekConstructModes.Current?.GetMode(mechs[re]) ?? ControlNodeMode.Consume;
                            if (next != runMode) break;
                            re++;
                        }
                        len      = re - rs;
                        firstAlt = sectionStartAlt ^ (rs % 2 == 1);
                        DrawControlNodeModeCellForRun(
                            new Rect(colX, groupStartY + rs * RowH + 3f, ColMode, len * RowH - 6f),
                            mechs, rs, len, firstAlt);
                        rs = re;
                    }
                    else
                    {
                        // Humanlike Necrons / Spyders: use NechWorkModeDef
                        NechWorkModeDef runMode = mechs[rs].TryGetComp<ThingComp_NechWorkMode>()?.CurMode;
                        int re = rs + 1;
                        while (re < mechs.Count)
                        {
                            NechWorkModeDef next = mechs[re].TryGetComp<ThingComp_NechWorkMode>()?.CurMode;
                            if (next != runMode) break;
                            re++;
                        }
                        len      = re - rs;
                        firstAlt = sectionStartAlt ^ (rs % 2 == 1);
                        DrawModeCellForRun(
                            new Rect(colX, groupStartY + rs * RowH + 3f, ColMode, len * RowH - 6f),
                            mechs, rs, len, firstAlt);
                        rs = re;
                    }
                }
            }

            // ── Hover highlight — drawn last so it sits on top of merged cells ─
            for (int i = 0; i < mechs.Count; i++)
            {
                Rect row = new Rect(view.x, groupStartY + i * RowH, view.width, RowH);
                if (Mouse.IsOver(row))
                    Widgets.DrawHighlightSelected(row);
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawCommanderHeader(Rect rect, Pawn commander, bool isControlNode)
    {
        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        float indent = 8f;

        string headerLabel;
        if (isControlNode)
        {
            headerLabel = "▸ " + "GW40K_NechPanel_ControlNode".Translate();
        }
        else if (commander != null)
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

        if (!isControlNode && commander != null && Widgets.ButtonInvisible(rect))
        {
            CameraJumper.TryJumpAndSelect(commander);
            _current?.Close();
        }

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;
    }

    // ── Per-pawn row ─────────────────────────────────────────────────────────

    private void DrawNechRow(Rect row, Pawn nech, NecronCommandGroupManager mgr,
                             bool skipCommander = false, bool skipMode = false)
    {
        float x       = row.x + 6f;
        float rowMidY = row.y + row.height * 0.5f;
        bool isScarab = !nech.RaceProps.Humanlike && !ControlNodeUtility.IsSpyder(nech);

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

        // ── Core Flux bar (or Health for scarabs) ───────────────────────────
        Rect efRect = new Rect(x, rowMidY - 10f, ColEnergy, 20f);
        if (isScarab)
            DrawHealthBar(efRect, nech);
        else
            DrawNeedBar(efRect, nech, NecronDefOfs.GW40K_CoreFlux, CoreFluxFill, CoreFluxBg,
                        "GW40K_NechPanel_CoreFluxTip".Translate());
        x += ColEnergy + ColGap;

        // ── Necrodermis bar ──────────────────────────────────────────────────
        Rect ndRect = new Rect(x, rowMidY - 10f, ColNecrodermis, 20f);
        DrawNeedBar(ndRect, nech, NecronDefOfs.GW_UD_Necrodermis, NecrodermsFill, NecrodermisBg,
                    "GW40K_NechPanel_NecrodermisTip".Translate());
        x += ColNecrodermis + ColGap;

        // ── Draft toggle ──────────────────────────────────────────────────────
        Rect draftRect = new Rect(x + (ColDraft - 24f) * 0.5f, rowMidY - 12f, 24f, 24f);
        DrawDraftToggle(draftRect, nech);
        x += ColDraft + ColGap;

        // ── Commander (drawn as merged cell by DrawScrollContent) ────────────
        if (!skipCommander)
            DrawCommanderCell(new Rect(x, row.y + 2f, ColCommander, row.height - 4f), nech);
        x += ColCommander + ColGap;

        // ── Command Group dropdown ────────────────────────────────────────────
        Rect grpRect = new Rect(x, row.y + 3f, ColGroup, row.height - 6f);
        DrawGroupCell(grpRect, nech, mgr);
        x += ColGroup + ColGap;

        // ── Work Mode (drawn as merged cell by DrawScrollContent) ─────────────
        if (!skipMode)
            DrawModeCell(new Rect(x, row.y + 3f, ColMode, row.height - 6f), nech);
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
        GUI.color = Color.white; // defensive — merged cell backgrounds can dirty GUI.color
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

    private static void DrawHealthBar(Rect rect, Pawn nech)
    {
        float hp = nech.health?.summaryHealth?.SummaryHealthPercent ?? 0f;

        Color fill, bg;
        if (hp > 0.5f)
        {
            fill = new Color(0.40f, 0.40f, 0.40f);   // dark grey
            bg   = new Color(0.18f, 0.18f, 0.18f);
        }
        else if (hp > 0.25f)
        {
            fill = new Color(0.88f, 0.52f, 0.18f);   // pale orange
            bg   = new Color(0.35f, 0.20f, 0.06f);
        }
        else
        {
            fill = new Color(0.80f, 0.14f, 0.14f);   // red
            bg   = new Color(0.28f, 0.05f, 0.05f);
        }

        Widgets.FillableBar(rect, Mathf.Clamp01(hp),
            SolidColorMaterials.NewSolidColorTexture(fill),
            SolidColorMaterials.NewSolidColorTexture(bg), true);

        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, $"{hp * 100f:0.#}%");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;

        TooltipHandler.TipRegion(rect, $"Health: {hp * 100f:0.#}%");
    }

    private static void DrawDraftToggle(Rect rect, Pawn nech)
    {
        if (nech.drafter == null) return;

        bool drafted  = nech.Drafted;
        bool hasLink  = HediffComp_NecronCommandTracker.GetCommanderOf(nech) != null
                     || HediffComp_ControlNodeTracker.GetControllerOfConstruct(nech) != null;
        bool disabled = !hasLink || nech.InMentalState;

        bool prev = drafted;
        Widgets.Checkbox(rect.x, rect.y, ref drafted, rect.width, disabled);

        if (drafted != prev && !disabled)
        {
            nech.drafter.Drafted = drafted;
            if (!drafted)
                nech.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced, false);
            SoundDefOf.DraftOn.PlayOneShotOnCamera();
        }

        string tipKey = prev ? "GW40K_NechPanel_DraftedTip" : "GW40K_NechPanel_UndraftedTip";
        TooltipHandler.TipRegion(rect, tipKey.Translate());
    }

    private static void DrawCommanderCell(Rect rect, Pawn nech)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(nech)
                      ?? HediffComp_ControlNodeTracker.GetControllerOfConstruct(nech);
        string label   = commander?.LabelShortCap ?? "–";

        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (commander != null && Widgets.ButtonInvisible(rect))
        {
            CameraJumper.TryJumpAndSelect(commander);
            _current?.Close();
        }
        if (commander != null)
            TooltipHandler.TipRegion(rect, commander.LabelCap);
    }

    private static void DrawGroupCell(Rect rect, Pawn nech, NecronCommandGroupManager mgr)
    {
        if (mgr == null) { Widgets.Label(rect, "–"); return; }

        bool isScarab = !nech.RaceProps.Humanlike && !ControlNodeUtility.IsSpyder(nech);

        if (isScarab)
        {
            // Scarabs use independent A/B groups
            int groupIdx = mgr.GetScarabGroupOf(nech);
            string label = groupIdx >= 0 ? mgr.GetScarabLabel(groupIdx) : "–";

            if (Widgets.ButtonText(rect, label))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                for (int i = 0; i < NecronCommandGroupManager.ScarabGroupCount; i++)
                {
                    int captured = i;
                    string lbl = mgr.GetScarabLabel(i);
                    if (groupIdx == i) lbl += " ✓";
                    opts.Add(new FloatMenuOption(lbl, () => mgr.AssignToScarabGroup(nech, captured)));
                }
                if (groupIdx >= 0)
                    opts.Add(new FloatMenuOption("GW40K_CmdGroup_Remove".Translate(), () => mgr.RemoveFromAllScarabGroups(nech)));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }
        else
        {
            // Humanlike Nechs use numbered command groups
            int groupIdx = mgr.GetGroupOf(nech);
            string label = groupIdx >= 0 ? (groupIdx + 1).ToString() : "–";

            if (Widgets.ButtonText(rect, label))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                for (int i = 0; i < NecronCommandGroupManager.GroupCount; i++)
                {
                    int captured = i;
                    string opt = mgr.GetLabel(i);
                    if (groupIdx == i) opt += " ✓";
                    opts.Add(new FloatMenuOption(opt, () => mgr.AssignToGroup(nech, captured)));
                }
                if (groupIdx >= 0)
                    opts.Add(new FloatMenuOption("GW40K_CmdGroup_Remove".Translate(), () => mgr.RemoveFromAllGroups(nech)));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
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

        if (Mouse.IsOver(rect) && hasCommander)
            Widgets.DrawHighlight(rect);

        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, modeLabel);
        Text.Anchor = TextAnchor.UpperLeft;

        GUI.color = Color.white;

        if (Widgets.ButtonInvisible(rect) && hasCommander)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (NechWorkModeDef m in wm.Props.availableModes)
            {
                NechWorkModeDef captured = m;
                bool isCurrent = captured == wm.CurMode;
                string optLabel = isCurrent ? captured.LabelCap + " ✓" : captured.LabelCap;
                opts.Add(new FloatMenuOption(optLabel, () => wm.TrySetMode(captured),
                    captured.UIIcon, Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }

    // Consistent background for merged cells — covers the alternating row shading underneath.
    private static readonly Color MergedBgBase = new Color(0.12f, 0.12f, 0.14f);
    private static readonly Color MergedBgAlt  = new Color(0.16f, 0.16f, 0.18f);

    private static void DrawMergedBackground(Rect rect, bool isAlt)
    {
        // Explicitly save/restore GUI.color — DrawBoxSolid is not guaranteed to restore it.
        Color prev = GUI.color;
        GUI.color = isAlt ? MergedBgAlt : MergedBgBase;
        GUI.DrawTexture(rect, BaseContent.WhiteTex);
        GUI.color = prev;
    }

    // Draws a merged mode cell covering 'len' pawns starting at 'startIdx'.
    // Visual: one label spanning all rows. Interaction: per-row invisible slices so
    // clicking only affects the specific pawn whose row was clicked.
    private static void DrawModeCellForRun(Rect rect, List<Pawn> mechs, int startIdx, int len, bool firstRowAlt)
    {
        // Consistent background covering the alternating row shading underneath
        DrawMergedBackground(rect, firstRowAlt);

        ThingComp_NechWorkMode wm = mechs[startIdx].TryGetComp<ThingComp_NechWorkMode>();
        string modeLabel = wm?.CurMode?.LabelCap ?? "–";
        bool   anyHasCmd = wm?.HasCommander == true;

        if (!anyHasCmd) GUI.color = Color.gray;
        if (Mouse.IsOver(rect) && anyHasCmd) Widgets.DrawHighlight(rect);

        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, modeLabel);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color   = Color.white;

        // Per-row slices: clicking a specific row only opens the menu for THAT pawn
        float sliceH = rect.height / len;
        for (int i = 0; i < len; i++)
        {
            int   pawni     = startIdx + i;
            Pawn  pawn      = mechs[pawni];
            ThingComp_NechWorkMode wmi = pawn.TryGetComp<ThingComp_NechWorkMode>();
            if (wmi == null || !wmi.HasCommander) continue;

            Rect slice = new Rect(rect.x, rect.y + i * sliceH, rect.width, sliceH);
            if (Widgets.ButtonInvisible(slice))
            {
                ThingComp_NechWorkMode capturedWm = wmi;
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (NechWorkModeDef m in capturedWm.Props.availableModes)
                {
                    NechWorkModeDef captured = m;
                    bool isCurrent = captured == capturedWm.CurMode;
                    string optLabel = isCurrent ? captured.LabelCap + " ✓" : captured.LabelCap;
                    opts.Add(new FloatMenuOption(optLabel,
                        () => capturedWm.TrySetMode(captured),
                        captured.UIIcon, Color.white));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }
    }

    // Merged mode cell for control-node constructs (scarabs) — uses ControlNodeMode, not NechWorkModeDef.
    private static void DrawControlNodeModeCellForRun(Rect rect, List<Pawn> mechs, int startIdx, int len, bool firstRowAlt)
    {
        DrawMergedBackground(rect, firstRowAlt);

        ControlNodeMode curMode = GameComponent_CanoptekConstructModes.Current?.GetMode(mechs[startIdx]) ?? ControlNodeMode.Consume;
        string modeLabel = Gizmo_ControlNodeBandwidth.ModeLabel(curMode);
        bool hasController = HediffComp_ControlNodeTracker.GetControllerOfConstruct(mechs[startIdx]) != null;

        if (!hasController) GUI.color = Color.gray;
        if (Mouse.IsOver(rect) && hasController) Widgets.DrawHighlight(rect);

        Text.Font   = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, modeLabel);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color   = Color.white;

        if (!hasController) return;

        float sliceH = rect.height / len;
        for (int i = 0; i < len; i++)
        {
            Pawn pawn = mechs[startIdx + i];
            Rect slice = new Rect(rect.x, rect.y + i * sliceH, rect.width, sliceH);
            if (!Widgets.ButtonInvisible(slice)) continue;

            ControlNodeMode current = GameComponent_CanoptekConstructModes.Current?.GetMode(pawn) ?? ControlNodeMode.Consume;
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (ControlNodeMode mode in new[]
            {
                ControlNodeMode.Consume, ControlNodeMode.Repair, ControlNodeMode.Work,
                ControlNodeMode.Produce, ControlNodeMode.Combat, ControlNodeMode.Defend
            })
            {
                ControlNodeMode captured  = mode;
                Pawn            capturedP = pawn;
                string lbl = Gizmo_ControlNodeBandwidth.ModeLabel(captured);
                if (current == captured) lbl += " ✓";
                opts.Add(new FloatMenuOption(lbl,
                    () => GameComponent_CanoptekConstructModes.Current?.SetMode(capturedP, captured)));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }
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

        // Build option list: null=Unrestricted, then Home + all player-defined allowed areas.
        var options = new List<Area> { null };
        foreach (Area a in nech.Map.areaManager.AllAreas)
            if (a is Area_Allowed || a is Area_Home) options.Add(a);

        Area current = nech.playerSettings.AreaRestrictionInPawnCurrentMap;
        float btnW = AreaBtnW; // fixed width — column grows, buttons never shrink

        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;

        float x = rect.x;
        foreach (Area area in options)
        {
            bool isSelected = area == current;
            Rect btn = new Rect(x, rect.y + 1f, btnW - 1f, rect.height - 2f);

            // Restore area color background; "Unrestricted" gets a neutral highlight
            if (area != null)
            {
                Color c = area.Color;
                GUI.color = new Color(c.r, c.g, c.b, isSelected ? 0.90f : 0.65f);
                GUI.DrawTexture(btn, BaseContent.WhiteTex);
                GUI.color = Color.white;
            }
            else if (Mouse.IsOver(btn) && !isSelected)
            {
                Widgets.DrawHighlight(btn);
            }

            // White outline on selected only
            if (isSelected)
                Widgets.DrawBox(btn, 1);

            // Hardcode "Unrestricted" — .Translate() can return garbled tagged strings
            Widgets.Label(btn, area?.Label ?? "Unrestricted");

            if (!isSelected && Widgets.ButtonInvisible(btn))
                nech.playerSettings.AreaRestrictionInPawnCurrentMap = area;

            x += btnW;
        }

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;
    }

    // ── Data helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns ordered (commander, mechs, isControlNode) tuples.
    /// Order: commanders (sorted), then Unbound, then Control Node (scarabs etc.) last.
    /// Control-node constructs (non-humanlike, non-spyder) are always in their own section
    /// and only shown when the "All Necrons" filter is active.
    /// </summary>
    private List<(Pawn commander, List<Pawn> mechs, bool isControlNode)> BuildGroupedData(NecronCommandGroupManager mgr)
    {
        Dictionary<Pawn, List<Pawn>> byCommander = new Dictionary<Pawn, List<Pawn>>();
        List<Pawn> unbound      = new List<Pawn>();
        List<Pawn> controlNode  = new List<Pawn>();

        foreach (Map map in Find.Maps)
        {
            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (!NechUtility.IsNechControlled(p)) continue;

                // Non-humanlike non-spyder constructs (scarabs etc.) → Control Node section.
                // They don't belong to command groups, so only shown in "All Necrons" view.
                if (!p.RaceProps.Humanlike && !ControlNodeUtility.IsSpyder(p))
                {
                    if (_filterGroup == -1)
                        controlNode.Add(p);
                    continue;
                }

                if (_filterGroup >= 0 && mgr != null && mgr.GetGroupOf(p) != _filterGroup) continue;

                Pawn cmd = HediffComp_NecronCommandTracker.GetCommanderOf(p);
                if (cmd != null)
                {
                    if (!byCommander.ContainsKey(cmd))
                        byCommander[cmd] = new List<Pawn>();
                    byCommander[cmd].Add(p);
                }
                else
                {
                    unbound.Add(p);
                }
            }
        }

        var result = new List<(Pawn, List<Pawn>, bool)>();
        List<Pawn> commanders = new List<Pawn>(byCommander.Keys);
        commanders.Sort((a, b) => string.Compare(a?.LabelShortCap, b?.LabelShortCap, System.StringComparison.OrdinalIgnoreCase));

        foreach (Pawn cmd in commanders)
            result.Add((cmd, byCommander[cmd], false));

        if (unbound.Count > 0)
            result.Add((null, unbound, false));

        if (controlNode.Count > 0)
            result.Add((null, controlNode, true));

        return result;
    }
}
