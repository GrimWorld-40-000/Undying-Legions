using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Adds "Recharge gauss capacitor" to the right-click float menu when a Necron with a gauss
/// capacitor right-clicks a power battery. For Nech pawns the existing
/// HarmonyPatch_NechOrderedJobRange enforces command range automatically.
/// </summary>
[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
public static class HarmonyPatch_BatteryRechargeFloatMenu
{
    [HarmonyPostfix]
    public static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
    {
        if (__result == null)
            return;
        Pawn pawn = context.FirstSelectedPawn;
        if (pawn == null || !NechEnergyUtility.AllowBatteryCharge(pawn))
            return;

        for (int i = 0; i < context.ClickedThings?.Count; i++)
        {
            CompPowerBattery battery = context.ClickedThings[i]?.TryGetComp<CompPowerBattery>();
            if (battery == null)
                continue;

            if (battery.StoredEnergy < 1f)
            {
                __result.Add(new FloatMenuOption("GW40K_RechargeGaussNoEnergy".Translate(), null));
                break;
            }

            if (!pawn.CanReach(battery.parent, PathEndMode.InteractionCell, Danger.Deadly))
            {
                __result.Add(new FloatMenuOption("GW40K_RechargeGaussNoPath".Translate(), null));
                break;
            }

            Thing batteryThing = battery.parent;
            __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("GW40K_RechargeGauss".Translate(), delegate
                {
                    Job job = JobMaker.MakeJob(NecronDefOfs.GW40K_Job_RechargeFromBattery, batteryThing);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                pawn,
                batteryThing));
            break;
        }
    }
}
