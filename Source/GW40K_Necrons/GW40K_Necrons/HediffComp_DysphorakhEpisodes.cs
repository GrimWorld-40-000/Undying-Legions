using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class HediffCompProperties_DysphorakhEpisodes : HediffCompProperties
{
    /// <summary>Mean-time between the three simple episodes (suffocation, hunger, catatonia).</summary>
    public float episodeMtbDays = 7f;

    /// <summary>Mean-time between Compulsive Clawing episodes (independent roll).</summary>
    public float clawingMtbDays = 18f;

    /// <summary>Minimum gap in days after any episode before the next can fire.</summary>
    public float episodeCooldownDays = 3f;

    public HediffCompProperties_DysphorakhEpisodes()
    {
        compClass = typeof(HediffComp_DysphorakhEpisodes);
    }
}

/// <summary>
/// Master controller for Dysphorakh episodes. Rolls two independent MTB tracks each tick:
/// <list type="bullet">
///   <item>Random episode: Suffocation, Phantom Hunger, or Sensory Disconnect (catatonic).</item>
///   <item>Compulsive Clawing: the escalating three-stage break that can end in Flayed One conversion.</item>
/// </list>
/// A shared cooldown gate prevents back-to-back episodes.
/// </summary>
public class HediffComp_DysphorakhEpisodes : HediffComp
{
    private int cooldownTicks;

    private HediffCompProperties_DysphorakhEpisodes Props =>
        (HediffCompProperties_DysphorakhEpisodes)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = parent.pawn;
        if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
            return;

        if (cooldownTicks > 0)
        {
            cooldownTicks--;
            return;
        }

        // Don't fire during another mental state
        if (pawn.MentalStateDef != null)
            return;

        // Random episode roll (suffocation / phantom hunger / sensory disconnect)
        if (Rand.MTBEventOccurs(Props.episodeMtbDays, GenDate.TicksPerDay, 1))
        {
            TriggerRandomEpisode(pawn);
            SetCooldown();
            return;
        }

        // Clawing roll (separate, rarer — only rolls if no random episode fired this tick)
        if (Rand.MTBEventOccurs(Props.clawingMtbDays, GenDate.TicksPerDay, 1))
        {
            TriggerClawing(pawn);
            SetCooldown();
        }
    }

    private void SetCooldown() =>
        cooldownTicks = (int)(Props.episodeCooldownDays * GenDate.TicksPerDay);

    /// <summary>Set a one-time initial cooldown (e.g. when the trait is first acquired).</summary>
    public void SetInitialCooldown(int ticks)
    {
        if (ticks > cooldownTicks)
            cooldownTicks = ticks;
    }

    // ── Episode selection ─────────────────────────────────────────────────────

    private void TriggerRandomEpisode(Pawn pawn)
    {
        // Weights: Suffocation 3, Phantom Hunger 3, Sensory Disconnect 2  (total 8)
        float roll = Rand.Value * 8f;

        if (roll < 3f)
            TriggerSuffocation(pawn);
        else if (roll < 6f)
            TriggerPhantomHunger(pawn);
        else
            TriggerSensoryDisconnect(pawn);
    }

    // ── Suffocation ───────────────────────────────────────────────────────────

    private void TriggerSuffocation(Pawn pawn)
    {
        MentalStateDef stateDef = NecronDefOfs.GW40K_Dysphorakh_Suffocation;
        if (stateDef == null) return;

        pawn.health.AddHediff(NecronDefOfs.GW40K_Dysphorakh_SuffocationShock);
        pawn.mindState.mentalStateHandler.TryStartMentalState(stateDef, forced: true, forceWake: true, causedByMood: false);

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Messages.Message("GW40K_Dysphorakh_Suffocation".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeEvent);
    }

    // ── Phantom Hunger ────────────────────────────────────────────────────────

    private void TriggerPhantomHunger(Pawn pawn)
    {
        MentalStateDef stateDef = NecronDefOfs.GW40K_Dysphorakh_PhantomHunger;
        if (stateDef == null) return;

        ConsumeNearbyFood(pawn);
        pawn.mindState.mentalStateHandler.TryStartMentalState(stateDef, forced: true, forceWake: true, causedByMood: false);

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Messages.Message("GW40K_Dysphorakh_PhantomHunger".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeEvent);
    }

    /// <summary>
    /// Finds the nearest accessible food item and consumes part of it without providing nutrition.
    /// If nothing is within range the episode still fires — the pawn just wanders.
    /// </summary>
    private static void ConsumeNearbyFood(Pawn pawn)
    {
        if (pawn.Map == null) return;

        Thing food = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            maxDistance: 20f,
            validator: t => t.def.ingestible != null && !t.IsForbidden(pawn));

        if (food == null) return;

        int consume = System.Math.Max(1, food.stackCount / 3);
        food.stackCount -= consume;
        if (food.stackCount <= 0)
            food.Destroy();
    }

    // ── Sensory Disconnect (catatonic) ────────────────────────────────────────

    private static void TriggerSensoryDisconnect(Pawn pawn)
    {
        MentalStateDef cataDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("CatatonicBreakdown");
        if (cataDef == null) return;

        bool started = pawn.mindState.mentalStateHandler.TryStartMentalState(cataDef, forced: true, forceWake: true, causedByMood: false);
        if (!started) return;

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Messages.Message("GW40K_Dysphorakh_SensoryDisconnect".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeEvent);
    }

    // ── Compulsive Clawing ────────────────────────────────────────────────────

    private static void TriggerClawing(Pawn pawn)
    {
        MentalStateDef clawDef = NecronDefOfs.GW40K_Dysphorakh_Clawing;
        if (clawDef == null) return;

        bool started = pawn.mindState.mentalStateHandler.TryStartMentalState(clawDef, forced: true, forceWake: true, causedByMood: false);
        if (!started) return;

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Messages.Message("GW40K_Dysphorakh_Clawing".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.ThreatSmall);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref cooldownTicks, "dysphorakhCooldownTicks", 0);
    }
}
