// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HarmonyPatch_Recruitable
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using RimWorld;
using System;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (Pawn_GuestTracker), "get_Recruitable")]
public static class HarmonyPatch_Recruitable
{
  [HarmonyPostfix]
  public static void Postfix(Pawn_GuestTracker __instance, ref bool __result)
  {
    Pawn pawn = Traverse.Create((object) __instance).Field("pawn").GetValue<Pawn>();
    if (pawn == null)
      return;
    bool flag = PawnsFinder.AllMapsAndWorld_Alive.Any<Pawn>((Predicate<Pawn>) (p => p.def.GetModExtension<NonOrganicPawn>() == null));
    if (!(pawn.def.GetModExtension<NonOrganicPawn>() != null & flag))
      return;
    __result = false;
  }
}
