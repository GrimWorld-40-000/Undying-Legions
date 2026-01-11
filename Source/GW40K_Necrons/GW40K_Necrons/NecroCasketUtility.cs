// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.NecroCasketUtility
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System;
using System.Linq;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public static class NecroCasketUtility
{
  public static NecronCasket FindCasketFor(Pawn p)
  {
    return NecroCasketUtility.FindNecroCasket(p, p, true, guestStatus: p.GuestStatus);
  }

  public static NecronCasket FindNecroCasket(
    Pawn sleeper,
    Pawn traveler,
    bool checkSocialProperness,
    bool ignoreOtherReservations = false,
    GuestStatus? guestStatus = null)
  {
    for (int dg = 0; dg < 3; dg++)
    {
      Danger maxDanger = dg <= 1 ? Danger.None : Danger.Deadly;
      ThingDef gw40KMaintenanceBed = NecronDefOfs.GW40K_MaintenanceBed;
      if (RestUtility.CanUseBedEver(sleeper, gw40KMaintenanceBed))
      {
        NecronCasket necroCasket = (NecronCasket) GenClosest.ClosestThingReachable(sleeper.PositionHeld, sleeper.MapHeld, ThingRequest.ForDef(gw40KMaintenanceBed), PathEndMode.OnCell, TraverseParms.For(traveler), validator: (Predicate<Thing>) (b => b.Position.GetDangerFor(sleeper, sleeper.MapHeld) <= maxDanger && NecroCasketUtility.IsValidBedFor(b, sleeper, traveler, checkSocialProperness, ignoreOtherReservations: ignoreOtherReservations, guestStatus: guestStatus) && (dg > 0 || !b.Position.GetItems(b.Map).Any<Thing>((Func<Thing, bool>) (thing => thing.def.IsCorpse)))));
        if (necroCasket != null)
          return necroCasket;
      }
    }
    return (NecronCasket) null;
  }

  public static bool IsValidBedFor(
    Thing bedThing,
    Pawn sleeper,
    Pawn traveler,
    bool checkSocialProperness,
    bool allowMedBedEvenIfSetToNoCare = false,
    bool ignoreOtherReservations = false,
    GuestStatus? guestStatus = null)
  {
    if (!NecroCasketUtility.CanUseBedNow(bedThing, sleeper, checkSocialProperness, allowMedBedEvenIfSetToNoCare, guestStatus))
      return false;
    NecronCasket necronCasket = bedThing as NecronCasket;
    if (!traveler.CanReach((LocalTargetInfo) (Thing) necronCasket, PathEndMode.OnCell, Danger.Some) || !sleeper.HasReserved((LocalTargetInfo) (Thing) necronCasket) && !traveler.CanReserve((LocalTargetInfo) (Thing) necronCasket, ignoreOtherReservations: ignoreOtherReservations) || traveler.HasReserved<JobDriver_TakeToBed>((LocalTargetInfo) (Thing) necronCasket, new LocalTargetInfo?((LocalTargetInfo) (Thing) sleeper)) || necronCasket.IsForbidden(traveler))
      return false;
    GuestStatus? nullable1 = guestStatus;
    GuestStatus guestStatus1 = GuestStatus.Prisoner;
    bool flag1 = nullable1.GetValueOrDefault() == guestStatus1 & nullable1.HasValue;
    GuestStatus? nullable2 = guestStatus;
    GuestStatus guestStatus2 = GuestStatus.Slave;
    bool flag2 = nullable2.GetValueOrDefault() == guestStatus2 & nullable2.HasValue;
    return (flag1 | flag2 || necronCasket.Faction == traveler.Faction || traveler.HostFaction != null && necronCasket.Faction == traveler.HostFaction) && (!ModsConfig.AnomalyActive || !sleeper.IsMutant || !sleeper.mutant.Def.entitledToMedicalCare);
  }

  public static bool CanUseBedNow(
    Thing bedThing,
    Pawn sleeper,
    bool checkSocialProperness,
    bool allowMedBedEvenIfSetToNoCare = false,
    GuestStatus? guestStatusOverride = null)
  {
    if (!(bedThing is NecronCasket t) || !t.Spawned || t.Map != sleeper.MapHeld || t.IsBurning() || !NecroCasketUtility.CanUseCasketEver(sleeper, t.def))
      return false;
    if (sleeper.IsColonist)
    {
      Job curJob = sleeper.CurJob;
      if ((curJob == null || !curJob.ignoreForbidden) && !sleeper.Downed && t.IsForbidden(sleeper))
        return false;
    }
    return true;
  }

  public static bool CanUseCasketEver(Pawn p, ThingDef bedDef)
  {
    return !p.RaceProps.IsMechanoid && (!ModsConfig.BiotechActive || bedDef != ThingDefOf.DeathrestCasket || p.CanDeathrest());
  }
}
