using System;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Recharge-from-core gizmo. allowCoreCharge only gates auto-siphon behavior;
/// a player-issued order should always be allowed regardless of the toggle.
/// </summary>
public sealed class Command_GaussRechargeFromCore : Command_Action
{
    private readonly Pawn nech;

    public Command_GaussRechargeFromCore(Pawn nech, Action action)
    {
        this.nech = nech;
        this.action = action;
    }

    // Removed AllowCoreRecharge check — allowCoreCharge controls auto-consumption only.
    public override bool Disabled => base.Disabled;
}
