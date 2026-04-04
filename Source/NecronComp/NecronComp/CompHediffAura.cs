using Verse;

#nullable disable
namespace NecronComp;

public class CompHediffAura : ThingComp
{
    public CompProperties_HediffAura Props => (CompProperties_HediffAura)this.props;
    public override void CompTick() { }
    public void GiveHediff() { }
}
