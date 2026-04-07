using System.Collections.Generic;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffCompProperties_NecronCommand : HediffCompProperties
{
    public float bandwidthMax = 6f;
    public float bandwidthCostPerMech = 1f;

    public HediffCompProperties_NecronCommand()
    {
        compClass = typeof(HediffComp_NecronCommandTracker);
    }
}

/// <summary>
/// Standalone Necron command tracker. Mirrors the vanilla Pawn_MechanitorTracker concept
/// but is fully independent — no vanilla mechanitor system involved.
/// Attached as a HediffComp on GW40K_CommandProtocolImplant.
/// </summary>
public class HediffComp_NecronCommandTracker : HediffComp
{
    public List<Pawn> controlledMechs = new List<Pawn>();

    public HediffCompProperties_NecronCommand Props => (HediffCompProperties_NecronCommand)props;

    public float BandwidthMax => Props.bandwidthMax;
    public float BandwidthUsed => controlledMechs.Count * Props.bandwidthCostPerMech;
    public bool HasBandwidthFor(float cost = -1f)
    {
        float needed = cost < 0f ? Props.bandwidthCostPerMech : cost;
        return BandwidthUsed + needed <= BandwidthMax;
    }

    public void BindMech(Pawn mech)
    {
        if (mech == null || controlledMechs.Contains(mech)) return;
        controlledMechs.Add(mech);
    }

    public void UnbindMech(Pawn mech)
    {
        controlledMechs.Remove(mech);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        if (Find.TickManager.TicksGame % 120 == 0)
            controlledMechs.RemoveAll(m => m == null || m.Dead || m.Destroyed);
    }

    public override void CompExposeData()
    {
        Scribe_Collections.Look(ref controlledMechs, "controlledMechs", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            controlledMechs ??= new List<Pawn>();
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        yield return new Gizmo_NecronBandwidth(this);
    }

    /// <summary>Convenience accessor — fetches the tracker from any pawn with the Command Protocol.</summary>
    public static HediffComp_NecronCommandTracker GetTracker(Pawn pawn)
    {
        return pawn?.health?.hediffSet?
            .GetFirstHediffOfDef(HediffDef.Named("GW40K_CommandProtocolImplant"))
            ?.TryGetComp<HediffComp_NecronCommandTracker>();
    }
}
