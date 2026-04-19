using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// When a Necron pawn is downed, their necron shield is dropped at their location.
/// The CompNecronShieldMemory stores the reference and re-issues a Wear job when
/// the pawn stands back up.
/// </summary>
// MakeDowned is non-public on Pawn_HealthTracker; string name is required for Harmony (nameof fails at compile).
[HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
public static class HarmonyPatch_NecronShieldOnDowned
{
    public static void Postfix(Pawn ___pawn)
    {
        Pawn pawn = ___pawn;
        if (pawn?.apparel == null || !pawn.Spawned) return;

        CompNecronShieldMemory comp = pawn.TryGetComp<CompNecronShieldMemory>();
        if (comp == null) return;

        Apparel shield = null;
        var worn = pawn.apparel.WornApparel;
        for (int i = 0; i < worn.Count; i++)
        {
            if (worn[i].def.defName == "GM40k_Necron_Shield")
            {
                shield = worn[i];
                break;
            }
        }

        if (shield == null) return;

        pawn.apparel.Remove(shield);
        if (GenPlace.TryPlaceThing(shield, pawn.Position, pawn.Map, ThingPlaceMode.Near, out Thing dropped))
            comp.droppedShield = dropped;
    }
}
