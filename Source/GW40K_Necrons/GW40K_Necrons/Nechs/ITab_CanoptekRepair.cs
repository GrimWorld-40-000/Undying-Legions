using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Inspect tab: toggle which autonomous Repair-mode targets this Canoptek may service.
/// </summary>
public class ITab_CanoptekRepair : ITab
{
    private static readonly Vector2 WinSize = new Vector2(320f, 340f);

    private ThingComp_CanoptekRepairPolicy Comp =>
        SelPawn?.TryGetComp<ThingComp_CanoptekRepairPolicy>();

    public ITab_CanoptekRepair()
    {
        size = WinSize;
        labelKey = "GW40K_TabCanoptekRepair";
    }

    public override bool IsVisible =>
        SelPawn != null
        && SelPawn.Faction == Faction.OfPlayer
        && Comp != null;

    protected override void FillTab()
    {
        if (Comp == null)
            return;

        // Start the outer group first so copy/paste can sit above the grey panel,
        // matching the layout of ITab_CanoptekConsume.
        Rect outerRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f).ContractedBy(6f);
        const float closeButtonClearTop   = 14f;
        const float closeButtonClearRight = -4f;
        outerRect.y      += closeButtonClearTop;
        outerRect.height -= closeButtonClearTop;
        outerRect.width  -= closeButtonClearRight;

        GUI.BeginGroup(outerRect);

        const float rowH   = 28f;
        const float rowGap = 6f;
        float btnW = (outerRect.width - rowGap) * 0.5f;

        // ── Copy / Paste — above the grey panel (mirrors ITab_CanoptekConsume) ──
        Rect copyBtn  = new Rect(0f,           0f, btnW, rowH);
        Rect pasteBtn = new Rect(btnW + rowGap, 0f, btnW, rowH);

        if (Widgets.ButtonText(copyBtn, "GW40K_CanoptekRepairCopy".Translate()))
        {
            CanoptekRepairPolicyClipboard.CopyFrom(Comp, SelPawn?.LabelShortCap);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        TooltipHandler.TipRegion(copyBtn, "GW40K_CanoptekRepairCopyTip".Translate());

        bool canPaste = CanoptekRepairPolicyClipboard.HasData;
        if (!canPaste) GUI.color = Color.gray;
        if (Widgets.ButtonText(pasteBtn, "GW40K_CanoptekRepairPaste".Translate()) && canPaste)
        {
            CanoptekRepairPolicyClipboard.TryPasteTo(Comp);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        GUI.color = Color.white;
        TooltipHandler.TipRegion(pasteBtn, () =>
            canPaste
                ? "GW40K_CanoptekRepairPasteTip".Translate(CanoptekRepairPolicyClipboard.SourceLabel).Resolve()
                : "GW40K_CanoptekRepairPasteEmpty".Translate().Resolve(),
            91822);

        // ── Grey panel — Clear/Allow + checkboxes ────────────────────────────
        float panelTop = rowH + rowGap;
        Rect menuRect  = new Rect(0f, panelTop, outerRect.width, outerRect.height - panelTop);
        Widgets.DrawMenuSection(menuRect);
        Rect inner = menuRect.ContractedBy(6f);
        GUI.BeginGroup(inner);

        float curY = 0f;
        Rect clearBtn = new Rect(0f,           curY, btnW, rowH);
        Rect allowBtn = new Rect(btnW + rowGap, curY, btnW, rowH);
        if (Widgets.ButtonText(clearBtn, "GW40K_CanoptekRepairClearAll".Translate()))
            Comp.SetAll(false);
        if (Widgets.ButtonText(allowBtn, "GW40K_CanoptekRepairAllowAll".Translate()))
            Comp.SetAll(true);
        curY += rowH + 8f;

        Listing_Standard list = new Listing_Standard();
        list.Begin(new Rect(0f, curY, inner.width, inner.height - curY));
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleSelf".Translate(),             ref Comp.allowSelf);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleFriendlyNecrons".Translate(),  ref Comp.allowFriendlyNecrons);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleFriendlyMechs".Translate(),    ref Comp.allowFriendlyMechs);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleNecronStructures".Translate(), ref Comp.allowNecronStructures);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleStructures".Translate(),        ref Comp.allowStructures);
        list.Gap(8f);
        list.Label("GW40K_CanoptekRepairStructuresNote".Translate());
        list.End();

        GUI.EndGroup(); // inner
        GUI.EndGroup(); // outerRect
    }
}
