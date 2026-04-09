using RimWorld;
using Verse;

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
    private int uncontrolledSinceTick = -1;

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
        SyncCommandState();
    }

    public override void CompTickRare()
    {
        if (parent is Pawn { Destroyed: false })
            SyncCommandState();
    }

    /// <summary>Clear timer when commanded; start clock once when uncontrolled (spawn / first tick).</summary>
    public void SyncCommandState()
    {
        Pawn p = parent as Pawn;
        if (p == null)
            return;
        if (NechInspectStringUtility.IsNechProperlyCommanded(p))
            uncontrolledSinceTick = -1;
        else if (uncontrolledSinceTick < 0)
            uncontrolledSinceTick = Find.TickManager.TicksGame;
    }

    /// <summary>Call when overseer link is removed — restart uncontrolled duration from now.</summary>
    public void NotifyCommandLinkLost()
    {
        Pawn p = parent as Pawn;
        if (p == null)
            return;
        if (!NechInspectStringUtility.IsNechProperlyCommanded(p))
            uncontrolledSinceTick = Find.TickManager.TicksGame;
    }

    public void NotifyCommandLinkGained()
    {
        uncontrolledSinceTick = -1;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref uncontrolledSinceTick, "uncontrolledSinceTick", -1);
    }
}
