// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.MaintenanceNeed
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class MaintenanceNeed : Need
{
  private int lastRestTick = -999;
  private float lastRestEffectiveness = 1f;
  private int ticksAtZero;
  private const float FullSleepHours = 10.5f;
  public const float BaseRestGainPerTick = 3.809524E-05f;
  private const float BaseRestFallPerTick = 1.58333332E-05f;
  public const float ThreshTired = 0.28f;
  public const float ThreshVeryTired = 0.14f;
  public const float DefaultFallAsleepMaxLevel = 0.75f;
  public const float DefaultNaturalWakeThreshold = 1f;
  public const float CanWakeThreshold = 0.2f;
  private const float BaseInvoluntarySleepMTBDays = 0.25f;

  public RestCategory CurCategory
  {
    get
    {
      if ((double) this.CurLevel < 0.0099999997764825821)
        return RestCategory.Exhausted;
      return (double) this.CurLevel < 0.14000000059604645 ? RestCategory.VeryTired : ((double) this.CurLevel < 0.2800000011920929 ? RestCategory.Tired : RestCategory.Rested);
    }
  }

  public float RestFallPerTick => 1f / 1000f;

  public override int GUIChangeArrow => -1;

  public MaintenanceNeed(Pawn pawn)
    : base(pawn)
  {
    this.threshPercents = new List<float>();
    this.threshPercents.Add(0.28f);
    this.threshPercents.Add(0.14f);
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Values.Look<int>(ref this.ticksAtZero, "ticksAtZero");
  }

  public override void SetInitialLevel() => this.CurLevel = Rand.Range(0.9f, 1f);

  public override void NeedInterval()
  {
    if (this.IsFrozen || (double) this.CurLevel <= 0.0)
      return;
    this.CurLevel -= this.RestFallPerTick;
  }

  public void TickResting(float restEffectiveness)
  {
    if ((double) restEffectiveness <= 0.0)
      return;
    this.lastRestTick = Find.TickManager.TicksGame;
    this.lastRestEffectiveness = restEffectiveness;
  }
}
