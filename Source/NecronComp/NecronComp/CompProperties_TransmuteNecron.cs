// Decompiled with JetBrains decompiler
// Type: NecronComp.CompProperties_TransmuteNecron
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using RimWorld;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NecronComp;

public class CompProperties_TransmuteNecron : CompProperties_AbilityEffect
{
  public List<CompProperties_TransmuteNecron.ElementRatio> sourcePair;
  public string failedMessage;

  public CompProperties_TransmuteNecron()
  {
    this.compClass = typeof (CompAbilityEffect_TransmuteNecron);
  }

  public class ElementRatio
  {
    public ThingDef sourceThing;
    public ThingDef outcomeThing;
    public float ratio = 3f;
  }
}
