using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace GW40K_Necrons;

// Lightweight deferred-action scheduler. Schedule(delayTicks, action) fires
// action after at least delayTicks game ticks. Requires no XML registration —
// the Harmony patch on TickManager.DoSingleTick drives the queue each tick.
public static class DeferredActions
{
    private static readonly List<(int tick, Action action)> Pending = new();

    public static void Schedule(int delayTicks, Action action)
    {
        Pending.Add((Find.TickManager.TicksGame + delayTicks, action));
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class Patch_TickManager_DoSingleTick
    {
        public static void Postfix()
        {
            if (Pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (Pending[i].tick > now) continue;
                Pending[i].action?.Invoke();
                Pending.RemoveAt(i);
            }
        }
    }
}
