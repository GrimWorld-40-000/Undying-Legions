using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Inspect tab: same layout as vanilla storage filter (shelf-style: no priority row), for Canoptek consume policy.
/// </summary>
public class ITab_CanoptekConsume : ITab
{
    /// <summary>RimWorld 1.6+ filter UI scroll/search state (replaces ref Vector2 scrollPosition).</summary>
    private readonly ThingFilterUI.UIState consumeUiState = new ThingFilterUI.UIState();

    private static readonly Vector2 WinSize = new Vector2(300f, 480f);

    private ThingComp_CanoptekConsumePolicy Comp =>
        SelPawn?.TryGetComp<ThingComp_CanoptekConsumePolicy>();

    public ITab_CanoptekConsume()
    {
        size = WinSize;
        labelKey = "GW40K_TabCanoptekConsume";
    }

    public override bool IsVisible =>
        SelPawn != null
        && SelPawn.Faction == Faction.OfPlayer
        && Comp != null;

    protected override void FillTab()
    {
        ThingFilter filter = Comp?.consumeFilter;
        if (filter == null)
            return;

        Rect position = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
        GUI.BeginGroup(position);

        const float copyPasteH = 24f;
        const float rowGap = 4f;
        const float btnGap = 4f;
        float btnW = (position.width - btnGap) * 0.25f;
        Rect copyBtn = new Rect(0f, 0f, btnW, copyPasteH);
        Rect pasteBtn = new Rect(btnW + btnGap, 0f, btnW, copyPasteH);

        if (Widgets.ButtonText(copyBtn, "GW40K_CanoptekConsumeCopy".Translate()))
        {
            CanoptekConsumePolicyClipboard.CopyFrom(filter, SelPawn?.LabelShortCap);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        TooltipHandler.TipRegion(copyBtn, "GW40K_CanoptekConsumeCopyTip".Translate());

        bool canPaste = CanoptekConsumePolicyClipboard.HasData;
        if (!canPaste)
            GUI.color = Color.gray;
        if (Widgets.ButtonText(pasteBtn, "GW40K_CanoptekConsumePaste".Translate()) && canPaste)
        {
            CanoptekConsumePolicyClipboard.TryPasteTo(filter);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        GUI.color = Color.white;
        TooltipHandler.TipRegion(pasteBtn, () =>
            canPaste
                ? "GW40K_CanoptekConsumePasteTip".Translate(CanoptekConsumePolicyClipboard.SourceLabel).Resolve()
                : "GW40K_CanoptekConsumePasteEmpty".Translate().Resolve(),
            91821);

        Rect filterRect = new Rect(0f, copyPasteH + rowGap, position.width, position.height - copyPasteH - rowGap);
        ThingFilterUI.DoThingFilterConfigWindow(
            filterRect,
            consumeUiState,
            filter,
            CanoptekConsumePolicyParentFilter.Instance,
            8,
            null,
            null,
            forceHideHitPointsConfig: false,
            forceHideQualityConfig: false,
            showMentalBreakChanceRange: false,
            suppressSmallVolumeTags: null,
            map: null);

        if (NecronDefOfs.GW_UD_Concept_CanoptekConsume != null)
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(NecronDefOfs.GW_UD_Concept_CanoptekConsume, KnowledgeAmount.FrameDisplayed);

        GUI.EndGroup();
    }
}
