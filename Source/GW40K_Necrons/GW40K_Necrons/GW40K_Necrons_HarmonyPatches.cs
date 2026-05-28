// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.GW40K_Necrons_HarmonyPatches
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class GW40K_Necrons_HarmonyPatches : Mod
{
  public GW40K_Necrons_HarmonyPatches(ModContentPack content)
    : base(content)
  {
    new Harmony(nameof (GW40K_Necrons_HarmonyPatches)).PatchAll();
  }
}

/// <summary>
/// Reveals Command Protocol and Control Node learning helpers the first time a player
/// recruits a pawn that carries either hediff.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
static class HarmonyPatch_RevealConceptsOnRecruit
{
    static void Postfix(Pawn __instance, Faction newFaction)
    {
        if (newFaction != Faction.OfPlayer) return;
        if (__instance?.health?.hediffSet == null) return;

        // Command Protocol
        if (NecronDefOfs.GW_UD_Concept_CommandProtocol != null)
        {
            HediffDef cpDef = DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_CommandProtocol");
            HediffDef cpImplant = DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_CommandProtocolImplant");
            if ((cpDef != null && __instance.health.hediffSet.HasHediff(cpDef))
             || (cpImplant != null && __instance.health.hediffSet.HasHediff(cpImplant)))
                LessonAutoActivator.TeachOpportunity(NecronDefOfs.GW_UD_Concept_CommandProtocol, OpportunityType.GoodToKnow);
        }

        // Control Node
        if (NecronDefOfs.GW_UD_Concept_ControlNode != null
            && NecronDefOfs.GW40K_ControlNodeImplant != null
            && __instance.health.hediffSet.HasHediff(NecronDefOfs.GW40K_ControlNodeImplant))
            LessonAutoActivator.TeachOpportunity(NecronDefOfs.GW_UD_Concept_ControlNode, OpportunityType.GoodToKnow);
    }
}

[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
static class HarmonyPatch_ApparelWear_MindshackleArtifact
{
    static void Postfix(Pawn_ApparelTracker __instance, Apparel newApparel)
    {
        if (newApparel is MindshackleArtifactApparel artifact)
            artifact.OnWorn(__instance.pawn);
    }
}

/// <summary>
/// Blocks non-Overlord pawns from seeing a valid wear option for items on the
/// GW40K_Artifact apparel layer.
/// </summary>
[HarmonyPatch(typeof(FloatMenuOptionProvider_Wear), "GetSingleOptionFor")]
static class HarmonyPatch_ArtifactEquipRestriction
{
    static void Postfix(ref FloatMenuOption __result, Thing clickedThing, FloatMenuContext context)
    {
        if (__result == null) return;
        if (clickedThing?.def?.apparel?.layers == null) return;
        if (!clickedThing.def.apparel.layers.Any(l => l.defName == "GW40K_Artifact")) return;

        Pawn pawn = context.FirstSelectedPawn;
        if (pawn == null) return;

        GeneDef overlordGene = DefDatabase<GeneDef>.GetNamedSilentFail("GW_UD_NecronOverlord");
        if (overlordGene != null && pawn.genes?.HasActiveGene(overlordGene) == true) return;

        __result = new FloatMenuOption("Only a Necron Overlord can equip this artifact.", null);
    }
}

/// <summary>
/// Applies Vekh Thrall hediffs (Painblocker + necrodermis colonization) to any pawn
/// generated with a Vekh Thrall pawnkind, regardless of which backstory was picked.
/// Checking pawnkind rather than the GW_UL_VekhThrall trait means the hediffs apply
/// even when a vanilla backstory is rolled instead of the custom Vekh one.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
    new[] { typeof(PawnGenerationRequest) })]
static class HarmonyPatch_VekhThrallPainblocker
{
    private static readonly HashSet<string> VekhKindDefs = new()
    {
        "GW_UL_VekhThrall",
        "GW_UL_VekhThrall_Breacher",
        "GW_UL_VekhThrall_Sapper",
    };

