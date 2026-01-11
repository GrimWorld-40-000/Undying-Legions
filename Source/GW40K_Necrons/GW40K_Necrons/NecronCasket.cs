// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.NecronCasket
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

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

  public override bool Accepts(Thing thing) => thing.def.GetModExtension<NonOrganicPawn>() != null;

  public override void Open()
  {
    if (!this.HasAnyContents)
      return;
    this.EjectContents();
    if (this.openedSignal.NullOrEmpty())
      return;
    Find.SignalManager.SendSignal(new Signal(this.openedSignal, this.Named("SUBJECT")));
  }

  public override void EjectContents()
  {
    ThingDef filthSlime = ThingDefOf.Filth_Slime;
    foreach (Thing thing in (IEnumerable<Thing>) this.innerContainer)
    {
      if (thing is Pawn pawn)
      {
        PawnComponentsUtility.AddComponentsForSpawn(pawn);
        pawn.filth.GainFilth(filthSlime);
      }
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
    if (this.ContainedThing == null)
      return;
    if (this.ticksToFinish > 0)
      --this.ticksToFinish;
    if (this.ticksToFinish > 0)
      return;
    this.ticksToFinish = -1;
    Pawn pawn = this.innerContainer.First<Thing>() as Pawn;
    pawn.health.RemoveAllHediffs();
    pawn.needs.TryGetNeed(NecronDefOfs.GW40K_NecronNeed).CurLevel = 1f;
    this.Open();
  }

  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
  {
    if (!this.Accepts((Thing) myPawn))
      yield return new FloatMenuOption("Pawn is not Necron", (Action) null);
    else if (myPawn.IsQuestLodger())
      yield return new FloatMenuOption((string) "CannotUseReason".Translate((NamedArgument) "CryptosleepCasketGuestsNotAllowed".Translate()), (Action) null);
    else if (this.innerContainer.Count == 0)
    {
      if (!myPawn.CanReach((LocalTargetInfo) (Thing) this, PathEndMode.InteractionCell, Danger.Deadly))
      {
        yield return new FloatMenuOption((string) "CannotUseNoPath".Translate(), (Action) null);
      }
      else
      {
        foreach (FloatMenuOption item in base.GetFloatMenuOptions(myPawn))
          yield return item;
      }
    }
  }

  public override string GetInspectStringLowPriority()
  {
    return this.ticksToFinish > 0 ? "Ticks left: " + this.ticksToFinish.ToString() : "";
  }
}
