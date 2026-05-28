using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Assigns names from the Necron name pool to any humanlike pawn with the NonOrganicPawn
/// extension (Warriors, Crypteks, Overlords, Lychguard, Flayed One, etc.).
/// Human pawns brought under Nech control keep their organic names — they lack NonOrganicPawn.
/// Runs as a postfix so backstory assignment in GiveAppropriateBioAndNameTo is unaffected.
/// </summary>
[HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveAppropriateBioAndNameTo")]
public static class HarmonyPatch_NecronNames
{
    private static readonly string[] Names =
    {
        "Nubkheptah",   "Ptolenakht",   "Inakhotep",    "Imhothes",
        "Hedjemes",     "Mereru",       "Asharu",       "Ahhokauhor",
        "Nephethor",    "Dakhakare",    "Inetkaskhet",  "Anhomhat",
        "Amenetep",     "Senseseneb",   "Merifret",     "Henumka",
        "Hedjemhat",    "Hakonut",      "Khetamen",     "Ankhesedes",
        "Khethor",      "Udjeru",       "Nephendes",    "Nebemenes",
        "Gilutep",      "Inadjem",      "Khemka",       "Mentumenre",
        "Djedesankh",   "Hewerenef",    "Anhoskhet",    "Nakhtotankh",
        "Magatep",      "Meresehti",    "Hotenza",      "Nebudes",
        "Minmorenef",   "Ptolethap",    "Hewenamun",    "Menkhenru",
        "Peneclid",     "Amenhopses",   "Merekht",      "Petuwa",
        "Aahondes",     "Rahemes",      "Ahatep",       "Mekemun",
        "Ibinhor",      "Ptahmomnisu",  "Deduros",      "Simurenef",
        "Nakhtoseneb",  "Berebiankh",   "Nakhtothap",   "Prehonebu",
        "Ramenut",      "Heweru",       "Harsionhotep", "Amenhoseneb",
        "Duaemopet",    "Harsiopses",   "Karotka",      "Hekenutekh",
        "Magapses",     "Rahot",        "Tadumaka",     "Nefeclea",
        "Akhefer",
    };

    // Postfix so the original method runs first — it applies forcedHeadTypes from genes and sets
    // pawn.story.headType. A prefix that returns false skips that assignment, leaving headType null
    // and crashing PawnRenderNode_Beard on first draw.
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        if (pawn?.def?.GetModExtension<NonOrganicPawn>() == null) return;
        if (!pawn.RaceProps.Humanlike) return;

        string name = Names[Rand.Range(0, Names.Length)];
        pawn.Name = new NameTriple(name, name, "");

        if (IsLowborn(pawn))
            pawn.gender = Gender.None;
    }

    private static bool IsLowborn(Pawn pawn) =>
        pawn.genes?.GenesListForReading?.Any(g =>
            g?.def?.defName == "GW_UD_LowBorn" ||
            g?.def?.defName == "GW_UD_LowBorn_FlayedOne") == true;
}
