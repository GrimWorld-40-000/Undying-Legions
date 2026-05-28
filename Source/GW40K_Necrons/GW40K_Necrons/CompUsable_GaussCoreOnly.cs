using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Gauss cores are only usable by pawns with a gauss capacitor + gauss energy need.
/// This blocks normal food ingestion behavior for non-gauss users.
/// </summary>
public class CompUsable_GaussCoreOnly : CompUsable
{
    public override AcceptanceReport CanBeUsedBy(Pawn p, bool ignoreErrors = false, bool disposable = false)
    {
        if (p == null)
            return false;

        // Only require the capacitor hediff. The GW40K_NechEnergy need may not be in the
        // needs list for some Necron types (Cryptek, Deathmark) if AddOrRemoveNeedsAsAppropriate
        // was skipped on respawn because the capacitor was already present. DoEffect handles
        // the need==null case gracefully so the dual check here was over-restrictive.
        if (NechEnergyUtility.GetCapacitorComp(p) == null)
            return new AcceptanceReport("Requires a gauss capacitor.");

        // Block manual siphoning when the capacitor is already above 50%.
        // Auto-consumption (JobGiver_GetGaussCore) only triggers at ≤10%, so it never
        // reaches this path at a level high enough to be blocked here.
        Need_NechEnergy gauss = p.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (gauss != null && gauss.CurLevelPercentage >= 0.5f)
            return new AcceptanceReport("GW40K_GaussCapacitorAbove50Pct".Translate());

        // Always pass ignoreErrors=true so forbidden/zone status never blocks a player-issued
        // siphon order from the right-click float menu. Auto-consumption checks forbidden
        // separately via JobGiver_GetGaussCore.IsUsableCoreFor.
        return base.CanBeUsedBy(p, true, disposable);
    }

    /// <summary>
    /// For forbidden cores the base class uses DecoratePrioritizedTask which sets
    /// revalidateClickTarget and causes the float menu to grey out the option.
    /// Instead we build the option manually, unforbidding the core in the click action.
    /// </summary>
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
    {
        if (Props.useJob == null) yield break;

        AcceptanceReport report = CanBeUsedBy(myPawn, Props.ignoreOtherReservations);
        string label = Props.useLabel.Formatted(parent);

        FloatMenuOption option;
        if (parent.IsForbidden(myPawn))
        {
            option = new FloatMenuOption(label, () =>
            {
                parent.SetForbidden(false, false);
                TryStartUseJob(myPawn, GetExtraTarget(myPawn), Props.ignoreOtherReservations);
            });
        }
        else
        {
            option = FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                    TryStartUseJob(myPawn, GetExtraTarget(myPawn), Props.ignoreOtherReservations)),
                myPawn, parent);
        }

        if (!report.Accepted)
        {
            option.Disabled = true;
            option.Label = option.Label + " (" + report.Reason + ")";
        }

        yield return option;
    }
}

public class CompProperties_UseEffectGaussCoreConsume : CompProperties_UseEffect
{
    public float energyAmount = 90f;

    public CompProperties_UseEffectGaussCoreConsume()
    {
        compClass = typeof(CompUseEffect_GaussCoreConsume);
    }
}

public class CompUseEffect_GaussCoreConsume : CompUseEffect
{
    private CompProperties_UseEffectGaussCoreConsume PropsTyped => (CompProperties_UseEffectGaussCoreConsume)props;

    public override void DoEffect(Pawn user)
    {
        base.DoEffect(user);

        if (user == null || parent == null || parent.Destroyed)
            return;

        HediffComp_GaussCapacitor cap = NechEnergyUtility.GetCapacitorComp(user);
        if (cap == null || cap.Props.capacity <= 0f)
            return;

        // Refresh needs in case the need was absent (no weapon equipped when the
        // pawn picked up the capacitor) and has now been added by ShouldHaveNeed.
        user.needs?.AddOrRemoveNeedsAsAppropriate();
        Need need = user.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        if (need == null)
            return;

        float delta = PropsTyped.energyAmount / cap.Props.capacity;
        need.CurLevel = Mathf.Clamp(need.CurLevel + delta, 0f, need.MaxLevel);

        Thing consumed = parent.SplitOff(1);
        consumed?.Destroy();
    }
}

/*
/// <summary>
/// Restores the "Pick up" float menu option for Gauss Cores. Vanilla's pickup
/// provider is suppressed for items with CompUsable when CanBeUsedBy fails,
/// so any pawn that lacks a gauss capacitor would otherwise have no way to
/// pick up the item at all.
/// </summary>
public class GaussCorePickupMenuProvider : FloatMenuOptionProvider
{
    protected override bool Drafted   => true;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;

    public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
    {
        Pawn pawn = context.FirstSelectedPawn;
        if (pawn == null)
            yield break;

        foreach (Thing thing in context.ClickedThings)
        {
            if (thing?.def?.defName != "GW40K_GaussCore")
                continue;

            int pickCount = Mathf.Max(1, Mathf.Min(thing.stackCount,
                MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing)));
            TaggedString label = pickCount == 1
                ? "PickUpOne".Translate(thing.Named("1"))
                : "PickUpAll".Translate(thing.Named("1"));

            if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                continue;
            }

            if (!pawn.CanReserve(thing))
            {
                yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
                continue;
            }

            Thing thingCapture = thing;
            int countCapture = pickCount;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                {
                    Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, thingCapture);
                    job.count = countCapture;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                pawn, thingCapture);
        }
    }
}
*/
