// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.Patch_PathFollower_TryEnterNextPathCell
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using System.Collections.Generic;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (Pawn_PathFollower), "TryEnterNextPathCell")]
public static class Patch_PathFollower_TryEnterNextPathCell
{
  private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> pawnRef = AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

  public static void Postfix(Pawn_PathFollower __instance)
  {
    Pawn mech = Patch_PathFollower_TryEnterNextPathCell.pawnRef.Invoke(__instance);
    if (mech == null || mech.Map == null || !mech.RaceProps.IsMechanoid)
      return;
    List<Thing> thingList = mech.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
    if (thingList == null)
      return;
    for (int index = 0; index < thingList.Count; ++index)
    {
      CompBuildingMechanitor comp = thingList[index].TryGetComp<CompBuildingMechanitor>();
      if (comp != null && comp.controlledMechs.Contains(mech) && !comp.WithinControlRadius(mech))
      {
        comp.ForceReturn(mech);
        break;
      }
    }
  }
}
