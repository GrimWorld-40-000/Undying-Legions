using System;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Recharge-from-core gizmo respects <see cref="HediffComp_GaussCapacitor.allowCoreCharge"/>.</summary>
public sealed class Command_GaussRechargeFromCore : Command_Action
{
    private readonly Pawn nech;

    public Command_GaussRechargeFromCore(Pawn nech, Action action)
    {
        this.nech = nech;
        this.action = action;
    }

    public override bool Disabled => base.Disabled || !NechEnergyUtility.AllowCoreRecharge(nech);
}
