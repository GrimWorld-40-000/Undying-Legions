// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.JumpAbility
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class JumpAbility : CompAbilityEffect
{
  public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
  {
    LongEventHandler.QueueLongEvent((Action) (() =>
    {
      IntVec3 intVec3 = target.Cell + ((this.parent.pawn.Position - target.Cell).ToVector3().normalized * 2f).ToIntVec3();
      Map map = this.parent.pawn.Map;
      ThingDef pawnFlyer = ThingDefOf.PawnFlyer;
      Pawn pawn = this.parent.pawn;
      IntVec3 destCell = intVec3;
      EffecterDef forcedVisible = EffecterDefOf.ForcedVisible;
      Ability parent = this.parent;
      LocalTargetInfo localTargetInfo = dest;
      Vector3? overrideStartVec = new Vector3?();
      Ability triggeringAbility = parent;
      LocalTargetInfo target1 = localTargetInfo;
      GenSpawn.Spawn((Thing) PawnFlyer.MakeFlyer(pawnFlyer, pawn, destCell, forcedVisible, (SoundDef) null, overrideStartVec: overrideStartVec, triggeringAbility: triggeringAbility, target: target1), target.Cell, map);
    }), "necronFlyAbility", false, (Action<Exception>) null);
  }
}
