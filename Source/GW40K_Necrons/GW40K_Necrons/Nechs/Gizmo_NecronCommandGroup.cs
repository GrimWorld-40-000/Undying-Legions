using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Gizmo shown on any selected Nech pawn that belongs to a Command Group.
/// Clicking the gizmo selects every live member of the same group on the current map.
/// Tooltip lists all members with their Core Flux and Necrodermis levels.
/// </summary>
public class Gizmo_NecronCommandGroup : Gizmo
{
    // ── Lazy icon (loaded once, shared between all instances) ────────────────

    private static Texture2D s_assignIcon;

    /// <summary>Icon for the "Assign to Command Group" action button.</summary>
    internal static Texture2D AssignIcon
    {
        get
        {
            if (s_assignIcon == null)
                s_assignIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignMechControlGroup", false)
                            ?? TexButton.ReorderDown
                            ?? BaseContent.BadTex;
            return s_assignIcon;
        }
    }

    // ── Layout constants ─────────────────────────────────────────────────────

    private const float GizmoSize   = 75f;
    private const float TitleH      = 14f;
    private const float FooterH     = 12f;
    private const float PortraitSz  = 20f;
    private const float PortraitGap = 2f;

    // ── Instance data ────────────────────────────────────────────────────────

    private readonly Pawn _pawn;
    private readonly int _groupIndex;
    private readonly NecronCommandGroupManager _mgr;

    public Gizmo_NecronCommandGroup(Pawn pawn, int groupIndex, NecronCommandGroupManager mgr)
    {
        _pawn       = pawn;
        _groupIndex = groupIndex;
        _mgr        = mgr;
    }

    // ── Gizmo rendering ──────────────────────────────────────────────────────

    public override float GetWidth(float maxWidth) => Mathf.Min(GizmoSize, maxWidth);

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        float w    = GetWidth(maxWidth);
        Rect rect  = new Rect(topLeft.x, topLeft.y, w, GizmoSize);

        Widgets.DrawWindowBackground(rect);
        if (Mouse.IsOver(rect))
            Widgets.DrawHighlight(rect);

