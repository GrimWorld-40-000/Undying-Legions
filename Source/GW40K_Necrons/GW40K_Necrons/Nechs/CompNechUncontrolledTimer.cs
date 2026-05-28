using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class CompProperties_NechUncontrolledTimer : CompProperties
{
    public CompProperties_NechUncontrolledTimer()
    {
        compClass = typeof(CompNechUncontrolledTimer);
    }
}

/// <summary>Tracks how long a Nech has lacked a valid nechinator command link (for inspect UI).</summary>
public class CompNechUncontrolledTimer : ThingComp
{
    public const int HostileLockoutImminentSeconds = 800;

    private const int WarningThresholdSeconds = 90;
    private const int RogueThresholdSeconds = 120;
    private const int HostileThresholdSeconds = 999;

    private int uncontrolledSinceTick = -1;
    private bool warningSent;
    private bool rogueSent;
    private bool hostileSent;
    private bool wasDownedLastTick;

    public int UncontrolledSecondsAtTick(int ticksGame)
    {
        if (uncontrolledSinceTick < 0)
            return 0;
        int d = ticksGame - uncontrolledSinceTick;
        if (d < 0)
            return 0;
        return d / GenTicks.TicksPerRealSecond;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (parent is Pawn pawn)
            wasDownedLastTick = pawn.Downed;
        SyncCommandState();
    }

    public override void CompTickRare()
    {
        if (parent is not Pawn { Destroyed: false } pawn) return;
        if (!NechUtility.IsNechControlled(pawn))
            return;
        if (pawn.Faction != Faction.OfPlayer)
        {
            NotifyCommandLinkGained();
            return;
        }
        SyncCommandState();
        // Stand-up after downed while still uncontrolled: timer restarts — clear one-shot escalation
        // latches or rogueSent / hostileSent suppress 120s / 999s forever despite the fresh clock.
        if (wasDownedLastTick && !pawn.Downed && uncontrolledSinceTick >= 0)
        {
            uncontrolledSinceTick = Find.TickManager.TicksGame;
            ClearEscalationLatches();
        }

        wasDownedLastTick = pawn.Downed;

        if (uncontrolledSinceTick < 0 || pawn.Dead || !pawn.Spawned || pawn.Downed) return;

        int seconds = UncontrolledSecondsAtTick(Find.TickManager.TicksGame);

        if (!warningSent && seconds >= WarningThresholdSeconds)
            SendWarning(pawn);

        if (!rogueSent && seconds >= RogueThresholdSeconds)
            TriggerRogue(pawn);

        if (!hostileSent && seconds >= HostileThresholdSeconds)
            ForceHostile(pawn);
    }

    private void ClearEscalationLatches()
    {
        warningSent = false;
        rogueSent = false;
        hostileSent = false;
    }

    private void SendWarning(Pawn pawn)
    {
        warningSent = true;
        if (!PawnUtility.ShouldSendNotificationAbout(pawn)) return;

        Messages.Message(
            "GW40K_NechGoingRogueWarningDesc".Translate(pawn.Named("PAWN")),
            pawn,
            MessageTypeDefOf.CautionInput);
    }

    private void TriggerRogue(Pawn pawn)
    {
        rogueSent = true;
        ForceRogue(pawn);
        if (!PawnUtility.ShouldSendNotificationAbout(pawn)) return;
        Messages.Message(
            "GW40K_NechWentRogueMessage".Translate(pawn.Named("PAWN")),
            pawn,
            MessageTypeDefOf.NegativeEvent);
    }

    public void ForceRogue()
    {
        if (parent is Pawn pawn)
            ForceRogue(pawn);
    }

    public void ForceHostile()
    {
        if (parent is Pawn pawn)
            ForceHostile(pawn);
    }

