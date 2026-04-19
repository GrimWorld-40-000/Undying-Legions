using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
public static class HarmonyPatch_StasisCryptLoadLabel
{
    public static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
    {
        if (__result == null || context.ClickedThings == null)
            return;

        bool relabelCarryToCryptosleep = false;
        for (int i = 0; i < context.ClickedThings.Count; i++)
        {
            if (context.ClickedThings[i] is NecronCasket)
            {
                relabelCarryToCryptosleep = true;
                break;
            }
        }

        // Right-clicking the downed pawn (not the building) still builds "carry to cryptosleep casket" from JobDefOf;
        // relabel whenever a stasis crypt is actually reachable for the sleeper so vanilla casket orders stay unchanged.
        if (!relabelCarryToCryptosleep && context.FirstSelectedPawn != null)
        {
            for (int i = 0; i < context.ClickedThings.Count; i++)
            {
                if (context.ClickedThings[i] is not Pawn sleeper)
                    continue;
                if (
                    NecroCasketUtility.FindNecroCasket(
                        sleeper,
                        context.FirstSelectedPawn,
                        checkSocialProperness: true,
                        ignoreOtherReservations: true,
                        guestStatus: sleeper.GuestStatus)
                    == null)
                    continue;
                relabelCarryToCryptosleep = true;
                break;
            }
        }

        if (!relabelCarryToCryptosleep)
            return;

        for (int i = 0; i < __result.Count; i++)
        {
            FloatMenuOption option = __result[i];
            string label = option?.Label;
            if (label.NullOrEmpty())
                continue;

            string lower = label.ToLowerInvariant();
            if (!lower.Contains("cryptosleep casket") || !lower.Contains("carry"))
                continue;

            option.Label = lower.StartsWith("cannot ")
                ? "GW40K_CannotLoadIntoStasisCrypt".Translate()
                : "GW40K_LoadIntoStasisCrypt".Translate();
        }
    }
}
