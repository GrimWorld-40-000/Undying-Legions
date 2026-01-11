// Decompiled with JetBrains decompiler
// Type: NecronComp.CompStructureSelfRegen
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using Verse;

#nullable disable
namespace NecronComp;

public class CompStructureSelfRegen : ThingComp
{
  public CompProperties_StructureSelfRegen Props => (CompProperties_StructureSelfRegen) this.props;

  public override void CompTick()
  {
    base.CompTick();
    if (this.parent.Map == null || !this.parent.IsHashIntervalTick(this.Props.interval) || this.parent.HitPoints >= this.parent.MaxHitPoints)
      return;
    if (this.Props.restoreToFull)
      this.parent.HitPoints = this.parent.MaxHitPoints;
    else
      this.parent.HitPoints += this.Props.amountHealed;
  }
}
