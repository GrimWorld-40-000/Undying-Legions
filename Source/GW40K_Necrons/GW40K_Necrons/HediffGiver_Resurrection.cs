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
  // Accumulates ticks spent below critical health threshold.
  // Resurrection only triggers after sustained critical injury,
  // preventing the hediff from firing on a brief dip below 10%.
  private float timeDowned = 0.0f;

  // 1800 ticks = 3 check intervals = ~30 seconds of sustained critical health
  private const float DownedTicksRequired = 1800f;

  public override void OnIntervalPassed(Pawn pawn, Hediff cause)
  {
    if (!pawn.IsHashIntervalTick(600))
      return;

    // Reset timer if pawn has recovered above the critical threshold
    if ((double)pawn.health.summaryHealth.SummaryHealthPercent >= 0.10000000149011612)
    {
      timeDowned = 0f;
      return;
    }

    // Already in the process of resurrecting — don't stack
    if (pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionHediff) != null)
      return;

    timeDowned += 600f;
    if (timeDowned >= DownedTicksRequired)
    {
      HealthUtility.AdjustSeverity(pawn, this.hediff, 0.999f);
      timeDowned = 0f;
    }
  }
}
