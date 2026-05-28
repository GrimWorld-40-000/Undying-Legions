using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Central identity checks for Nech-controlled pawns — mechanical Nechs and humanlike constructs
/// that obey the Command Protocol.
/// </summary>
internal static class NechUtility
{
    // ── Identity helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Humanlike Flayed colonists use race <c>GW40K_NecronFlayedOne</c> with <see cref="NecronMechExtension"/> —
    /// same Nechinator command rules as mechanical Nechs.
    /// </summary>
    internal static bool IsHumanlikeFlayedNech(Pawn p) =>
        p?.RaceProps?.Humanlike == true && p.def?.defName == "GW40K_NecronFlayedOne";

    /// <summary>
    /// Pawn-based Nech colonist kinds. Race (<c>GW40K_NecronWarrior</c>) is shared with Crypteks and
    /// Overlords, so we gate by kindDef. Add new pawn-based Nech kinds here as they are introduced.
    /// </summary>
    private static readonly HashSet<string> PawnBasedNechKindDefs = new HashSet<string>
    {
        "GW_UL_NecronWarriorPawnKind_Colonist",
        "GW_UL_NecronImmortalPawnKind_Colonist",
        "GW_UL_CryptothrallPawnKind_Colonist",
    };

    internal static bool IsHumanlikeWarriorNech(Pawn p) =>
        p?.RaceProps?.Humanlike == true && PawnBasedNechKindDefs.Contains(p.kindDef?.defName);

    /// <summary>Any humanlike pawn that should receive full Nech command-system treatment.</summary>
    internal static bool IsHumanlikeNechControlled(Pawn p) =>
        IsHumanlikeFlayedNech(p) || IsHumanlikeWarriorNech(p);

    /// <summary>
    /// True for any pawn participating in the Nech command system — mechanical Nechs
    /// (via <see cref="NecronMechExtension"/>), humanlike Nech constructs, or Canoptek
    /// constructs linked by control nodes.
    /// </summary>
    internal static bool IsNechControlled(Pawn p) =>
        p?.def?.GetModExtension<NecronMechExtension>() != null
        || IsHumanlikeNechControlled(p)
        || ControlNodeUtility.IsCanoptek(p);
}

// ── Colonist bar ──────────────────────────────────────────────────────────────
// All Nech-controlled pawns (mechanical or humanlike construct) are managed through
// the Nechinator UI, not the colonist bar.

[HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
internal static class HarmonyPatch_NechHideFromColonistBar
{
    [HarmonyPostfix]
    internal static void Postfix(ColonistBar __instance)
    {
        List<ColonistBar.Entry> entries = Traverse.Create(__instance)
            .Field("cachedEntries")
            .GetValue<List<ColonistBar.Entry>>();

        entries?.RemoveAll(e => NechUtility.IsNechControlled(e.pawn));
    }
}

// ── Equipment retention (Nech / Flayed) ─────────────────────────────────────
// - DropAllEquipment: nech Flayed + humanlike Nech constructs keep gear when downed, etc.
// - TryDropEquipment: Flayed integrated claws cannot be stripped or dropped individually.

[HarmonyPatch]
internal static class HarmonyPatch_NechEquipmentRetention
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.DropAllEquipment))]
    [HarmonyPrefix]
    internal static bool PrefixDropAllEquipment(Pawn_EquipmentTracker __instance)
    {
        Pawn p = __instance.pawn;
        // MARKED FOR REMOVAL: UD_Necron_FlayedOne check removed — nech-based mech type removed.
        return !NechUtility.IsHumanlikeNechControlled(p);
    }

    private const string IntegratedFlayedClawDefName = "GW40k_Necron_Claw";

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    [HarmonyPrefix]
    internal static bool PrefixTryDropEquipment(
        Pawn_EquipmentTracker __instance,
        ThingWithComps eq,
        out ThingWithComps resultingEq,
        IntVec3 pos,
        bool forbid,
        ref bool __result)
    {
        resultingEq = null;
        Pawn pawn = __instance?.pawn;
        if (pawn == null || eq == null || eq.def?.defName != IntegratedFlayedClawDefName)
            return true;
        if (!IsFlayedOneWithIntegratedClaws(pawn))
            return true;

        __result = false;
        return false;
    }

    private static bool IsFlayedOneWithIntegratedClaws(Pawn pawn)
    {
        string race = pawn.def?.defName;
        if (race == "GW40K_NecronFlayedOne") // MARKED FOR REMOVAL: "UD_Necron_FlayedOne" removed (nech-based)
            return true;
        return pawn.kindDef?.defName == "GW_UL_NecronFlayedOnePawnKind_Colonist";
    }
}

// ── Stripping (Biotech): Nechs are not vanilla mech / colony strip targets ──

/// <summary>
/// Hides the Biotech "strip" affordance for Nech-controlled pawns (humanlike Command Protocol constructs and Canoptek).
/// </summary>
[HarmonyPatch(typeof(StrippableUtility), nameof(StrippableUtility.CanBeStrippedByColony))]
internal static class HarmonyPatch_NechNoStripByColony
{
    [HarmonyPostfix]
    internal static void Postfix(Thing th, ref bool __result)
    {
        if (!__result || th is not Pawn pawn)
            return;
        if (NechUtility.IsNechControlled(pawn))
            __result = false;
    }
}

// ── Undraft on mental break ───────────────────────────────────────────────────

[HarmonyPatch(typeof(MentalState), nameof(MentalState.PostStart))]
internal static class HarmonyPatch_NechUndraftOnMentalBreak
{
    [HarmonyPostfix]
    internal static void Postfix(MentalState __instance)
    {
        Pawn pawn = __instance.pawn;
        if (!NechUtility.IsNechControlled(pawn)) return;
        if (pawn.drafter == null || !pawn.Drafted) return;

        pawn.drafter.Drafted = false;
        pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
    }
}
