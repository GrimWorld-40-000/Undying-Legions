// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.JobGiver_GetMaintenance
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System;
using Verse;
using Verse.AI;
using Verse.AI.Group;

#nullable disable
namespace GW40K_Necrons;

public class JobGiver_GetMaintenance : ThinkNode_JobGiver
{
  private RestCategory minCategory;
  private float maxLevelPercentage = 1f;

  public override ThinkNode DeepCopy(bool resolve = true)
  {
    JobGiver_GetMaintenance giverGetMaintenance = (JobGiver_GetMaintenance) base.DeepCopy(resolve);
    giverGetMaintenance.minCategory = this.minCategory;
    giverGetMaintenance.maxLevelPercentage = this.maxLevelPercentage;
    return (ThinkNode) giverGetMaintenance;
  }

  public override float GetPriority(Pawn pawn)
  {
    Log.Message("a");
    Need_Rest rest = pawn.needs.rest;
    if (rest == null)
    {
      Log.Message("b");
      return 0.0f;
    }
    if (rest.CurCategory < this.minCategory)
    {
      Log.Message("c");
      return 0.0f;
    }
    if ((double) rest.CurLevelPercentage > (double) this.maxLevelPercentage)
    {
      Log.Message("d");
      return 0.0f;
    }
    if (Find.TickManager.TicksGame < pawn.mindState.canSleepTick)
    {
      Log.Message("e");
      return 0.0f;
    }
    Lord lord = pawn.GetLord();
    if (lord != null && !lord.CurLordToil.AllowSatisfyLongNeeds)
    {
      Log.Message("f");
      return 0.0f;
    }
    if (!RestUtility.CanFallAsleep(pawn))
    {
      Log.Message("g");
      return 0.0f;
    }
    TimeAssignmentDef timeAssignmentDef;
    if (pawn.RaceProps.Humanlike)
    {
      timeAssignmentDef = pawn.timetable == null ? TimeAssignmentDefOf.Anything : pawn.timetable.CurrentAssignment;
    }
    else
    {
      int num = GenLocalDate.HourOfDay((Thing) pawn);
      timeAssignmentDef = num < 7 || num > 21 ? TimeAssignmentDefOf.Sleep : TimeAssignmentDefOf.Anything;
    }
    float curLevel = rest.CurLevel;
    if (timeAssignmentDef == TimeAssignmentDefOf.Anything)
      return (double) curLevel < 0.30000001192092896 ? 8f : 0.0f;
    if (timeAssignmentDef == TimeAssignmentDefOf.Work)
      return 0.0f;
    if (timeAssignmentDef == TimeAssignmentDefOf.Meditate)
      return (double) curLevel < 0.15999999642372131 ? 8f : 0.0f;
    if (timeAssignmentDef == TimeAssignmentDefOf.Joy)
      return (double) curLevel < 0.30000001192092896 ? 8f : 0.0f;
    Log.Message("x");
    if (timeAssignmentDef == TimeAssignmentDefOf.Sleep)
      return 8f;
    throw new NotImplementedException();
  }

  protected override Job TryGiveJob(Pawn pawn)
  {
    MaintenanceNeed need = (MaintenanceNeed) pawn.needs.TryGetNeed(NecronDefOfs.GW40K_NecronNeed);
    if (need == null || (double) need.CurLevel > 0.20000000298023224)
    {
      Log.Message("1");
      return (Job) null;
    }
    Lord lord = pawn.GetLord();
    Log.Message("Lord lord = pawn.GetLord();");
    if (lord != null)
    {
      Log.Message("lord is not null");
      return (Job) null;
    }
    if (pawn.IsWildMan())
    {
      Log.Message("pawn is wild man");
      return (Job) null;
    }
    if (pawn.InMentalState)
    {
      Log.Message("mental state");
      return (Job) null;
    }
    if (pawn.roping.IsRoped)
    {
      Log.Message("pawn roping");
      return (Job) null;
    }
    NecronCasket casketFor = NecroCasketUtility.FindCasketFor(pawn);
    Log.Message("building_Bed = NecroCasketUtility.FindCasketFor(pawn);");
    if (casketFor == null)
    {
      Log.Message("3");
      return (Job) null;
    }
    Log.Message("if (building_Bed == null)");
    if (casketFor != null)
    {
      Log.Message("4");
      return JobMaker.MakeJob(JobDefOf.EnterCryptosleepCasket, (LocalTargetInfo) (Thing) casketFor);
    }
    Log.Message("5");
    return (Job) null;
  }

  private bool TryFindGroundSleepSpotFor(Pawn pawn, out IntVec3 cell)
  {
    Map map = pawn.Map;
    IntVec3 position = pawn.Position;
    if (pawn.RaceProps.Dryad && pawn.connections != null)
    {
      foreach (Thing connectedThing in pawn.connections.ConnectedThings)
      {
        if (pawn.CanReach((LocalTargetInfo) connectedThing, PathEndMode.Touch, Danger.Deadly))
        {
          position = connectedThing.Position;
          break;
        }
      }
    }
    else if (JobGiver_GetMaintenance.IsValidCell(pawn, position))
    {
      cell = position;
      return true;
    }
    for (int index = 0; index < 2; ++index)
    {
      int radius = index == 0 ? 4 : 12;
      IntVec3 result;
      if (CellFinder.TryRandomClosewalkCellNear(position, map, radius, out result, (Predicate<IntVec3>) (c => JobGiver_GetMaintenance.IsValidCell(pawn, c))))
      {
        cell = result;
        return true;
      }
    }
    cell = CellFinder.RandomClosewalkCellNearNotForbidden(pawn, 4, (Predicate<IntVec3>) (c => JobGiver_GetMaintenance.IsValidCell(pawn, c)));
    return JobGiver_GetMaintenance.IsValidCell(pawn, cell);
  }

  private static bool IsValidCell(Pawn pawn, IntVec3 cell)
  {
    return !cell.IsForbidden(pawn) && !cell.GetTerrain(pawn.Map).avoidWander && pawn.CanReserve((LocalTargetInfo) cell);
  }
}
