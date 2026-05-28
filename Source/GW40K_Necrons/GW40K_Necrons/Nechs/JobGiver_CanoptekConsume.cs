using System;
using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// When a Canoptek construct is in Consume mode, necrodermis is below 98%, and nothing is already held for consumption,
/// find the nearest allowed thing with positive necrodermis yield and start <see cref="JobDriver_CanoptekConsume"/>.
/// </summary>
public class JobGiver_CanoptekConsume : ThinkNode_JobGiver
{
    private const float NecroMaxFraction = 0.98f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null || pawn.Drafted)
            return null;
        if (!NecrodermisIngestionUtility.IsCanoptek(pawn))
            return null;

        // Never consume during siege/raids — enemy scarabs in a lord would otherwise forage.
        if (pawn.GetLord() != null && pawn.Faction != Faction.OfPlayer)
            return null;

        // Yield to the Animal think tree while under attack.
        if (ScarabRaidDutyUtility.IsInRecentCombat(pawn)) return null;

        GameComponent_CanoptekConstructModes store = GameComponent_CanoptekConstructModes.Current;
        ControlNodeMode mode = store?.GetMode(pawn) ?? ControlNodeMode.Consume;
        if (mode != ControlNodeMode.Consume)
            return null;

        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel >= need.MaxLevel * NecroMaxFraction)
            return null;
        float remainingCapacity = Math.Max(0f, need.MaxLevel - need.CurLevel);
        if (remainingCapacity < 1E-4f)
            return null;

        ThingComp_CanoptekConsumePolicy comp = pawn.TryGetComp<ThingComp_CanoptekConsumePolicy>();
        if (comp == null)
            return null;
        if (comp.IsConsumeOnCooldown())
            return null;
        if (comp.IsPostConsumeBlocked())
            return null;
        comp.innerContainer ??= new ThingOwner<Thing>(comp);
        if (comp.innerContainer.Count > 0)
            return null;

        if (pawn.CurJobDef == NecronDefOfs.GW40K_Job_CanoptekConsume)
            return null;

        ThingFilter filter = comp.consumeFilter;
        if (filter == null)
            return null;

        Thing best = FindBestTarget(pawn, filter, comp, remainingCapacity);
        if (best == null)
            return null;

        float gain = CanoptekConsumeNecrodermisMath.GetNecrodermisGain(best, CanoptekConsumeNecrodermisMath.EffectiveConsumeStackCount(best));
        if (gain < 1E-4f || gain > remainingCapacity + 1E-4f)
            return null;

        return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_CanoptekConsume, best);
    }

    private static Thing FindBestTarget(Pawn pawn, ThingFilter filter, ThingComp_CanoptekConsumePolicy comp, float remainingCapacity)
    {
        TraverseParms traverse = TraverseParms.For(pawn);
        float maxDist = 9999f;

        Thing haul = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
            PathEndMode.ClosestTouch,
            traverse,
            maxDist,
            t => IsValidConsumeTarget(pawn, filter, t, comp, remainingCapacity));

        Thing plant = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.Plant),
            PathEndMode.ClosestTouch,
            traverse,
            maxDist,
            t => IsValidConsumeTarget(pawn, filter, t, comp, remainingCapacity) && t is Plant);

        if (haul == null)
            return plant;
        if (plant == null)
            return haul;
        float dh = pawn.Position.DistanceToSquared(haul.Position);
        float dp = pawn.Position.DistanceToSquared(plant.Position);
        return dh <= dp ? haul : plant;
    }

    private static bool IsValidConsumeTarget(Pawn pawn, ThingFilter filter, Thing t, ThingComp_CanoptekConsumePolicy comp, float remainingCapacity)
    {
        if (t == null || !t.Spawned || t.Map != pawn.Map)
            return false;
        if (t is Blueprint || t is Frame)
            return false;
        if (comp != null && comp.IsTemporarilyInvalidTarget(t))
            return false;
        if (!filter.Allows(t) || !CanoptekConsumePolicyParentFilter.Instance.Allows(t))
            return false;
        if (t.IsForbidden(pawn))
            return false;
        if (!pawn.CanReserve(t))
            return false;
        float gain = CanoptekConsumeNecrodermisMath.GetNecrodermisGain(t, CanoptekConsumeNecrodermisMath.EffectiveConsumeStackCount(t));
        if (gain < 1E-4f || gain > remainingCapacity + 1E-4f)
            return false;
        return true;
    }
}
