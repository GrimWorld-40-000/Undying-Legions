using Verse;

#nullable disable
namespace NecronComp;

public class CompStructureSelfRegen : ThingComp
{
    public CompProperties_StructureSelfRegen Props => (CompProperties_StructureSelfRegen)this.props;
    public override void CompTick() { }
}
