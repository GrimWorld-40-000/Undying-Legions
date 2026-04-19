using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class HediffCompProperties_NecronCommand : HediffCompProperties
{
    public float bandwidthMax = 6f;
    public float bandwidthCostPerMech = 1f;
    public float controlRange = 40f;

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
    public float ControlRange => Props.controlRange > 0f ? Props.controlRange : 40f;
    public Pawn CommanderPawn => parent?.pawn;

    /// <summary>Sum of <see cref="CommandBandwidthCostOf"/> for bound constructs — Necron extension / hediff props only, not vanilla BandwidthCost stat.</summary>
    public float BandwidthUsed
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < controlledMechs.Count; i++)
            {
                Pawn m = controlledMechs[i];
                if (m != null && !m.Dead && !m.Destroyed)
                    sum += CommandBandwidthCostOf(m);
            }
            return sum;
        }
    }

    /// <summary>Bandwidth for a race ThingDef (spawn recipe / binding).</summary>
    public float CommandBandwidthCostForRace(ThingDef raceDef)
    {
        if (raceDef == null)
            return Props.bandwidthCostPerMech;
        NecronMechExtension ext = raceDef.GetModExtension<NecronMechExtension>();
        if (ext != null && ext.commandBandwidthCost > 0f)
            return ext.commandBandwidthCost;
        return Props.bandwidthCostPerMech;
    }

    /// <summary>Bandwidth for the construct defined by a Monolith <see cref="RecipeExtension_SpawnMech"/> recipe.</summary>
    public float CommandBandwidthCostForPawnKind(PawnKindDef kind) =>
        CommandBandwidthCostForRace(kind?.race);

    /// <summary>Per-mech cost from <see cref="NecronMechExtension.commandBandwidthCost"/>; fallback to hediff <see cref="HediffCompProperties_NecronCommand.bandwidthCostPerMech"/>.</summary>
    public float CommandBandwidthCostOf(Pawn mech) =>
        mech == null ? 0f : CommandBandwidthCostForRace(mech.def);

    public bool HasBandwidthFor(float cost = -1f)
    {
        float needed = cost < 0f ? Props.bandwidthCostPerMech : cost;
        return BandwidthUsed + needed <= BandwidthMax;
    }

    /// <summary>Room to bind <paramref name="mechToBind"/> (uses that pawn's Necron command bandwidth cost).</summary>
    public bool HasBandwidthFor(Pawn mechToBind)
    {
        if (mechToBind == null)
            return HasBandwidthFor();
        if (controlledMechs.Contains(mechToBind))
            return true;
        return BandwidthUsed + CommandBandwidthCostOf(mechToBind) <= BandwidthMax;
    }

    public bool IsWithinControlRange(Pawn mech)
    {
        Pawn commander = CommanderPawn;
        if (commander == null || mech == null)
            return false;
        if (!commander.Spawned || !mech.Spawned || commander.Map != mech.Map)
            return false;
        return commander.Position.DistanceTo(mech.Position) <= ControlRange;
    }

    public void BindMech(Pawn mech)
    {
        if (mech == null || controlledMechs.Contains(mech)) return;
        controlledMechs.Add(mech);
        mech.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkGained();
    }

    public void UnbindMech(Pawn mech)
    {
        if (mech == null || !controlledMechs.Remove(mech)) return;
        if (!mech.Destroyed && mech.drafter != null && mech.Drafted)
        {
            mech.drafter.Drafted = false;
            mech.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
        }
        mech.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkLost();
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        int tick = Find.TickManager.TicksGame;
        if (tick % 120 == 0)
            controlledMechs.RemoveAll(m => m == null || m.Dead || m.Destroyed);

        if (tick % 30 == 0)
            TickCommanderFacing();
    }

    /// <summary>Draft nechinator faces the controlled construct (selected nech if possible, else nearest in range).</summary>
    private void TickCommanderFacing()
    {
        Pawn comm = CommanderPawn;
        if (comm == null || !comm.Spawned || comm.Dead || comm.Downed)
            return;
        if (!comm.Drafted || controlledMechs.Count == 0)
            return;

        Pawn faceTarget = null;
        if (Find.Selector != null)
        {
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                if (obj is Pawn p && controlledMechs.Contains(p) && p.Spawned && !p.Dead && p.MapHeld == comm.MapHeld)
                {
                    faceTarget = p;
                    break;
                }
            }
        }

        if (faceTarget == null)
        {
            float best = float.MaxValue;
            foreach (Pawn m in controlledMechs)
            {
                if (m == null || m.Dead || !m.Spawned || m.MapHeld != comm.MapHeld)
                    continue;
                if (!IsWithinControlRange(m))
                    continue;
                float d = comm.Position.DistanceToSquared(m.Position);
                if (d < best)
                {
                    best = d;
                    faceTarget = m;
                }
            }
        }

        if (faceTarget == null)
            return;

        comm.rotationTracker?.FaceCell(faceTarget.Position);
    }

    public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
    {
        for (int i = controlledMechs.Count - 1; i >= 0; i--)
        {
            Pawn nech = controlledMechs[i];
            if (nech != null && !nech.Dead && !nech.Destroyed)
                nech.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkLost();
        }
        controlledMechs.Clear();
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

    /// <summary>
    /// Finds the Nechinator currently commanding <paramref name="nech"/>.
    /// Replaces <c>pawn.GetOverseer()</c> for Nechs — no vanilla relation required.
    /// </summary>
    public static Pawn GetCommanderOf(Pawn nech)
    {
        if (nech == null) return null;
        foreach (Pawn candidate in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
        {
            if (candidate.def.GetModExtension<NecronMechExtension>() != null) continue;
            HediffComp_NecronCommandTracker t = GetTracker(candidate);
            if (t != null && t.controlledMechs.Contains(nech))
                return candidate;
        }
        return null;
    }
}
