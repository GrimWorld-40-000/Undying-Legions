using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Clears uncontrolled timer when a Nech gains an overseer relation.</summary>
[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation), typeof(PawnRelationDef), typeof(Pawn))]
public static class HarmonyPatch_NechOverseerRelationTimer
{
    [HarmonyPostfix]
    public static void Postfix(Pawn_RelationsTracker __instance, PawnRelationDef def, Pawn otherPawn)
    {
        if (def != PawnRelationDefOf.Overseer)
            return;

        Pawn owner = Traverse.Create(__instance).Field<Pawn>("pawn").Value;
        foreach (Pawn p in new[] { otherPawn, owner })
        {
            if (p?.def.GetModExtension<NecronMechExtension>() == null)
                continue;
            p.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkGained();
        }
    }
}
