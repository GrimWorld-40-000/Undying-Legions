// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HarmonyPatch_ShouldBeDead
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (Pawn_HealthTracker), "ShouldBeDead")]
public class HarmonyPatch_ShouldBeDead
{
  [HarmonyPostfix]
  public static void fix(Pawn ___pawn, ref bool __result)
  {
    if (___pawn.def.GetModExtension<NonOrganicPawn>() == null || ___pawn.health.hediffSet.GetBrain() == null)
      return;
    __result = false;
  }
}