    public void ForceRogue(Pawn pawn)
    {
        if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Downed)
            return;
        if (pawn.Faction != Faction.OfPlayer)
            return;
        var rogueDef = NecronDefOfs.GW40K_NechRogue ?? MentalStateDefOf.Berserk;
        pawn.mindState?.mentalStateHandler?.TryStartMentalState(
            rogueDef, null, forced: true, causedByMood: false);
        NechTakeControlJobUtility.CancelTakeControlJobsTargeting(pawn);
    }

    public void ForceHostile(Pawn pawn)
    {
        hostileSent = true;
        if (pawn == null || pawn.Dead || pawn.Destroyed)
            return;
        if (pawn.Faction != Faction.OfPlayer)
            return;

        // Must snapshot before SetFaction: once hostile, the pawn is no longer colony-owned and
        // ShouldSendNotificationAbout becomes false, which suppressed the threat letter.
        bool sendHostileLetter = PawnUtility.ShouldSendNotificationAbout(pawn);

        Faction hostileNecronFaction = Find.FactionManager?.FirstFactionOfDef(
            DefDatabase<FactionDef>.GetNamedSilentFail("UD_NecronFaction"));
        if (hostileNecronFaction != null && pawn.Faction != hostileNecronFaction)
        {
            pawn.SetFaction(hostileNecronFaction);
            NechTakeControlJobUtility.CancelTakeControlJobsTargeting(pawn);
        }
        else
            ForceRogue(pawn);

        if (!sendHostileLetter)
            return;

        Find.LetterStack.ReceiveLetter(
            "GW40K_NechWentHostileTitle".Translate(pawn.Named("PAWN")),
            "GW40K_NechWentHostileDesc".Translate(pawn.Named("PAWN")),
            LetterDefOf.ThreatBig,
            pawn);
    }

    /// <summary>Clear timer when commanded; start clock once when uncontrolled (spawn / first tick).
    /// Also auto-undrafts the Nech if it is drafted without a commander, so it doesn't get stuck in
    /// a drafted-but-unorderable limbo (e.g. after the commander dies).</summary>
    public void SyncCommandState()
    {
        Pawn p = parent as Pawn;
        if (p == null)
            return;
        if (!NechUtility.IsNechControlled(p) || p.Faction != Faction.OfPlayer)
        {
            uncontrolledSinceTick = -1;
            warningSent = false;
            rogueSent = false;
            hostileSent = false;
            return;
        }

        if (NechInspectStringUtility.IsNechProperlyCommanded(p))
        {
            uncontrolledSinceTick = -1;
            warningSent = false;
            rogueSent = false;
            hostileSent = false;
        }
        else
        {
            if (uncontrolledSinceTick < 0)
                uncontrolledSinceTick = Find.TickManager.TicksGame;

            // Nech lost its commander while drafted — undraft so it can fight autonomously.
            if (p.Drafted && p.drafter != null)
            {
                p.drafter.Drafted = false;
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
            }
        }
    }

    /// <summary>Call when overseer link is removed — restart uncontrolled duration from now.</summary>
    public void NotifyCommandLinkLost()
    {
        Pawn p = parent as Pawn;
        if (p == null)
            return;
        if (!NechUtility.IsNechControlled(p))
            return;
        if (!NechInspectStringUtility.IsNechProperlyCommanded(p))
        {
            uncontrolledSinceTick = Find.TickManager.TicksGame;
            ClearEscalationLatches();
        }
    }

    public void NotifyCommandLinkGained()
    {
        if (parent is Pawn p && !NechUtility.IsNechControlled(p))
            return;
        uncontrolledSinceTick = -1;
        warningSent = false;
        rogueSent = false;
        hostileSent = false;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref uncontrolledSinceTick, "uncontrolledSinceTick", -1);
        Scribe_Values.Look(ref warningSent, "warningSent", false);
        Scribe_Values.Look(ref rogueSent, "rogueSent", false);
        Scribe_Values.Look(ref hostileSent, "hostileSent", false);
        Scribe_Values.Look(ref wasDownedLastTick, "wasDownedLastTick", false);
    }
}
