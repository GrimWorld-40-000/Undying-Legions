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
    private int ticksToFinish = -1;
    private const int timeToHeal = 600;

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
        this.ticksToFinish = 600;
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
            yield return new FloatMenuOption("Pawn is not Necron", (Action)null);
        else if (myPawn.IsQuestLodger())
            yield return new FloatMenuOption((string)"CannotUseReason".Translate((NamedArgument)"CryptosleepCasketGuestsNotAllowed".Translate()), (Action)null);
        else if (this.innerContainer.Count == 0)
        {
            if (!myPawn.CanReach((LocalTargetInfo)(Thing)this, PathEndMode.InteractionCell, Danger.Deadly))
                yield return new FloatMenuOption((string)"CannotUseNoPath".Translate(), (Action)null);
            else
                foreach (FloatMenuOption item in base.GetFloatMenuOptions(myPawn))
                    yield return item;
        }
    }

    public override string GetInspectStringLowPriority()
    {
        return this.ticksToFinish > 0 ? "Ticks to restore: " + this.ticksToFinish.ToString() : "";
    }
}
