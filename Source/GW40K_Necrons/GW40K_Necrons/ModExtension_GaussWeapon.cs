using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Marks a weapon as requiring Gauss energy to operate.
/// Attach to a ThingDef via modExtensions.
/// </summary>
public class ModExtension_GaussWeapon : DefModExtension
{
    // Fraction of Need_NechEnergy.CurLevel (0-1) consumed per projectile fired.
    // Set to 0 to only require non-zero energy without per-shot draining.
    public float gaussConsumption = 0f;

    // When true, treat as a melee gauss weapon:
    //   - never fully disabled; instead reduces damage by 80% when energy is zero.
    //   - no per-shot consumption.
    public bool isMeleeGaussWeapon = false;
}
