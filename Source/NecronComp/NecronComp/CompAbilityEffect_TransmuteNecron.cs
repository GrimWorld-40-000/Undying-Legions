// Decompiled with JetBrains decompiler
// Type: NecronComp.CompAbilityEffect_TransmuteNecron
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using RimWorld;
using RimWorld.Planet;
using System;
using UnityEngine;
using Verse;

#nullable disable
namespace NecronComp;

public class CompAbilityEffect_TransmuteNecron : CompAbilityEffect
{
  public CompProperties_TransmuteNecron Props => (CompProperties_TransmuteNecron) this.props;

  public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
  {
    base.Apply(target, dest);
    Thing thing1 = target.Thing;
    foreach (CompProperties_TransmuteNecron.ElementRatio elementRatio in this.Props.sourcePair)
    {
      if (thing1.def == elementRatio.sourceThing && (double) thing1.stackCount >= (double) elementRatio.ratio)
      {
        int num = Mathf.RoundToInt((float) thing1.stackCount / elementRatio.ratio);
        Thing thing2 = ThingMaker.MakeThing(elementRatio.outcomeThing);
        thing2.stackCount = num;
        GenPlace.TryPlaceThing(thing2, target.Cell, this.parent.pawn.Map, ThingPlaceMode.Direct);
        thing1.Destroy();
        break;
      }
    }
  }

  public override bool CanApplyOn(GlobalTargetInfo target)
  {
    return target.HasThing && this.TryGetElementFor(target.Thing.Stuff ?? target.Thing.def, out CompProperties_TransmuteNecron.ElementRatio _);
  }

  public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
  {
    return target.HasThing && this.TryGetElementFor(target.Thing.def, out CompProperties_TransmuteNecron.ElementRatio _);
  }

  private bool TryGetElementFor(
    ThingDef stuff,
    out CompProperties_TransmuteNecron.ElementRatio ratio)
  {
    ratio = this.Props.sourcePair.FirstOrDefault<CompProperties_TransmuteNecron.ElementRatio>((Predicate<CompProperties_TransmuteNecron.ElementRatio>) (x => x.sourceThing == stuff));
    return ratio != null;
  }

  public override bool AICanTargetNow(LocalTargetInfo target) => false;
}
