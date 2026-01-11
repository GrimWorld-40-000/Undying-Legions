// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.HediffGiver_Resurrection
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffGiver_Resurrection : HediffGiver
{
  private float timeDowned = 0.0f;

  public override void OnIntervalPassed(Pawn pawn, Hediff cause)
  {
    if (!pawn.IsHashIntervalTick(600) || (double) pawn.health.summaryHealth.SummaryHealthPercent >= 0.10000000149011612 || pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionHediff) != null)
      return;
    HealthUtility.AdjustSeverity(pawn, this.hediff, 0.999f);
  }
}
