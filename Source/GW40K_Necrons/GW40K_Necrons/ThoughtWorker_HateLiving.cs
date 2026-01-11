// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.ThoughtWorker_HateLiving
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class ThoughtWorker_HateLiving : ThoughtWorker
{
  protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
  {
    return p.def.GetModExtension<NonOrganicPawn>() == null ? (ThoughtState) false : (ThoughtState) (otherPawn.def.GetModExtension<NonOrganicPawn>() == null && otherPawn.def.race.IsFlesh);
  }
}
