// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.Patch_FinishRecipeAndStartStoringProduct
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using System;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (Toils_Recipe), "FinishRecipeAndStartStoringProduct")]
public static class Patch_FinishRecipeAndStartStoringProduct
{
  public static void Postfix(Toil __result, TargetIndex billGiverInd)
  {
    __result.AddFinishAction((Action) (() =>
    {
      Pawn actor = __result.actor;
      if (actor == null)
        return;
      Job curJob = actor.CurJob;
      if (curJob == null || !(curJob.targetA.Thing is Building thing2))
        return;
      CompBuildingMechanitor comp = thing2.GetComp<CompBuildingMechanitor>();
      if (comp == null)
        return;
      PawnKindDef mechKind = comp.mechKind;
      if (mechKind == null)
        return;
      comp.QueueMechSpawn(mechKind);
    }));
  }
}
