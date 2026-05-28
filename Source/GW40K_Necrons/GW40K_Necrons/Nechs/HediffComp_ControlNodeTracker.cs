using System;
using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public enum ControlNodeMode : byte
{
    Consume = 0,
    Repair  = 1,
    Work    = 2,
    Produce = 3,
    Combat  = 4,  // Scarab: actively attack, use Jump/Explode; block consume/repair/work
    Defend  = 5,  // Scarab: patrol near controller; block consume/repair/work
    // BreakDown = 8  // reserved, not yet implemented
}

public class HediffCompProperties_ControlNode : HediffCompProperties
{
    public int standardBandwidthMax = 3;
    public int cryptekBandwidthMax = 4;
    public int spyderBandwidthMax = 6;
    public float bandwidthCostPerScarab = 1f;
    public float controlRange = 20f;

    public HediffCompProperties_ControlNode()
    {
        compClass = typeof(HediffComp_ControlNodeTracker);
    }
}

/// <summary>
/// Dedicated Control Node tracker for Canoptek constructs (scarabs, Spyders, etc.).
/// Separate from Command Protocol systems and state.
/// </summary>
public class HediffComp_ControlNodeTracker : HediffComp
{
    private static readonly HediffDef ControlNodeImplantDef =
        DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_ControlNodeImplant");

    public List<Pawn> controlledScarabs = new List<Pawn>();
    public ControlNodeMode mode = ControlNodeMode.Consume;

    public HediffCompProperties_ControlNode Props => (HediffCompProperties_ControlNode)props;
    public Pawn CommanderPawn => parent?.pawn;
    public float ControlRange => Props.controlRange > 0f ? Props.controlRange : 20f;
    public float BandwidthCostPerScarab => Props.bandwidthCostPerScarab > 0f ? Props.bandwidthCostPerScarab : 1f;

    public int BandwidthMax
    {
        get
        {
            Pawn commander = CommanderPawn;
            if (commander == null)
                return Props.standardBandwidthMax;

            if (ControlNodeUtility.IsSpyder(commander))
                return Props.spyderBandwidthMax;
            if (ControlNodeUtility.IsCryptek(commander))
                return Props.cryptekBandwidthMax;
            return Props.standardBandwidthMax;
        }
    }

    public float BandwidthUsed
    {
        get
        {
            float used = 0f;
            for (int i = controlledScarabs.Count - 1; i >= 0; i--)
            {
                Pawn scarab = controlledScarabs[i];
                if (scarab == null || scarab.Dead || scarab.Destroyed)
                    continue;
                used += BandwidthCostPerScarab;
            }

            return used;
        }
    }

    public bool HasBandwidthFor(Pawn scarabToBind)
    {
        if (scarabToBind == null || !ControlNodeUtility.IsCanoptek(scarabToBind))
            return false;
        if (controlledScarabs.Contains(scarabToBind))
            return true;
        return BandwidthUsed + BandwidthCostPerScarab <= BandwidthMax;
    }

    public bool IsWithinControlRange(Pawn scarab)
    {
        Pawn commander = CommanderPawn;
        if (commander == null || scarab == null)
            return false;
        if (!commander.Spawned || !scarab.Spawned || commander.Map != scarab.Map)
            return false;
        return commander.Position.DistanceTo(scarab.Position) <= ControlRange;
    }

    public bool BindScarab(Pawn scarab)
    {
        if (scarab == null || !ControlNodeUtility.IsCanoptek(scarab))
            return false;
        if (controlledScarabs.Contains(scarab))
            return true;
        if (!HasBandwidthFor(scarab))
            return false;
        controlledScarabs.Add(scarab);

        // If the controller is a Spyder, push its repair-policy template to the newly bound scarab.
        if (ControlNodeUtility.IsSpyder(CommanderPawn))
        {
            ThingComp_CanoptekRepairPolicy spyderPolicy =
                CommanderPawn.TryGetComp<ThingComp_CanoptekRepairPolicy>();
            ThingComp_CanoptekRepairPolicy scarabPolicy =
                scarab.TryGetComp<ThingComp_CanoptekRepairPolicy>();
            if (spyderPolicy != null && scarabPolicy != null)
            {
                scarabPolicy.allowSelf             = spyderPolicy.allowSelf;
                scarabPolicy.allowFriendlyNecrons  = spyderPolicy.allowFriendlyNecrons;
                scarabPolicy.allowFriendlyMechs    = spyderPolicy.allowFriendlyMechs;
                scarabPolicy.allowNecronStructures = spyderPolicy.allowNecronStructures;
                scarabPolicy.allowStructures       = spyderPolicy.allowStructures;
            }
        }

        // On link creation, initialize construct mode from the controller's current mode.
        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        modes?.SetMode(scarab, mode);
        // Controlled scarabs should enter with auto mode enabled.
        modes?.SetAutoMode(scarab, true);
        // Auto-assign to Scarab Group A if not already in any scarab group.
        NecronCommandGroupManager grpMgr = NecronCommandGroupManager.Instance;
        if (grpMgr != null && grpMgr.GetScarabGroupOf(scarab) < 0)
            grpMgr.AssignToScarabGroup(scarab, 0);

        // On control-link gain, secondary color tracks commander's preferred color.
        scarab.TryGetComp<CompScarabPaint>()?.QueueCommanderFavoriteTint(CommanderPawn);
        return true;
    }