    static void Postfix(Pawn __result)
    {
        if (__result?.kindDef == null) return;
        if (!VekhKindDefs.Contains(__result.kindDef.defName)) return;

        HediffDef hediff = DefDatabase<HediffDef>.GetNamedSilentFail("GW_UL_VekhThrallPainblocker");
        if (hediff != null && !__result.health.hediffSet.HasHediff(hediff))
            __result.health.AddHediff(hediff);

        // Necrodermis colonization at random severity 10–75 % (spans latent → vascular stages).
        HediffDef colonDef = DefDatabase<HediffDef>.GetNamedSilentFail("Necron_NecrodermisGrowth");
        if (colonDef != null && !__result.health.hediffSet.HasHediff(colonDef))
        {
            Hediff colon = HediffMaker.MakeHediff(colonDef, __result);
            colon.Severity = Rand.Range(0.1f, 0.75f);
            __result.health.AddHediff(colon);
        }

        // Strip headgear: apparelAllowHeadgearChance=0 doesn't block ideology-desired masks
        // (Apparel_WarMask has ideoDesireAllowedFactionCategoryTags=Tribal, bypassing the chance).
        if (__result.apparel != null)
        {
            var headgear = __result.apparel.WornApparel
                .Where(a => a.def?.apparel?.bodyPartGroups?.Any(g =>
                    g.defName == "UpperHead" || g.defName == "FullHead") == true)
                .ToList();
            foreach (var item in headgear)
            {
                __result.apparel.GetDirectlyHeldThings().Remove(item);
                if (!item.Destroyed) item.Destroy();
            }
        }
    }
}

/// <summary>
/// ThinkNode_ForbidOutsideFlagRadius NullRefs [Ref 22F8EEF7] when pawn.mindState.duty is null
/// mid-tick — any vanilla duty (Defend, AssaultColony) that includes this node can hit this
/// when duty is cleared/replaced between UpdateAllDuties and the think tree evaluating.
/// Returning NoJob is the safe exit; the pawn will pick up a proper job next tick.
/// </summary>
[HarmonyPatch(typeof(ThinkNode_ForbidOutsideFlagRadius), nameof(ThinkNode_ForbidOutsideFlagRadius.TryIssueJobPackage))]
static class HarmonyPatch_ForbidOutsideFlagRadius_NullDuty
{
    static bool Prefix(Pawn pawn, ref ThinkResult __result)
    {
        if (pawn?.mindState?.duty == null)
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        return true;
    }
}

/// <summary>
/// Prevents JobGiver_GetFood from throwing when run on a pawn with no food need.
/// DutyDef.Defend's thinkNode runs SatisfyBasicNeedsAndWork → JobGiver_GetFood on every
/// pawn in a lord with that duty. Non-humanlike Necrons (Spyder, scarabs, and any Necron
/// race with foodType=None) have null food needs, which causes a NullReferenceException
/// inside GetFood that propagates through ThinkNode_Subtree → ThinkNode_Priority.
/// Returning null here is the correct early-exit: the pawn simply doesn't eat.
/// </summary>
[HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
static class HarmonyPatch_GetFood_NullFoodNeed
{
    static bool Prefix(Pawn pawn, ref Job __result)
    {
        if (pawn.needs?.food == null)
        {
            __result = null;
            return false;
        }
        return true;
    }
}

/// <summary>
/// Strips headgear and face coverings from Necron humanlike pawns after generation.
/// The ideology-desire system (ideoDesireAllowedFactionCategoryTags) bypasses
/// apparelAllowHeadgearChance=0, forcing visage masks and gas masks onto Necrons.
/// Necrons are living metal constructs — they don't wear face or head gear.
/// Covers UpperHead/FullHead (helmets, masks) and Eyes (gas masks, goggles).
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
    new[] { typeof(PawnGenerationRequest) })]
static class HarmonyPatch_NecronStripHeadgear
{
    static void Postfix(Pawn __result)
    {
        if (__result == null) return;
        if (!NechEnergyUtility.IsNecronPawn(__result)) return;
        if (!__result.RaceProps.Humanlike) return;
        if (__result.apparel == null) return;

        var toRemove = __result.apparel.WornApparel
            .Where(a => IsHeadOrFaceGear(a.def))
            .ToList();

        foreach (var item in toRemove)
        {
            __result.apparel.GetDirectlyHeldThings().Remove(item);
            if (!item.Destroyed) item.Destroy();
        }
    }

