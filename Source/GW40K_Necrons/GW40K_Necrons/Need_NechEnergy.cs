using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public class Need_NechEnergy : Need
{
    private const float FallPerDay = 0.9f;
    private const float ChargerGainPerDayHalfRate = 0.5f;
    private const float MonolithGainPerDay = 1.0f;
    private const float BatteryGainPerDayHalfRate = 0.35f;
    private const float BatteryDrainWdPerDay = 1200f;
    private const float IntervalsPerDay = 400f;

    public Need_NechEnergy(Pawn pawn)
        : base(pawn)
    {
    }

    public override void SetInitialLevel()
    {
        CurLevel = 0.75f;
    }

    public override int GUIChangeArrow => IsChargingNow() ? 1 : -1;

    public override void NeedInterval()
    {
        float cap = NechEnergyUtility.CapacitorCapacity(pawn);
        if (cap <= 0f)
        {
            CurLevel = 0f;
            return;
        }

        float delta = -FallPerDay / IntervalsPerDay;
        if (IsUsingVanillaChargeJob())
            delta += ChargerGainPerDayHalfRate / IntervalsPerDay;
        if (IsNearPoweredMonolith())
            delta += MonolithGainPerDay / IntervalsPerDay;
        if (TryDrainBatteryForCharge())
            delta += BatteryGainPerDayHalfRate / IntervalsPerDay;

        CurLevel += delta;
    }

    public bool IsChargingNow() =>
        IsUsingVanillaChargeJob() || IsNearPoweredMonolith() || (NechEnergyUtility.AllowBatteryCharge(pawn) && HasBatteryToDrain());

    private bool IsUsingVanillaChargeJob()
    {
        string n = pawn?.CurJobDef?.defName;
        if (n.NullOrEmpty())
            return false;
        return n.IndexOf("Charge", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsNearPoweredMonolith()
    {
        if (!pawn.Spawned || pawn.Map == null)
            return false;
        foreach (Building b in pawn.Map.listerBuildings.allBuildingsColonist)
        {
            if (b?.def?.defName == null)
                continue;
            if (b.def.defName.IndexOf("Monolith", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (b.Position.DistanceTo(pawn.Position) > 6f)
                continue;
            CompPowerTrader p = b.GetComp<CompPowerTrader>();
            if (p == null || p.PowerOn)
                return true;
        }
        return false;
    }

    private bool TryDrainBatteryForCharge()
    {
        if (!NechEnergyUtility.AllowBatteryCharge(pawn))
            return false;
        CompPowerBattery battery = FindClosestBattery();
        if (battery == null)
            return false;
        float drainPerInterval = BatteryDrainWdPerDay / IntervalsPerDay;
        if (battery.StoredEnergy <= drainPerInterval)
            return false;
        battery.DrawPower(drainPerInterval);
        return true;
    }

    private bool HasBatteryToDrain() => FindClosestBattery() != null;

    private CompPowerBattery FindClosestBattery()
    {
        if (!pawn.Spawned || pawn.Map == null)
            return null;
        CompPowerBattery best = null;
        float bestDist = float.MaxValue;
        foreach (Building b in pawn.Map.listerBuildings.allBuildingsColonist)
        {
            CompPowerBattery c = b?.GetComp<CompPowerBattery>();
            if (c == null || c.StoredEnergy <= 1f)
                continue;
            float d = b.Position.DistanceTo(pawn.Position);
            if (d < bestDist && d <= 20f)
            {
                best = c;
                bestDist = d;
            }
        }
        return best;
    }
}
