// Decompiled with JetBrains decompiler
// Type: NecronComp.CompHediffAura
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using Verse;

#nullable disable
namespace NecronComp;

public class CompHediffAura : ThingComp
{
  public CompProperties_HediffAura Props => (CompProperties_HediffAura) this.props;

  public override void CompTick()
  {
    base.CompTick();
    if (this.parent.Map == null || !this.parent.IsHashIntervalTick(this.Props.interval))
      return;
    this.GiveHediff();
  }

  public void GiveHediff()
  {
    foreach (Pawn pawn in this.parent.Position.GetNearbyPawnFriendAndFoe(this.parent.Map, this.Props.radius))
    {
      if (!this.Props.raceException.Contains(pawn.def))
        pawn.health.GetOrAddHediff(this.Props.hediffDef).Severity += this.Props.severityPerTrigger;
    }
  }
}
