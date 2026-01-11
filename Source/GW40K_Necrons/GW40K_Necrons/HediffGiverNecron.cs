// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HediffGiverNecron
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffGiverNecron : HediffGiver
{
  public override void OnIntervalPassed(Pawn pawn, Hediff cause)
  {
    if (pawn.Dead)
      return;
    RestCategory curCategory = pawn.needs.TryGetNeed<MaintenanceNeed>().CurCategory;
    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_TiredHediff);
    if (hediff == null && curCategory != 0)
    {
      hediff = HediffMaker.MakeHediff(NecronDefOfs.GW40K_Necron_TiredHediff, pawn);
      hediff.Severity = 0.1f;
      pawn.health.AddHediff(hediff);
    }
    switch (curCategory)
    {
      case RestCategory.Rested:
        if (hediff == null)
          break;
        pawn.health.RemoveHediff(hediff);
        break;
      case RestCategory.Tired:
        hediff.Severity = 0.2f;
        break;
      case RestCategory.VeryTired:
        hediff.Severity = 0.4f;
        break;
      case RestCategory.Exhausted:
        hediff.Severity = 0.8f;
        break;
    }
  }
}
