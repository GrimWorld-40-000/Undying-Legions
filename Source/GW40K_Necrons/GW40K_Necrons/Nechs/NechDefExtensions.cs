using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Necron construct marker — gates Command Protocol vs vanilla Mechlink control.
/// <see cref="commandBandwidthCost"/> is Nechinator-only (not the vanilla BandwidthCost stat), so Command Protocol bandwidth stays mod-local.
/// </summary>
public class NecronMechExtension : DefModExtension
{
    /// <summary>Command Protocol bandwidth consumed while this construct is bound. Default 1; override per race (e.g. Immortal / Flayed One = 2).</summary>
    public float commandBandwidthCost = 1f;

    /// <summary>Core size scalar. Also used as stasis length multiplier; effective stasis hours = global base × this.</summary>
    public float coreSize = 1f;
    public float eternalSlumberLengthFactor = 1f; // legacy compatibility
}

/// <summary>Monolith recipes: which Necron construct <see cref="PawnKindDef"/> to summon.</summary>
public class RecipeExtension_SpawnMech : DefModExtension
{
    public PawnKindDef mechKindDef;
}
