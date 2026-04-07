using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

public class NecronCasket : Building_CryptosleepCasket
{
    /// <summary>
    /// Length of the restoration cycle (game ticks). Uses a fraction of <see cref="GenDate.TicksPerDay"/> so
    /// stasis scales with RimWorld time instead of ~600 ticks (~10s real at 1×).
    /// </summary>
    private static readonly int StasisCycleTicks = GenDate.TicksPerDay / 4;

    private int ticksToFinish = -1;

    public override bool Accepts(Thing thing)
    {
        if (thing is not Pawn pawn) return false;
        return pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_CoreFlux) != null;
    }

    public override void Open()
    {
        if (!this.HasAnyContents) return;
        this.EjectContents();
        if (this.openedSignal.NullOrEmpty()) return;
        Find.SignalManager.SendSignal(new Signal(this.openedSignal, this.Named("SUBJECT")));
    }

    public override void EjectContents()
    {
        foreach (Thing thing in (IEnumerable<Thing>)this.innerContainer)
        {
            if (thing is Pawn pawn)
                PawnComponentsUtility.AddComponentsForSpawn(pawn);
        }
        if (!this.Destroyed)
            SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(this.Position, this.Map)));
        this.innerContainer.TryDropAll(this.InteractionCell, this.Map, ThingPlaceMode.Near);
        this.contentsKnown = true;
    }

    public override bool CanOpen => this.ticksToFinish <= 0 && this.ContainedThing != null;

    public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
    {
        this.ticksToFinish = StasisCycleTicks;
        return base.TryAcceptThing(thing, true);
    }

    protected override void Tick()
    {
        base.Tick();
        if (this.ContainedThing == null) return;
        if (this.ticksToFinish > 0)
        {
            --this.ticksToFinish;
            return;
        }
        this.ticksToFinish = -1;
        Pawn pawn = this.innerContainer.First<Thing>() as Pawn;
        if (pawn != null)
        {
            pawn.health.RemoveAllHediffs();
            pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux)?.SetInitialLevel();
        }
        this.Open();
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
    {
        if (!this.Accepts((Thing)myPawn))
        {
            yield return new FloatMenuOption("Pawn is not Necron", (Action)null);
            yield break;
        }
        if (myPawn.IsQuestLodger())
        {
            yield return new FloatMenuOption((string)"CannotUseReason".Translate((NamedArgument)"CryptosleepCasketGuestsNotAllowed".Translate()), (Action)null);
            yield break;
        }
        foreach (FloatMenuOption item in CompFloatMenuOptionsSafe(myPawn))
            yield return item;
        if (this.innerContainer.Count != 0)
            yield break;
        if (!myPawn.CanReach((LocalTargetInfo)(Thing)this, PathEndMode.InteractionCell, Danger.Deadly))
        {
            yield return new FloatMenuOption((string)"CannotUseNoPath".Translate(), (Action)null);
            yield break;
        }
        JobDef jobDef = JobDefOf.EnterCryptosleepCasket;
        string label = "GW40K_EnterStasisCrypt".Translate();
        Action action = delegate
        {
            if (ModsConfig.BiotechActive)
            {
                if (!(myPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicBond) is Hediff_PsychicBond bond)
                    || !ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson(myPawn, bond))
                {
                    myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
                }
                else
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "PsychicBondDistanceWillBeActive_Cryptosleep".Translate(
                            myPawn.Named("PAWN"),
                            ((Pawn)bond.target).Named("BOND")),
                        delegate { myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc); },
                        destructive: true));
                }
            }
            else
            {
                myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
            }
        };
        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), myPawn, this);
    }

    /// <summary>Mirrors <see cref="ThingWithComps.GetFloatMenuOptions"/> comp pass without calling <see cref="Building_CryptosleepCasket.GetFloatMenuOptions"/> (avoids vanilla enter label).</summary>
    private IEnumerable<FloatMenuOption> CompFloatMenuOptionsSafe(Pawn selPawn)
    {
        List<ThingComp> list = this.AllComps;
        if (list == null)
            yield break;
        List<FloatMenuOption> buffer = new List<FloatMenuOption>();
        for (int i = 0; i < list.Count; i++)
        {
            ThingComp comp = list[i];
            try
            {
                foreach (FloatMenuOption item in comp.CompFloatMenuOptions(selPawn))
                    buffer.Add(item);
            }
            catch (Exception ex)
            {
                Log.Error("Exception in CompFloatMenuOptions for " + comp?.GetType()?.ToString() + " of " + this + " at " + selPawn + ": " + ex);
            }
        }
        foreach (FloatMenuOption item in buffer)
            yield return item;
    }

    public override string GetInspectStringLowPriority()
    {
        if (this.ticksToFinish <= 0)
            return string.Empty;
        return "GW40K_StasisCryptTimeRemaining"
            .Translate(this.ticksToFinish.ToStringTicksToPeriod().Named("REMAINING"))
            .Resolve();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        string ejectLabel = "CommandPodEject".Translate();
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            if (gizmo is Command_Action cmd && cmd.defaultLabel == ejectLabel)
            {
                Action original = cmd.action;
                cmd.action = delegate
                {
                    if (this.ticksToFinish > 0 && this.Faction == Faction.OfPlayer)
                    {
                        Pawn pawn = this.ContainedThing as Pawn;
                        TaggedString text = pawn != null
                            ? "GW40K_StasisCryptInterruptConfirm".Translate(pawn.Named("PAWN"))
                            : "GW40K_StasisCryptInterruptConfirmNoPawn".Translate();
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, original, destructive: true));
                    }
                    else
                    {
                        original();
                    }
                };
            }
            yield return gizmo;
        }
    }
}
