// Decompiled with JetBrains decompiler
// Type: NecronComp.CompProperties_HediffAura
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NecronComp;

public class CompProperties_HediffAura : CompProperties
{
  public float radius;
  public HediffDef hediffDef;
  public float severityPerTrigger;
  public int interval = 250;
  public List<ThingDef> raceException = new List<ThingDef>();

  public CompProperties_HediffAura() => this.compClass = typeof (CompHediffAura);
}
