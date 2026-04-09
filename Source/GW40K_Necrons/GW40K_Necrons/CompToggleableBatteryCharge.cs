using Verse;

#nullable disable
namespace GW40K_Necrons;

public class CompToggleableBatteryCharge : ThingComp
{
    public bool allowBatteryCharge;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref allowBatteryCharge, "allowBatteryCharge", false);
    }
}

public class CompProperties_ToggleableBatteryCharge : CompProperties
{
    public CompProperties_ToggleableBatteryCharge()
    {
        compClass = typeof(CompToggleableBatteryCharge);
    }
}
