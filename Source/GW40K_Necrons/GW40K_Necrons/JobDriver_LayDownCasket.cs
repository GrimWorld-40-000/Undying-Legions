// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.JobDriver_LayDownCasket
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class JobDriver_LayDownCasket : JobDriver
{
  private bool canMoveOrCrawl;
  private bool hasSavedValues;
  public const TargetIndex BedOrRestSpotIndex = TargetIndex.A;

  public NecronCasket Bed => this.job.GetTarget(TargetIndex.A).Thing as NecronCasket;

  public IntVec3 Cell => this.job.GetTarget(TargetIndex.A).Cell;

  public virtual bool CanSleep => true;

  public virtual bool CanRest => true;

  public virtual bool LookForOtherJobs => true;

  public override bool TryMakePreToilReservations(bool errorOnFailed)
  {
    if (this.Bed != null)
    {
      if (!this.pawn.Reserve((LocalTargetInfo) (Thing) this.Bed, this.job, stackCount: 0, errorOnFailed: errorOnFailed))
        return false;
    }
    else if (this.job.targetA.Cell.IsValid && !this.pawn.ageTracker.CurLifeStage.alwaysDowned && !this.job.forceSleep && !this.pawn.Reserve((LocalTargetInfo) this.job.targetA.Cell, this.job, errorOnFailed: errorOnFailed))
      return false;
    return true;
  }

  public override bool CanBeginNowWhileLyingDown()
  {
    return JobInBedUtility.InBedOrRestSpotNow(this.pawn, this.job.GetTarget(TargetIndex.A));
  }

  public override void Notify_Starting()
  {
    base.Notify_Starting();
    this.canMoveOrCrawl = !this.pawn.Downed || this.pawn.health.CanCrawl;
    this.hasSavedValues = true;
  }

  protected override IEnumerable<Toil> MakeNewToils()
  {
    bool hasBed = this.Bed != null;
    if (hasBed)
    {
      yield return Toils_Bed.GotoBed(TargetIndex.A).FailOn<Toil>((Func<bool>) (() => this.pawn.Downed && !this.pawn.health.CanCrawl && !this.Bed.OccupiedRect().Contains(this.pawn.Position)));
    }
    else
    {
      Thing thing;
      if (((thing = this.job.GetTarget(TargetIndex.A).Thing) == null || this.pawn.SpawnedParentOrMe != thing) && this.canMoveOrCrawl)
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell).FailOn<Toil>((Func<bool>) (() => this.pawn.Downed && !this.pawn.health.CanCrawl));
      thing = (Thing) null;
    }
    yield return this.LayDownToil(hasBed);
  }

  public virtual Toil LayDownToil(bool hasBed)
  {
    return Toils_LayDown.LayDown(TargetIndex.A, hasBed, this.LookForOtherJobs, this.CanSleep, this.CanRest);
  }

  public override string GetReport()
  {
    string reportStringOverride = this.GetReportStringOverride();
    if (!reportStringOverride.NullOrEmpty())
      return reportStringOverride;
    if (this.asleep)
      return (string) "ReportSleeping".Translate();
    Thing spawnedParentOrMe;
    return (spawnedParentOrMe = this.pawn.SpawnedParentOrMe) != null && this.pawn != spawnedParentOrMe ? JobDriver_Carried.GetReport(this.pawn, spawnedParentOrMe) : (this.pawn.health.hediffSet.InLabor(false) ? (string) "GivingBirth".Translate() : (string) "ReportResting".Translate());
  }

  public override bool IsContinuation(Job j)
  {
    return !(this.job.GetTarget(TargetIndex.A) != j.GetTarget(TargetIndex.A)) && base.IsContinuation(j);
  }

  public override void ExposeData()
  {
    Scribe_Values.Look<bool>(ref this.canMoveOrCrawl, "canMoveOrCrawl");
    Scribe_Values.Look<bool>(ref this.hasSavedValues, "hasSavedValues");
    if (Scribe.mode == LoadSaveMode.PostLoadInit && !this.hasSavedValues)
    {
      this.canMoveOrCrawl = !this.pawn.Downed || this.pawn.health.CanCrawl;
      this.hasSavedValues = true;
    }
    base.ExposeData();
  }
}
