using RimWorld;

namespace GW40K_Necrons;

public class ScarabSelfDestructProperties : CompProperties_AbilityEffect
{
    // Maximum damage when all 4 scarab units are alive
    public float baseDamage = 30f;

    // Maximum explosion radius when all 4 scarab units are alive
    public float baseRadius = 4.9f;

    public ScarabSelfDestructProperties() => compClass = typeof(ScarabSelfDestruct);
}
