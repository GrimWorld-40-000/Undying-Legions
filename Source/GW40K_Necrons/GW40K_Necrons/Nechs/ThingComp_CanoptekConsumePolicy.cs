using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Per-pawn filter for which things this Canoptek construct may consume for necrodermis (UI + job logic).
/// Defaults to nothing allowed; players enable categories/items like a stockpile policy.
/// Optional buffer; consume job is instant after staging, but eject / tick-rare cleanup still use this if needed.
/// </summary>
public class ThingComp_CanoptekConsumePolicy : ThingComp, IThingHolder
{
    private const string ConsumeJobDefName = "GW40K_Job_CanoptekConsume";
    private const int EjectCooldownTicks = 300;
    // HasAnyRepairTarget scans map pawns and structures — only run at this interval, staggered by pawn ID.
    private const int RepairScanIntervalTicks = 2000;
    private int nextRepairScanTick = -1;
    /// <summary>After a consume cycle completes, wait this long before the think tree can assign another consume job.</summary>
    private const int PostConsumeReengageDelayTicks = 120;
    public ThingFilter consumeFilter;

    /// <summary>Internal buffer while a consume job is processing a haulable target.</summary>
    public ThingOwner<Thing> innerContainer;
    private int loadingStartTick = -1;
    private int loadingDurationTicks;
    private int consumeCooldownUntilTick;
    private int lastEjectedThingId = -1;
    private int postConsumeBlockedUntilTick;
    private bool ejectSuppressed;

    public static ThingFilter FilterFor(Pawn pawn) =>
        pawn?.TryGetComp<ThingComp_CanoptekConsumePolicy>()?.consumeFilter;

