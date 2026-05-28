using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Re-enables work tags that are blocked by Necron genes/traits but should be
/// overridden by specific implants or higher-tier genes:
///   - Crafting: restored for Command Protocol implant bearers (Crypteks, Overlords).
///   - Social: restored for any pawn with an active GW_UD_SocialMatrix gene, allowing
///             high-tier Necrons (Lychguard, Deathmark, Cryptek, Overlord) to perform
///             social work despite carrying GW_UD_LowBorn.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.WorkTagIsDisabled))]
public static class HarmonyPatch_CommandProtocolWork
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, WorkTags w, ref bool __result)
    {
        if (!__result) return;

        if ((w & WorkTags.Crafting) != 0 &&
            __instance.health?.hediffSet?.GetFirstHediffOfDef(
                HediffDef.Named("GW40K_CommandProtocolImplant")) != null)
        {
            __result = false;
            return;
        }

        if ((w & WorkTags.Social) != 0 &&
            __instance.health?.hediffSet?.GetFirstHediffOfDef(
                HediffDef.Named("GW40K_Necron_SocialMatrix_Active")) != null)
        {
            __result = false;
        }
    }
}
