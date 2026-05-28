using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Standalone ability-style gizmo: order all Control Node–linked scarabs to attack a target or move to a cell.
/// Shown beside the Command Protocol / Control Node bandwidth UI when at least one scarab is linked.
/// </summary>
public class Gizmo_ControlNodeSwarm : Command_Action
{
    private readonly HediffComp_ControlNodeTracker tracker;
    private const string SpyderUncontrolledReason = "Spyder must be under command to issue control-node orders.";

    public Gizmo_ControlNodeSwarm(HediffComp_ControlNodeTracker tracker)
    {
        this.tracker = tracker;
        defaultLabel = "Swarm";
        defaultDesc =
            "Order all linked Canoptek constructs to attack a hostile pawn or building, or move to a ground cell.";
        icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false)
            ?? ContentFinder<Texture2D>.Get("UI/Commands/DesirePower");
        action = BeginSwarmTargeting;
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        bool noScarabs = tracker == null || tracker.controlledScarabs.Count == 0;
        Pawn commander = tracker?.CommanderPawn;
        bool spyderUncontrolled = commander != null
            && ControlNodeUtility.IsSpyder(commander)
            && HediffComp_NecronCommandTracker.GetCommanderOf(commander) == null;
        disabled = noScarabs || spyderUncontrolled;
        disabledReason = noScarabs
            ? "Requires at least one linked scarab."
            : spyderUncontrolled ? SpyderUncontrolledReason : string.Empty;
        return base.GizmoOnGUI(topLeft, maxWidth, parms);
    }

    private void BeginSwarmTargeting()
    {
        if (tracker == null || tracker.controlledScarabs.Count == 0)
            return;

        Pawn commander = tracker.CommanderPawn;
        if (commander == null || !commander.Spawned || commander.Map == null)
            return;

        TargetingParameters parms = new TargetingParameters
        {
            canTargetLocations = true,
            canTargetPawns = true,
            canTargetBuildings = true,
            canTargetItems = false,
            mapObjectTargetsMustBeAutoAttackable = false,
            validator = t =>
            {
                if (!t.IsValid)
                    return false;
                if (t.HasThing)
                {
                    Thing thing = t.Thing;
                    if (thing == null || thing.Destroyed || thing.Map != commander.Map)
                        return false;
                    if (thing is Pawn targetPawn)
                        return targetPawn.Faction == null || targetPawn.HostileTo(commander);
                    if (thing is Building targetBuilding)
                        return targetBuilding.Faction == null || targetBuilding.HostileTo(commander);
                    return false;
                }

                return t.Cell.IsValid && t.Cell.InBounds(commander.Map);
            }
        };

        Find.Targeter.BeginTargeting(parms, delegate(LocalTargetInfo target) { IssueSwarmOrders(tracker, target); });
    }

    internal static void IssueSwarmOrders(HediffComp_ControlNodeTracker tracker, LocalTargetInfo target)
    {
        Pawn commander = tracker?.CommanderPawn;
        if (commander == null || !commander.Spawned || commander.Map == null)
            return;

        for (int i = 0; i < tracker.controlledScarabs.Count; i++)
        {
            Pawn scarab = tracker.controlledScarabs[i];
            if (scarab == null || scarab.Dead || scarab.Destroyed || !scarab.Spawned || scarab.Map != commander.Map)
                continue;

            Job job;
            if (target.HasThing && target.Thing is Thing targetThing)
            {
                bool ranged = scarab.equipment?.Primary != null && !scarab.equipment.Primary.def.IsMeleeWeapon;
                if (ranged)
                {
                    // Same shape as a drafted attack order: no shot cap — keep shooting until target is gone.
                    job = JobMaker.MakeJob(JobDefOf.AttackStatic, targetThing);
                }
                else
                {
                    job = JobMaker.MakeJob(JobDefOf.AttackMelee, targetThing);
                }
            }
            else
            {
                job = JobMaker.MakeJob(JobDefOf.Goto, target.Cell);
            }

            job.playerForced = true;
            // Force immediate execution instead of queueing behind current jobs.
            scarab.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
        }

        // Switch the control node itself and all linked scarabs to Combat mode so they
        // auto-engage without needing a manual order for each target.
        // tracker.SetMode propagates to linked constructs via GameComponent_CanoptekConstructModes.
        tracker.SetMode(ControlNodeMode.Combat);

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }
}