    public ThingOwner GetDirectlyHeldThings() => innerContainer;

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
    }

    public IntVec3 GetRootHolderOrMapCell()
    {
        Pawn p = parent as Pawn;
        return p != null && p.Spawned ? p.Position : IntVec3.Invalid;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        innerContainer ??= new ThingOwner<Thing>(this);
        if (consumeFilter == null)
            InitDefaultFilter();
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        ClearLoadingIfInvalid();
        if (parent is not Pawn pawn || !pawn.Spawned || pawn.Map == null)
            return;
        TickAutoMode(pawn);
        if (innerContainer == null || innerContainer.Count == 0)
            return;
        // Never spill while this job driver is active (covers any CurJobDef / prefix edge cases).
        if (pawn.jobs?.curDriver is JobDriver_CanoptekConsume)
            return;
        if (pawn.CurJobDef != null && pawn.CurJobDef.defName == ConsumeJobDefName)
            return;

        List<Thing> snapshot = new List<Thing>();
        foreach (Thing t in innerContainer)
        {
            if (t != null && !t.Destroyed)
                snapshot.Add(t);
        }

        foreach (Thing t in snapshot)
        {
            innerContainer.Remove(t);
            if (!GenPlace.TryPlaceThing(t, pawn.Position, pawn.Map, ThingPlaceMode.Near)
                && !innerContainer.TryAdd(t, canMergeWithExistingStacks: true))
            {
                if (!t.Destroyed)
                    t.Destroy(DestroyMode.Vanish);
            }
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        ClearLoadingIfInvalid();
        ClearExpiredEjectCooldown();
        ClearExpiredPostConsumeBlock();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        SpillOrDestroyHeldOnOwnerLoss(previousMap);
    }

    public void BeginLoading(int durationTicks)
    {
        loadingDurationTicks = Mathf.Max(0, durationTicks);
        loadingStartTick = loadingDurationTicks > 0 ? Find.TickManager.TicksGame : -1;
    }

    public void EndLoading()
    {
        loadingStartTick = -1;
        loadingDurationTicks = 0;
    }

    public bool TryGetLoadingProgress(out float progress01)
    {
        if (!IsLoadingNow())
        {
            progress01 = 0f;
            return false;
        }

        int elapsed = Find.TickManager.TicksGame - loadingStartTick;
        progress01 = Mathf.Clamp01(elapsed / (float)loadingDurationTicks);
        return true;
    }

    public Thing GetCurrentConsumedThing()
    {
        if (innerContainer != null)
        {
            foreach (Thing held in innerContainer)
            {
                if (held != null && !held.Destroyed)
                    return held;
            }
        }

        if (parent is Pawn pawn
            && pawn.CurJobDef?.defName == ConsumeJobDefName
            && pawn.CurJob?.targetA.HasThing == true)
        {
            Thing t = pawn.CurJob.targetA.Thing;
            if (t != null && !t.Destroyed)
                return t;
        }

        return null;
    }

    public void SetEjectSuppressed(bool suppressed) => ejectSuppressed = suppressed;

    public bool IsEjectSuppressed() => ejectSuppressed;

    public bool CanEjectNow(Thing currentThing, out string reason)
    {
        if (!IsLinkedByCommandNode())
        {
            reason = "Requires Command Node link.";
            return false;
        }

        if (currentThing == null)
        {
            reason = "No active consumed item.";
            return false;
        }

        if (currentThing is Plant)
        {
            reason = "Plants cannot be ejected once consume has started.";
            return false;
        }

        if (ejectSuppressed)
        {
            reason = "Cannot eject, nothing held.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryEjectCurrentConsumedThing()
    {
        if (parent is not Pawn pawn)
            return false;
        Thing ejectedThing = GetCurrentConsumedThing();
        if (!CanEjectNow(ejectedThing, out _))
            return false;
        bool changed = false;
        if (innerContainer != null && innerContainer.Count > 0 && pawn.Spawned && pawn.Map != null)
        {
            List<Thing> tmp = new List<Thing>();
            foreach (Thing t in innerContainer)
            {
                if (t != null && !t.Destroyed)
                    tmp.Add(t);
            }

            foreach (Thing t in tmp)
            {
                innerContainer.Remove(t);
                GenPlace.TryPlaceThing(t, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                changed = true;
            }
        }

        if (pawn.CurJobDef?.defName == ConsumeJobDefName && pawn.jobs != null)
        {
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            changed = true;
        }

        EndLoading();
        if (changed)
            BeginEjectCooldown(ejectedThing);
        return changed;
    }

    public bool IsLinkedByCommandNode()
    {
        if (parent is not Pawn pawn)
            return false;
        return HediffComp_ControlNodeTracker.GetControllerOfConstruct(pawn) != null;
    }

    public bool IsConsumeOnCooldown()
    {
        ClearExpiredEjectCooldown();
        return Find.TickManager.TicksGame < consumeCooldownUntilTick;
    }

    /// <summary>True while the short delay after a finished consume cycle is still running (blocks new consume jobs).</summary>
    public bool IsPostConsumeBlocked()
    {
        ClearExpiredPostConsumeBlock();
        return Find.TickManager.TicksGame < postConsumeBlockedUntilTick;
    }

    public void BeginPostConsumeReengageDelay(int delayTicks = PostConsumeReengageDelayTicks)
    {
        postConsumeBlockedUntilTick = Find.TickManager.TicksGame + Mathf.Max(0, delayTicks);
    }

    public bool IsTemporarilyInvalidTarget(Thing thing)
    {
        if (thing == null || !IsConsumeOnCooldown() || lastEjectedThingId < 0)
            return false;
        return thing.thingIDNumber == lastEjectedThingId;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref consumeFilter, "consumeFilter");
        Scribe_Deep.Look(ref innerContainer, "consumeInner", this);
        Scribe_Values.Look(ref loadingStartTick, "consumeLoadingStartTick", -1);
        Scribe_Values.Look(ref loadingDurationTicks, "consumeLoadingDurationTicks", 0);
        Scribe_Values.Look(ref consumeCooldownUntilTick, "consumeCooldownUntilTick", 0);
        Scribe_Values.Look(ref lastEjectedThingId, "lastEjectedThingId", -1);
        Scribe_Values.Look(ref postConsumeBlockedUntilTick, "postConsumeBlockedUntilTick", 0);
        Scribe_Values.Look(ref ejectSuppressed, "consumeEjectSuppressed", false);
        if (consumeFilter == null)
            InitDefaultFilter();
        innerContainer ??= new ThingOwner<Thing>(this);
    }

    private void TickAutoMode(Pawn pawn)
    {
        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        if (modes == null || !modes.GetAutoMode(pawn))
            return;
        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null)
            return;

        // While under attack, preserve the current mode and let the Animal think tree fight.
        // The 30-second window (1800 ticks) resets on every hit, so combat stays suppressed
        // as long as the scarab keeps taking damage. When 30s pass with no hits, mode resumes.
        if (ScarabRaidDutyUtility.IsInRecentCombat(pawn))
            return;

        float pct = need.CurLevelPercentage;
        ControlNodeMode current = modes.GetMode(pawn, ControlNodeMode.Consume);

        // Critically low: consume from any mode — cheap check, runs every rare tick.
        if (pct < 0.05f)
        {
            if (current != ControlNodeMode.Consume)
                modes.SetMode(pawn, ControlNodeMode.Consume);
            return;
        }

        // Post-combat self-repair: if the scarab is critically damaged (≤2 units alive = ≤50% HP)
        // and has enough necrodermis to repair with (>50%), prioritise self-repair regardless of
        // what mode it was in before combat.
        if (ScarabSelfDestructUtility.AliveUnitCount(pawn) <= 2 && pct > 0.5f
            && current != ControlNodeMode.Repair)
        {
            modes.SetMode(pawn, ControlNodeMode.Repair);
            return;
        }

        // Throttle the expensive map scan to every ~30s, staggered across scarabs.
        if (nextRepairScanTick < 0)
            nextRepairScanTick = Find.TickManager.TicksGame + (pawn.thingIDNumber % RepairScanIntervalTicks);
        if (Find.TickManager.TicksGame < nextRepairScanTick)
            return;
        nextRepairScanTick = Find.TickManager.TicksGame + RepairScanIntervalTicks;

        bool hasRepair = JobGiver_CanoptekRepair.HasAnyRepairTarget(pawn);

        if (current == ControlNodeMode.Consume && pct > 0.95f)
        {
            // Full necro idling in Consume: activate repair or work.
            modes.SetMode(pawn, hasRepair ? ControlNodeMode.Repair : ControlNodeMode.Work);
        }
        else if (current == ControlNodeMode.Repair && !hasRepair)
        {
            // Repairs exhausted (at any necro level): settle into Work.
            // Work is a stable destination — never auto-switch back to Repair from Work.
            modes.SetMode(pawn, ControlNodeMode.Work);
        }
    }

    private void InitDefaultFilter()
    {
        consumeFilter = new ThingFilter();
        consumeFilter.ResolveReferences();
        consumeFilter.SetDisallowAll();

        // Enemy scarabs should aggressively consume any valid parent-filter item by default.
        if (parent is Pawn pawn
            && pawn.Faction != null
            && Faction.OfPlayer != null
            && pawn.Faction.HostileTo(Faction.OfPlayer))
        {
            ThingFilter parentFilter = CanoptekConsumePolicyParentFilter.Instance;
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d != null && parentFilter.Allows(d))
                    consumeFilter.SetAllow(d, true);
            }
        }
    }

    private bool IsLoadingNow()
    {
        if (loadingDurationTicks <= 0 || loadingStartTick < 0 || parent is not Pawn pawn)
            return false;
        if (pawn.CurJobDef?.defName != ConsumeJobDefName)
            return false;
        return Find.TickManager.TicksGame < loadingStartTick + loadingDurationTicks;
    }

    private void ClearLoadingIfInvalid()
    {
        if (!IsLoadingNow())
            EndLoading();
    }

    private void BeginEjectCooldown(Thing ejectedThing)
    {
        consumeCooldownUntilTick = Find.TickManager.TicksGame + EjectCooldownTicks;
        lastEjectedThingId = ejectedThing?.thingIDNumber ?? -1;
    }

    private void ClearExpiredEjectCooldown()
    {
        if (consumeCooldownUntilTick > 0 && Find.TickManager.TicksGame >= consumeCooldownUntilTick)
        {
            consumeCooldownUntilTick = 0;
            lastEjectedThingId = -1;
        }
    }

    private void ClearExpiredPostConsumeBlock()
    {
        if (postConsumeBlockedUntilTick > 0 && Find.TickManager.TicksGame >= postConsumeBlockedUntilTick)
            postConsumeBlockedUntilTick = 0;
    }

    /// <summary>
    /// Safety net: if the construct dies/is destroyed, release held consume items.
    /// Plants are always destroyed; other things are dropped near the last map position when possible.
    /// </summary>
    private void SpillOrDestroyHeldOnOwnerLoss(Map previousMap)
    {
        if (innerContainer == null || innerContainer.Count == 0)
            return;

        Pawn pawn = parent as Pawn;
        IntVec3 dropCell = pawn != null && pawn.Position.IsValid ? pawn.Position : IntVec3.Invalid;
        Map dropMap = previousMap ?? pawn?.Map;

        List<Thing> snapshot = new List<Thing>();
        foreach (Thing t in innerContainer)
        {
            if (t != null && !t.Destroyed)
                snapshot.Add(t);
        }

        foreach (Thing t in snapshot)
        {
            innerContainer.Remove(t);
            if (t is Plant || dropMap == null || !dropCell.IsValid)
            {
                if (!t.Destroyed)
                    t.Destroy(DestroyMode.Vanish);
                continue;
            }

            GenPlace.TryPlaceThing(t, dropCell, dropMap, ThingPlaceMode.Near);
        }
    }
}
