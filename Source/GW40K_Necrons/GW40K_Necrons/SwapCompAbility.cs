// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.SwapCompAbility
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class SwapCompAbility : CompAbilityEffect
{
  public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
  {
    IntVec3 position1 = this.parent.pawn.Position;
    Pawn pawn = target.Pawn;
    IntVec3 position2 = pawn.Position;
    EffecterDefOf.ForcedVisible.Spawn(position2, pawn.MapHeld);
    EffecterDefOf.ForcedVisible.Spawn(position1, pawn.MapHeld);
    this.parent.pawn.Position = position2;
    pawn.Position = position1;
  }
}