    private static bool IsHeadOrFaceGear(ThingDef def)
    {
        var apparel = def?.apparel;
        if (apparel == null) return false;
        if (apparel.bodyPartGroups?.Any(g =>
            g.defName == "UpperHead" ||
            g.defName == "FullHead"  ||
            g.defName == "Eyes") == true)
            return true;
        // Also catch items on the Overhead layer (hats, hoods, helmets).
        return apparel.layers?.Any(l => l.defName == "Overhead") == true;
    }
}

/// <summary>
/// Prevents the vanilla Siege strategy from being selected for the Necron faction.
/// Necrons have their own custom NecronSiege strategy; vanilla Siege (mortar-based) is
/// incompatible with them and would produce broken behaviour.
/// </summary>
[HarmonyPatch(typeof(RaidStrategyWorker_Siege), nameof(RaidStrategyWorker_Siege.CanUseWith))]
static class HarmonyPatch_BlockVanillaSiegeForNecrons
{
    static bool Prefix(IncidentParms parms, ref bool __result)
    {
        if (parms?.faction?.def?.defName == "UD_NecronFaction")
        {
            __result = false;
            return false;
        }
        return true;
    }
}

/// <summary>
/// Enforces Necron hierarchy rules after a raid pawn group is generated:
///   Lychguard (both kinds) require an Overlord.
///   Cryptothrall and Spyder require a Cryptek.
/// If a follower is present but its leader is absent, the cheapest follower is
/// replaced with a freshly generated leader pawn.
/// </summary>
[HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
static class HarmonyPatch_NecronGroupHierarchy
{
    private const string NecronFactionDefName  = "UD_NecronFaction";
    private const string ScarabKindDefName     = "GW_UL_ScarabSwarm";
    private const string CryptekKindDefName    = "UD_NecronCryptek";
    private const string SpyderKindDefName     = "UD_Necron_CanoptekSpyder";
    private const int    ScarabsPerCryptek     = 4;
    private const int    ScarabsPerSpyder      = 6;

    // (follower kindDef, required leader kindDef)
    private static readonly (string Follower, string Leader)[] Rules =
    {
        ("UD_NecronLychguard",                  "UD_NecronOverlord"),
        ("UD_NecronLychguard_2",                "UD_NecronOverlord"),
        ("GW_UL_CryptothrallPawnKind_Colonist", CryptekKindDefName),
        (SpyderKindDefName,                     CryptekKindDefName),
    };

    static void Postfix(ref IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
    {
        if (__result == null) return;
        if (parms?.faction?.def?.defName != NecronFactionDefName) return;

        List<Pawn> pawns = __result.ToList();
        if (pawns.Count == 0) return;

        // ── Hierarchy: ensure every follower type has its required leader ──
        foreach (var (followerKind, leaderKind) in Rules)
        {
            if (!pawns.Any(p => p.kindDef?.defName == followerKind)) continue;
            if (pawns.Any(p => p.kindDef?.defName == leaderKind)) continue;

            Pawn toSwap = pawns
                .Where(p => p.kindDef?.defName == followerKind)
                .OrderBy(p => p.kindDef?.combatPower ?? 0f)
                .FirstOrDefault();
            if (toSwap == null) continue;

            PawnKindDef leaderDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(leaderKind);
            if (leaderDef == null) continue;

            Pawn leader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                leaderDef, parms.faction,
                PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f));

            if (leader == null) continue;

            pawns.Remove(toSwap);
            if (!toSwap.Destroyed) toSwap.Destroy();
            pawns.Add(leader);
        }

        // ── Scarab minimums: 4 per Cryptek, 6 per Spyder ──
        int crypteks = pawns.Count(p => p.kindDef?.defName == CryptekKindDefName);
        int spyders  = pawns.Count(p => p.kindDef?.defName == SpyderKindDefName);
        int required = crypteks * ScarabsPerCryptek + spyders * ScarabsPerSpyder;
        int existing = pawns.Count(p => p.kindDef?.defName == ScarabKindDefName);
        int toAdd    = required - existing;

        if (toAdd > 0)
        {
            PawnKindDef scarabDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(ScarabKindDefName);
            if (scarabDef != null)
            {
                for (int i = 0; i < toAdd; i++)
                {
                    Pawn scarab = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        scarabDef, parms.faction,
                        PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f));
                    if (scarab != null)
                        pawns.Add(scarab);
                }
            }
        }

        __result = pawns;
    }
}
