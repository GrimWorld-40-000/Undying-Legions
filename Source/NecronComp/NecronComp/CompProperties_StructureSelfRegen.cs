// Decompiled with JetBrains decompiler
// Type: NecronComp.CompProperties_StructureSelfRegen
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using Verse;

#nullable disable
namespace NecronComp;

public class CompProperties_StructureSelfRegen : CompProperties
{
  public int interval = 2500;
  public int amountHealed;
  public bool restoreToFull = false;

  public CompProperties_StructureSelfRegen() => this.compClass = typeof (CompStructureSelfRegen);
}
