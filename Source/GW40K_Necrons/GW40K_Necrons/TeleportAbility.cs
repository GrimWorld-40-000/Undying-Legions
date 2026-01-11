// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.TeleportAbility
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class TeleportAbility : CompAbilityEffect
{
  public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
  {
    base.Apply(target, dest);
    IntVec3 cell = target.Cell;
    EffecterDefOf.ForcedVisible.Spawn(this.parent.pawn.Position, this.parent.pawn.MapHeld);
    this.parent.pawn.Position = cell;
  }
}
