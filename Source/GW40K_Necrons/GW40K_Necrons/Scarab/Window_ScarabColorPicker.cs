using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Scarab paint picker: **same palette** as vanilla mech accent (<see cref="RimWorld.MainTabWindow_Mechs"/>),
/// fixed **12 columns** and vertical scroll (vanilla <see cref="RimWorld.Dialog_ChooseColor"/> layout).
/// CutoutComplex: Primary tints mask **red** (carapace), Secondary tints mask **green** (head); see <see cref="Graphic_ScarabDualMask"/>.
/// </summary>
public class Window_ScarabColorPicker : Window
{
    /// <summary>Swatch columns — matches vanilla <c>Widgets.ColorSelector</c> at default dialog width (~12).</summary>
    private const int GridCols = 12;

    private const float ColorSize = 22f;
    private const float ColorPadding = 2f;
    private const float GapBetweenCells = 2f;

    private readonly List<CompScarabPaint> comps;
    private bool editingPrimary = true;
    private Color pendingPrimary;
    private Color pendingSecondary;
    private Vector2 paletteScrollPos;

    private static List<Color> cachedPalette;

    private CompScarabPaint Primary => comps.Count > 0 ? comps[0] : null;

    /// <summary>Match vanilla <see cref="RimWorld.Dialog_ChooseColor.InitialSize"/>.</summary>
    public override Vector2 InitialSize => new Vector2(500f, 410f);

    public Window_ScarabColorPicker(List<CompScarabPaint> comps)
    {
        cachedPalette = null;
        this.comps = comps ?? new List<CompScarabPaint>();
        CompScarabPaint first = Primary;
        pendingPrimary = first?.Primary ?? Color.white;
        pendingSecondary = first?.Secondary ?? Color.white;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        doCloseX = true;
        draggable = true;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        if (Primary?.parent is not Pawn p || !ScarabPaintUtility.PlayerMayConfigurePaint(p))
            Close();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "Choose construct color");
        Text.Font = GameFont.Small;

        float bottomY = inRect.height - Window.CloseButSize.y;
        Rect previewRect = new Rect(inRect.x, inRect.y + 35f + 10f, 88f, 88f).ContractedBy(2f);
        Widgets.DrawBoxSolid(previewRect, editingPrimary ? pendingPrimary : pendingSecondary);
        Widgets.DrawBox(previewRect);

        Rect gridOuter = inRect;
        gridOuter.xMin += 105f;
        gridOuter.yMin += 45f;
        gridOuter.height = bottomY - gridOuter.yMin - 10f;
        DrawPaletteGrid(gridOuter);

        if (Widgets.ButtonText(new Rect(inRect.x, bottomY, Window.CloseButSize.x, Window.CloseButSize.y), "CloseButton".Translate()))
            Close();

        Rect toggleRect = new Rect(inRect.x + (inRect.width - 172f) * 0.5f, bottomY, 172f, Window.CloseButSize.y);
        string toggleLabel = editingPrimary ? "Editing: Primary" : "Editing: Secondary";
        if (Widgets.ButtonText(toggleRect, toggleLabel))
            editingPrimary = !editingPrimary;

        if (Widgets.ButtonText(new Rect(inRect.width - Window.CloseButSize.x, bottomY, Window.CloseButSize.x, Window.CloseButSize.y), "OK".Translate()))
        {
            foreach (CompScarabPaint c in comps)
            {
                c.SetPrimary(pendingPrimary);
                c.SetSecondary(pendingSecondary);
            }
            Close();
        }
    }

    private void DrawPaletteGrid(Rect rect)
    {
        List<Color> palette = GetPalette();
        if (palette.Count == 0)
            return;

        float cellOuter = ColorSize + ColorPadding * 2f;
        float rowStep = cellOuter + GapBetweenCells;
        float scrollBar = 16f;
        float innerW = rect.width - scrollBar;
        int rows = Mathf.CeilToInt(palette.Count / (float)GridCols);
        float viewH = rows * rowStep - GapBetweenCells;
        if (viewH < 1f)
            viewH = 1f;

        Rect viewRect = new Rect(0f, 0f, innerW, viewH);
        Widgets.BeginScrollView(rect, ref paletteScrollPos, viewRect);
        for (int i = 0; i < palette.Count; i++)
        {
            int r = i / GridCols;
            int c = i % GridCols;
            float x = c * (cellOuter + GapBetweenCells);
            float y = r * rowStep;
            Rect outer = new Rect(x, y, cellOuter, cellOuter);
            DrawPaletteCell(outer, palette[i]);
        }

        Widgets.EndScrollView();
    }

    private void DrawPaletteCell(Rect outer, Color color)
    {
        Rect inner = new Rect(outer.x + ColorPadding, outer.y + ColorPadding, ColorSize, ColorSize);
        Widgets.DrawLightHighlight(outer);
        if (Mouse.IsOver(outer))
            Widgets.DrawHighlight(outer);

        bool selected = editingPrimary
            ? Approximately(pendingPrimary, color)
            : Approximately(pendingSecondary, color);
        if (selected)
            Widgets.DrawBox(outer);

        Widgets.DrawBoxSolid(inner, color);

        if (Widgets.ButtonInvisible(outer))
        {
            if (editingPrimary)
                pendingPrimary = color;
            else
                pendingSecondary = color;
        }
    }

    private static List<Color> GetPalette()
    {
        if (cachedPalette == null || cachedPalette.Count == 0)
            cachedPalette = BuildPalette();
        return cachedPalette;
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f
            && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f
            && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private static List<Color> BuildPalette()
    {
        if (VanillaMechTabColorPalette.TryBuild(out List<Color> vanilla))
            return vanilla;

        var colors = new List<Color>();
        float[] sats = { 0.08f, 0.22f, 0.38f, 0.55f, 0.75f };
        float[] vals = { 0.20f, 0.35f, 0.50f, 0.68f, 0.85f, 1.0f };
        foreach (float v in vals)
            foreach (float s in sats)
                for (int h = 0; h < 12; h++)
                    colors.Add(Color.HSVToRGB(h / 12f, s, v));
        colors.Add(Color.white);
        colors.Add(new Color(0.85f, 0.85f, 0.85f));
        colors.Add(new Color(0.6f, 0.6f, 0.6f));
        colors.Add(new Color(0.25f, 0.25f, 0.25f));
        colors.Add(Color.black);
        colors.SortByColor(c => c);
        return colors;
    }
}
