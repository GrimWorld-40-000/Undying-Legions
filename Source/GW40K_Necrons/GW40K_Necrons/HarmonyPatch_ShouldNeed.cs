// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HarmonyPatch_ShouldNeed
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (Pawn_NeedsTracker), "ShouldHaveNeed")]
public class HarmonyPatch_ShouldNeed
{
  [HarmonyPostfix]
  public static void fix(NeedDef nd, Pawn ___pawn, ref bool __result)
  {
    if (nd.defName == "GW40K_NecronNeed")
      __result = ___pawn.def.GetModExtension<NonOrganicPawn>() != null;
    if (nd.defName == "Rest" && ___pawn.def.GetModExtension<NonOrganicPawn>() != null)
      __result = false;
    if (nd.defName == "Joy" && ___pawn.def.GetModExtension<NonOrganicPawn>() != null)
      __result = false;
    if (nd.defName == "Beauty" && ___pawn.def.GetModExtension<NonOrganicPawn>() != null)
      __result = false;
    if (nd.defName == "Comfort")
    {
      NonOrganicPawn modExtension = ___pawn.def.GetModExtension<NonOrganicPawn>();
      if (modExtension != null && !modExtension.comfortNeed)
        __result = false;
    }
    if (nd.defName == "Outdoors" && ___pawn.def.GetModExtension<NonOrganicPawn>() != null)
      __result = false;
    if (nd.defName == "Indoors" && ___pawn.def.GetModExtension<NonOrganicPawn>() != null)
      __result = false;
    if (!(nd.defName == "Mood") || ___pawn.def.GetModExtension<NonOrganicPawn>() == null)
      return;
    __result = false;
  }
}
