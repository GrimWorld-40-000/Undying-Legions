// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.JudgeAbility
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class JudgeAbility : CompAbilityEffect
{
  public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
  {
    Pawn pawn = target.Pawn;
    return pawn != null && pawn.InMentalState;
  }

  public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
  {
    base.Apply(target, dest);
    Pawn pawn = target.Pawn;
    if (pawn == null)
      return;
    EffecterDefOf.ForcedVisible.Spawn(pawn.Position, pawn.MapHeld);
    pawn.stances.stunner.StunFor(1200, (Thing) null);
    pawn.MentalState.RecoverFromState();
  }
}
