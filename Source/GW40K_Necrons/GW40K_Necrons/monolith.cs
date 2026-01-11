// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.CompProperties_BuildingMechanitor
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using Verse;

#nullable disable
namespace GW40K_Necrons;

public class CompProperties_BuildingMechanitor : CompProperties
{
  public PawnKindDef mechKind;
  public float controlRadius = 20f;

  public CompProperties_BuildingMechanitor() => this.compClass = typeof (CompBuildingMechanitor);
}
