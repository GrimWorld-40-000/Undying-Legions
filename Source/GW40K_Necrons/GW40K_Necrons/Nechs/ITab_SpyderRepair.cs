using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Inspect tab on the Canoptek Spyder: configures the repair-policy template that is applied to
/// all linked scarabs on connection and pushed live whenever the player changes a setting here.
///
/// The Spyder owns its own <see cref="ThingComp_CanoptekRepairPolicy"/> (added via XML comp) which
/// acts as the master template. On every checkbox change the tab calls
/// <see cref="PushToScarabs"/> to propagate the new value to every linked scarab immediately.
/// Initial push on link creation is handled by <see cref="HediffComp_ControlNodeTracker.BindScarab"/>.
/// </summary>
public class ITab_SpyderRepair : ITab
{
    private static readonly Vector2 WinSize = new Vector2(320f, 340f);

    private ThingComp_CanoptekRepairPolicy Comp =>
        SelPawn?.TryGetComp<ThingComp_CanoptekRepairPolicy>();

    private HediffComp_ControlNodeTracker Tracker =>
        HediffComp_ControlNodeTracker.GetTracker(SelPawn);

    public ITab_SpyderRepair()
    {
        size     = WinSize;
        labelKey = "GW40K_TabSpyderRepair";
    }

    public override bool IsVisible =>
        SelPawn != null
        && SelPawn.Faction == Faction.OfPlayer
        && Comp != null;

    protected override void FillTab()
    {
        ThingComp_CanoptekRepairPolicy comp = Comp;
        if (comp == null) return;

        // Outer group matches ITab_CanoptekConsume exactly: ContractedBy(10f), copy/paste at y=0.
        Rect outerRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);

        GUI.BeginGroup(outerRect);

        const float rowH   = 24f;
        const float rowGap = 4f;
        float btnW = (outerRect.width - rowGap) * 0.25f;

        // ── Copy / Paste — above the grey panel ──────────────────────────────
        Rect copyBtn  = new Rect(0f,           0f, btnW, rowH);
        Rect pasteBtn = new Rect(btnW + rowGap, 0f, btnW, rowH);

        if (Widgets.ButtonText(copyBtn, "GW40K_CanoptekRepairCopy".Translate()))
        {
            CanoptekRepairPolicyClipboard.CopyFrom(comp, SelPawn?.LabelShortCap);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        TooltipHandler.TipRegion(copyBtn, "GW40K_CanoptekRepairCopyTip".Translate());

        bool canPaste = CanoptekRepairPolicyClipboard.HasData;
        if (!canPaste) GUI.color = Color.gray;
        if (Widgets.ButtonText(pasteBtn, "GW40K_CanoptekRepairPaste".Translate()) && canPaste)
        {
            CanoptekRepairPolicyClipboard.TryPasteTo(comp);
            PushToScarabs(comp);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        GUI.color = Color.white;
        TooltipHandler.TipRegion(pasteBtn, () =>
            canPaste
                ? "GW40K_CanoptekRepairPasteTip".Translate(CanoptekRepairPolicyClipboard.SourceLabel).Resolve()
                : "GW40K_CanoptekRepairPasteEmpty".Translate().Resolve(),
            91823); // unique tooltip ID; must not collide with the scarab tab's ID (91822)

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
        {
            comp.SetAll(false);
            PushToScarabs(comp);
        }
        if (Widgets.ButtonText(allowBtn, "GW40K_CanoptekRepairAllowAll".Translate()))
        {
            comp.SetAll(true);
            PushToScarabs(comp);
        }
        curY += rowH + 8f;

        // ── Checkboxes (snapshot → draw → detect change → push) ─────────────
        bool snapSelf      = comp.allowSelf;
        bool snapNecrons   = comp.allowFriendlyNecrons;
        bool snapMechs     = comp.allowFriendlyMechs;
        bool snapNecStruct = comp.allowNecronStructures;
        bool snapStructs   = comp.allowStructures;

        Listing_Standard list = new Listing_Standard();
        list.Begin(new Rect(0f, curY, inner.width, inner.height - curY));
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleSelf".Translate(),             ref comp.allowSelf);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleFriendlyNecrons".Translate(),  ref comp.allowFriendlyNecrons);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleFriendlyMechs".Translate(),    ref comp.allowFriendlyMechs);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleNecronStructures".Translate(), ref comp.allowNecronStructures);
        list.CheckboxLabeled("GW40K_CanoptekRepairToggleStructures".Translate(),        ref comp.allowStructures);
        list.Gap(8f);
        list.Label("GW40K_CanoptekRepairStructuresNote".Translate());
        list.End();

        bool changed =
            comp.allowSelf             != snapSelf      ||
            comp.allowFriendlyNecrons  != snapNecrons   ||
            comp.allowFriendlyMechs    != snapMechs     ||
            comp.allowNecronStructures != snapNecStruct ||
            comp.allowStructures       != snapStructs;
        if (changed)
            PushToScarabs(comp);

        GUI.EndGroup(); // inner
        GUI.EndGroup(); // outerRect
    }

    /// <summary>
    /// Copies the Spyder's current repair policy to every live scarab linked to its control node.
    /// Scarabs without a <see cref="ThingComp_CanoptekRepairPolicy"/> are silently skipped.
    /// </summary>
    private void PushToScarabs(ThingComp_CanoptekRepairPolicy spyderPolicy)
    {
        HediffComp_ControlNodeTracker tracker = Tracker;
        if (tracker == null) return;
        for (int i = 0; i < tracker.controlledScarabs.Count; i++)
        {
            Pawn scarab = tracker.controlledScarabs[i];
            if (scarab == null || scarab.Dead || scarab.Destroyed) continue;
            ThingComp_CanoptekRepairPolicy sp = scarab.TryGetComp<ThingComp_CanoptekRepairPolicy>();
            if (sp == null) continue;
            sp.allowSelf             = spyderPolicy.allowSelf;
            sp.allowFriendlyNecrons  = spyderPolicy.allowFriendlyNecrons;
            sp.allowFriendlyMechs    = spyderPolicy.allowFriendlyMechs;
            sp.allowNecronStructures = spyderPolicy.allowNecronStructures;
            sp.allowStructures       = spyderPolicy.allowStructures;
        }
    }
}