    public void UnbindScarab(Pawn scarab)
    {
        if (scarab == null)
            return;
        controlledScarabs.Remove(scarab);
    }

    public void SetMode(ControlNodeMode newMode)
    {
        mode = newMode;

        // "Override" behavior is a mode push, not a lock:
        // apply the selected mode to currently linked constructs, which can still be changed individually later.
        GameComponent_CanoptekConstructModes store = GameComponent_CanoptekConstructModes.Current;
        if (store == null)
            return;
        for (int i = 0; i < controlledScarabs.Count; i++)
        {
            Pawn construct = controlledScarabs[i];
            if (construct == null || construct.Dead || construct.Destroyed || !ControlNodeUtility.IsCanoptek(construct))
                continue;
            store.SetMode(construct, newMode);
        }
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        if (Find.TickManager.TicksGame % 120 == 0)
            controlledScarabs.RemoveAll(s => s == null || s.Dead || s.Destroyed || !ControlNodeUtility.IsCanoptek(s));
    }

    public override void CompExposeData()
    {
        Scribe_Collections.Look(ref controlledScarabs, "controlledScarabs", LookMode.Reference);
        Scribe_Values.Look(ref mode, "mode", ControlNodeMode.Consume);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            controlledScarabs ??= new List<Pawn>();
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        if (CommanderPawn == null || CommanderPawn.Faction != Faction.OfPlayer)
            yield break;

        if (controlledScarabs.Count > 0)
            yield return new Gizmo_ControlNodeSwarm(this);
        yield return new Gizmo_ControlNodeBandwidth(this);
    }

    public static HediffComp_ControlNodeTracker GetTracker(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null || ControlNodeImplantDef == null)
            return null;

        return pawn.health.hediffSet.GetFirstHediffOfDef(ControlNodeImplantDef)?.TryGetComp<HediffComp_ControlNodeTracker>();
    }

    public static Pawn GetControllerOfScarab(Pawn scarab) => GetControllerOfConstruct(scarab);

    public static Pawn GetControllerOfConstruct(Pawn construct)
    {
        if (!ControlNodeUtility.IsCanoptek(construct))
            return null;

        foreach (Pawn candidate in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
        {
            HediffComp_ControlNodeTracker tracker = GetTracker(candidate);
            if (tracker != null && tracker.controlledScarabs.Contains(construct))
                return candidate;
        }

        return null;
    }
}

internal static class ControlNodeUtility
{
    internal static bool IsCanoptek(Pawn pawn) =>
        NecrodermisIngestionUtility.IsCanoptek(pawn);

    internal static bool IsCryptek(Pawn pawn) =>
        pawn?.kindDef?.defName == "UD_NecronCryptek" ||
        pawn?.def?.defName?.IndexOf("Cryptek", StringComparison.OrdinalIgnoreCase) >= 0;

    internal static bool IsSpyder(Pawn pawn)
    {
        string defName = pawn?.def?.defName;
        string kindDefName = pawn?.kindDef?.defName;
        return (defName?.IndexOf("Spyder", StringComparison.OrdinalIgnoreCase) >= 0)
            || (defName?.IndexOf("Spider", StringComparison.OrdinalIgnoreCase) >= 0)
            || (kindDefName?.IndexOf("Spyder", StringComparison.OrdinalIgnoreCase) >= 0)
            || (kindDefName?.IndexOf("Spider", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
