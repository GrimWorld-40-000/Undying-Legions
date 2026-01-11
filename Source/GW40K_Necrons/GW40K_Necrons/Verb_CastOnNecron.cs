// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.Verb_CastOnNecron
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

internal class Verb_CastOnNecron : Verb_CastAbility
{
  public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
  {
    return base.ValidateTarget(target, showMessages) && target.Pawn.def.GetModExtension<NonOrganicPawn>() != null;
  }
}
