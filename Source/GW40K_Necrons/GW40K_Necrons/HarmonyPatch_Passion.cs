// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HarmonyPatch_Passion
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof (PawnGenerator), "GenerateSkills")]
public class HarmonyPatch_Passion
{
  [HarmonyPostfix]
  public static void fix(Pawn pawn)
  {
    if (pawn.def.GetModExtension<NonOrganicPawn>() == null)
      return;
    string passion = pawn.def.GetModExtension<NonOrganicPawn>().passion;
    if (passion == null || passion == "")
      return;
    foreach (SkillRecord skill in pawn.skills.skills)
    {
      if (!skill.TotallyDisabled && skill.def.defName == passion)
        skill.passion = pawn.def.GetModExtension<NonOrganicPawn>().passionType;
    }
  }
}
