using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Vanilla <see cref="PawnApparelGenerator.GenerateStartingApparelFor"/> can skip equipping <see cref="PawnKindDef.apparelRequired"/>
/// in some cases. Postfix fills gaps. Patches are applied from <see cref="StaticConstructorOnStartup"/> — not via
/// <c>PatchAll()</c> — because touching <see cref="PawnApparelGenerator"/> during mod construction runs its static
/// ctor before <c>ThingStuffPair</c> is ready and crashes startup.
/// </summary>
[StaticConstructorOnStartup]
internal static class HarmonyPatch_GuaranteeApparelRequired
{
    static HarmonyPatch_GuaranteeApparelRequired()
    {
        new Harmony("GW40K_Necrons.GuaranteeApparelRequired").Patch(
            AccessTools.Method(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.GenerateStartingApparelFor)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatch_GuaranteeApparelRequired), nameof(Postfix))));
    }

    private static void Postfix(Pawn pawn)
    {
        if (pawn?.kindDef?.apparelRequired == null || pawn.apparel == null)
            return;
        if (!pawn.RaceProps.Humanlike)
            return;

        foreach (ThingDef req in pawn.kindDef.apparelRequired)
        {
            if (req == null || !req.IsApparel)
                continue;
            if (pawn.apparel.WornApparel.Any(a => a.def == req))
                continue;

            ThingDef stuff = null;
            if (req.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(req);
                if (stuff == null)
                    stuff = GenStuff.RandomStuffByCommonalityFor(req);
            }

            if (req.MadeFromStuff && stuff == null)
                continue;

            Apparel apparel = (Apparel)ThingMaker.MakeThing(req, stuff);
            PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
            pawn.apparel.Wear(apparel, false);
        }
    }
}
