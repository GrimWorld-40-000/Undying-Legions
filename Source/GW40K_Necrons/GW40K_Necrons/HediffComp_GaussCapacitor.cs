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
    public bool allowBatteryCharge;

    /// <summary>When false, manual &quot;Recharge from core&quot; is blocked (Gauss gizmo core toggle).</summary>
    public bool allowCoreCharge = true;

    public HediffCompProperties_GaussCapacitor Props => (HediffCompProperties_GaussCapacitor)props;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref allowBatteryCharge, "allowBatteryCharge", false);
        Scribe_Values.Look(ref allowCoreCharge, "allowCoreCharge", true);
    }
}
