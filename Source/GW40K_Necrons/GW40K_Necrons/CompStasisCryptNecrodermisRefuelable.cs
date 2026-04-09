using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// <see cref="CompRefuelable"/> for stasis crypts: vanilla refuel UI / hauling; fuel is only burned from <see cref="NecronCasket"/> while the cycle advances.
/// </summary>
/// <remarks>
/// XML uses <c>consumeFuelOnlyWhenUsed</c> so <see cref="CompRefuelable"/> does not drain fuel every tick (vanilla default burn would run whenever the building is on).
/// Actual consumption is explicit: <see cref="BurnFuelForStasisProcessingTick"/>. Sync note for nutrition / fall rates: see <see cref="NecronStasisFuelUtility"/> remarks.
/// </remarks>
public class CompStasisCryptNecrodermisRefuelable : CompRefuelable
{
    public void BurnFuelForStasisProcessingTick()
    {
        if (!HasFuel)
            return;
        ConsumeFuel(NecronStasisFuelUtility.StasisFuelConsumedPerTick());
    }
}
