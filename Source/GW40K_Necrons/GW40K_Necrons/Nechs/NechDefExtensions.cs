using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Marker on Necron construct ThingDefs — gates Command Protocol / vanilla Mechlink control.</summary>
public class NecronMechExtension : DefModExtension { }

/// <summary>Monolith recipes: which Necron construct <see cref="PawnKindDef"/> to summon.</summary>
public class RecipeExtension_SpawnMech : DefModExtension
{
    public PawnKindDef mechKindDef;
}
