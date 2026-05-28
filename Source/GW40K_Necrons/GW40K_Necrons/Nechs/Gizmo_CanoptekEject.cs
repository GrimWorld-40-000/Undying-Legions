using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Ability-style eject command for Canoptek consume jobs.
/// Extra item health bars render above the icon and grow upward, so bottom alignment matches other ability icons.
/// </summary>
public class Gizmo_CanoptekEject : Command_Action
{
    private static readonly Color ItemHealthFillColor = new Color(0.86f, 0.66f, 0.24f);
    private static readonly Color ItemHealthEmptyColor = new Color(0.12f, 0.12f, 0.12f);
    private const float ItemProgressRowH = 18f;

    private readonly Pawn pawn;
    private readonly ThingComp_CanoptekConsumePolicy consumeComp;

    public Gizmo_CanoptekEject(Pawn pawn, ThingComp_CanoptekConsumePolicy consumeComp)
    {
        this.pawn = pawn;
        this.consumeComp = consumeComp;
        defaultLabel = "Eject";
        defaultDesc = "Eject currently consumed item.";
        icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower");
        action = TryEject;
    }

    public override float GetWidth(float maxWidth) => 75f;

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        List<Thing> extraItems = new List<Thing>();
        Thing currentThing = consumeComp?.GetCurrentConsumedThing();
        if (consumeComp?.innerContainer != null)
        {
            foreach (Thing held in consumeComp.innerContainer)
            {
                if (held != null && !held.Destroyed && held != currentThing)
                    extraItems.Add(held);
            }
        }

        float extraHeight = extraItems.Count * ItemProgressRowH;
        float rowY = topLeft.y - extraHeight;
        for (int i = 0; i < extraItems.Count; i++)
        {
            Thing t = extraItems[i];
            float hpPct = t.def.useHitPoints
                ? Mathf.Clamp01(t.HitPoints / Mathf.Max(1f, t.MaxHitPoints))
                : 1f;
            string hpText = t.def.useHitPoints
                ? $"{t.HitPoints}/{t.MaxHitPoints}"
                : $"x{t.stackCount}";
            Rect labelRect = new Rect(topLeft.x, rowY, GetWidth(maxWidth), 8f);
            Rect barRect = new Rect(topLeft.x + 4f, rowY + 8f, GetWidth(maxWidth) - 8f, 8f);
            Widgets.Label(labelRect, hpText);
            Widgets.FillableBar(
                barRect,
                hpPct,
                SolidColorMaterials.NewSolidColorTexture(ItemHealthFillColor),
                SolidColorMaterials.NewSolidColorTexture(ItemHealthEmptyColor),
                false);
            rowY += ItemProgressRowH;
        }

        bool linkedByNode = consumeComp?.IsLinkedByCommandNode() == true;
        string cannotEjectReason = "No active consumed item.";
        bool canEject = consumeComp != null && consumeComp.CanEjectNow(currentThing, out cannotEjectReason);

        if (currentThing != null && currentThing.def?.uiIcon != null)
            icon = currentThing.def.uiIcon;
        else
            icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower");

        disabled = false;
        disabledReason = string.Empty;
        if (!canEject)
        {
            if (!linkedByNode && string.IsNullOrEmpty(cannotEjectReason))
                Disable("Requires Command Node link.");
            else
                Disable(cannotEjectReason);
        }

        return base.GizmoOnGUI(topLeft, maxWidth, parms);
    }

    private void TryEject()
    {
        if (consumeComp == null)
            return;
        if (consumeComp.TryEjectCurrentConsumedThing())
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }
}
