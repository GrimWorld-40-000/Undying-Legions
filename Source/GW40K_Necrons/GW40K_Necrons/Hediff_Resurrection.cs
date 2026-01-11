// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.Hediff_Resurrection
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class Hediff_Resurrection : HediffWithComps
{
  private void Heal()
  {
    List<Hediff> hediffList = new List<Hediff>();
    foreach (Hediff hediff in this.pawn.health.hediffSet.hediffs.Where<Hediff>((Func<Hediff, bool>) (hediff => hediff is Hediff_MissingPart)).ToList<Hediff>())
      hediffList.Add(hediff);
    foreach (Hediff hediff in hediffList)
      this.pawn.health.RestorePart(hediff.Part);
    this.pawn.health.RestorePart(this.pawn.health.hediffSet.GetBrain());
    foreach (Hediff hediff in this.pawn.health.hediffSet.hediffs)
      hediff.Heal(999999f);
    this.pawn.Drawer.renderer.SetAllGraphicsDirty();
  }

  public override void PostTick()
  {
    if (this.GetComp<HediffComp_Disappears>().ticksToDisappear <= 1)
    {
      Log.Message("necron healed!");
      this.Heal();
    }
    base.PostTick();
  }
}
