using Verse;

#nullable disable
namespace GW40K_Necrons;

public class CompProperties_BuildingMechanitor : CompProperties
{
    public PawnKindDef mechKind;
    public float controlRadius = 20f;

    public CompProperties_BuildingMechanitor() => this.compClass = typeof(CompBuildingMechanitor);
}