        // ── Title ────────────────────────────────────────────────────────────
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(rect.x + 2f, rect.y + 3f, rect.width - 4f, TitleH),
                      _mgr.GetLabel(_groupIndex));
        Text.Anchor = TextAnchor.UpperLeft;

        // ── Mini portraits of group members ──────────────────────────────────
        List<Pawn> group  = _mgr.GetGroup(_groupIndex);
        float pX           = rect.x + 4f;
        float pY           = rect.y + TitleH + 6f;
        float rowXMax      = rect.xMax - 4f;
        float portraitYMax = rect.yMax - FooterH - 4f;

        if (group != null)
        {
            foreach (Pawn member in group)
            {
                if (member == null || member.Dead || member.Destroyed || !member.Spawned)
                    continue;

                // Wrap to next row if needed
                if (pX + PortraitSz > rowXMax)
                {
                    pX  = rect.x + 4f;
                    pY += PortraitSz + PortraitGap;
                }
                if (pY + PortraitSz > portraitYMax)
                    break;

                Rect pr = new Rect(pX, pY, PortraitSz, PortraitSz);
                RenderTexture portrait = PortraitsCache.Get(
                    member,
                    new Vector2(PortraitSz, PortraitSz),
                    Rot4.South,
                    default,
                    1.0f);
                GUI.DrawTexture(pr, portrait);

                // Outline the current pawn's portrait
                if (member == _pawn)
                    Widgets.DrawBox(pr, 1);

                pX += PortraitSz + PortraitGap;
            }
        }

        // ── Footer: member count ─────────────────────────────────────────────
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.LowerCenter;
        int count = CountLiveMembers();
        Widgets.Label(new Rect(rect.x + 2f, rect.yMax - FooterH - 2f, rect.width - 4f, FooterH),
                      $"{count} {(count == 1 ? "nech" : "nechs")}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font   = GameFont.Small;

        // ── Tooltip ──────────────────────────────────────────────────────────
        TooltipHandler.TipRegion(rect, () => BuildTooltip(), rect.GetHashCode() ^ _groupIndex);

        // ── Click: select all in group ───────────────────────────────────────
        GizmoState state = GizmoState.Clear;
        if (Mouse.IsOver(rect))
        {
            state = GizmoState.Mouseover;
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && Mouse.IsOver(rect))
            {
                SelectGroupPawns();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                Event.current.Use();
                state = GizmoState.Interacted;
            }
        }
        return new GizmoResult(state);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private int CountLiveMembers()
    {
        List<Pawn> group = _mgr.GetGroup(_groupIndex);
        if (group == null) return 0;
        int n = 0;
        foreach (Pawn p in group)
            if (p != null && !p.Dead && !p.Destroyed) n++;
        return n;
    }

    private void SelectGroupPawns()
    {
        List<Pawn> group = _mgr.GetGroup(_groupIndex);
        if (group == null || group.Count == 0) return;

        Map map = Find.CurrentMap;
        if (map == null) return;

        Find.Selector.ClearSelection();
        Pawn firstSpawned = null;
        foreach (Pawn p in group)
        {
            if (p == null || p.Dead || p.Destroyed) continue;
            if (p.Spawned && p.MapHeld == map)
            {
                Find.Selector.Select(p, playSound: false, forceDesignatorDeselect: false);
                firstSpawned ??= p;
            }
        }
        if (firstSpawned != null)
            CameraJumper.TryJump(firstSpawned);
    }

    private string BuildTooltip()
    {
        List<Pawn> group = _mgr.GetGroup(_groupIndex);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(_mgr.GetLabel(_groupIndex));

        if (group == null || group.Count == 0)
        {
            sb.Append("(empty)");
            return sb.ToString();
        }

        sb.AppendLine();
        foreach (Pawn p in group)
        {
            if (p == null || p.Dead || p.Destroyed) continue;

            float energyPct = 0f;
            Need energyNeed = p.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
            if (energyNeed != null) energyPct = energyNeed.CurLevelPercentage * 100f;

            float necroPct = 0f;
            Need necroNeed = p.needs?.TryGetNeed(NecronDefOfs.GW_UD_Necrodermis);
            if (necroNeed != null) necroPct = necroNeed.CurLevelPercentage * 100f;

            ThingComp_NechWorkMode wm = p.TryGetComp<ThingComp_NechWorkMode>();
            string modeName = wm?.CurMode?.LabelCap ?? "–";

            sb.AppendLine($"  {p.LabelCap}");
            sb.AppendLine($"    Core Flux {energyPct:0.#}%  ·  Necrodermis {necroPct:0.#}%  ·  Mode: {modeName}");
        }

        sb.AppendLine();
        sb.Append("Click to select all.");
        return sb.ToString().TrimEnd();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Static injection helpers
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Provides the two Command Group gizmos injected at the end of every Nech pawn's
/// gizmo list by <see cref="HarmonyPatch_NechGizmos"/>.
/// <list type="bullet">
///   <item>If the pawn belongs to a group: <see cref="Gizmo_NecronCommandGroup"/> (shows members, click=select all)</item>
///   <item>Always: an "Assign to Command Group" <see cref="Command_Action"/> (float menu: Group 1 / Group 2 / Remove)</item>
/// </list>
/// </summary>
internal static class NecronCommandGroupGizmos
{
    internal static IEnumerable<Gizmo> GetGizmos(Pawn pawn)
    {
        NecronCommandGroupManager mgr = NecronCommandGroupManager.Instance;
        if (mgr == null || pawn?.Faction != Faction.OfPlayer)
            yield break;

        int groupIdx = mgr.GetGroupOf(pawn);

        // Group membership box (only when assigned)
        if (groupIdx >= 0)
            yield return new Gizmo_NecronCommandGroup(pawn, groupIdx, mgr);

        // Assign button (always)
        yield return MakeAssignGizmo(pawn, mgr);
    }

    private static Command_Action MakeAssignGizmo(Pawn pawn, NecronCommandGroupManager mgr)
    {
        int current = mgr.GetGroupOf(pawn);
        string label = current >= 0
            ? $"{mgr.GetLabel(current)} ✓"
            : "GW40K_CmdGroup_Assign".Translate().ToString();

        return new Command_Action
        {
            defaultLabel = label,
            defaultDesc  = "GW40K_CmdGroup_AssignDesc".Translate(),
            icon         = Gizmo_NecronCommandGroup.AssignIcon,
            action       = () => ShowAssignMenu(pawn, mgr)
        };
    }

    private static void ShowAssignMenu(Pawn pawn, NecronCommandGroupManager mgr)
    {
        int current = mgr.GetGroupOf(pawn);
        List<FloatMenuOption> opts = new List<FloatMenuOption>();

        for (int i = 0; i < NecronCommandGroupManager.GroupCount; i++)
        {
            int captured = i;
            string lbl   = mgr.GetLabel(i);
            if (current == i) lbl += " ✓";
            opts.Add(new FloatMenuOption(lbl, () => mgr.AssignToGroup(pawn, captured)));
        }

        if (current >= 0)
            opts.Add(new FloatMenuOption("GW40K_CmdGroup_Remove".Translate(), () => mgr.RemoveFromAllGroups(pawn)));

        Find.WindowStack.Add(new FloatMenu(opts));
    }
}
