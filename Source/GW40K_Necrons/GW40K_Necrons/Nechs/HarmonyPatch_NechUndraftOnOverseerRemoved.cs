using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// When an overseer link is removed, drop draft on the nech so it cannot stay in drafted state while uncontrolled.
/// </summary>
[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.RemoveDirectRelation), typeof(PawnRelationDef), typeof(Pawn))]
public static class HarmonyPatch_NechUndraftOnOverseerRemoved
{
    [HarmonyPostfix]
    public static void Postfix(Pawn_RelationsTracker __instance, PawnRelationDef def, Pawn otherPawn)
    {
        if (def != PawnRelationDefOf.Overseer)
            return;

        Pawn nech = null;
        if (otherPawn != null && otherPawn.def.GetModExtension<NecronMechExtension>() != null)
            nech = otherPawn;
        else
        {
            Pawn owner = Traverse.Create(__instance).Field<Pawn>("pawn").Value;
            if (owner?.def.GetModExtension<NecronMechExtension>() != null)
                nech = owner;
        }

        if (nech == null || nech.Destroyed)
            return;
        if (nech.GetOverseer() != null)
            return;
        if (nech.drafter == null || !nech.Drafted)
            return;

        nech.drafter.Drafted = false;
        nech.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
        nech.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkLost();
    }
}
