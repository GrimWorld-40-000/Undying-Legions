using Verse;

#nullable disable
namespace GW40K_Necrons;

public class HediffCompProperties_GaussCapacitor : HediffCompProperties
{
    public float capacity = 100f;
    public float mass = 5f;
    public float coreFluxCostFull = 0.25f;

    public HediffCompProperties_GaussCapacitor()
    {
        compClass = typeof(HediffComp_GaussCapacitor);
    }
}

public class HediffComp_GaussCapacitor : HediffComp
{
    public bool allowBatteryCharge = true;

    /// <summary>When false, manual &quot;Recharge from core&quot; is blocked (Gauss gizmo core toggle).</summary>
    public bool allowCoreCharge;

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        // Fill to 100% the moment a capacitor is installed so the pawn is
        // immediately combat-ready rather than starting at 0.
        Need_NechEnergy need = Pawn?.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (need != null)
            need.CurLevel = 1f;
    }

    public HediffCompProperties_GaussCapacitor Props => (HediffCompProperties_GaussCapacitor)props;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref allowBatteryCharge, "allowBatteryCharge", true);
        Scribe_Values.Look(ref allowCoreCharge, "allowCoreCharge", false);
    }
}
