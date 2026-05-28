using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Prevents NameBank first-name lookup for genderless non-organic humanlikes.
/// NameBank's HumanStandard first-name lists are gendered (Male/Female), so Gender.None can throw.
/// </summary>
[HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GeneratePawnName),
    new[] { typeof(Pawn), typeof(NameStyle), typeof(string), typeof(bool), typeof(XenotypeDef) })]
public static class HarmonyPatch_FixNecronGenderlessNameGeneration
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

    [HarmonyPrefix]
    private static bool Prefix(Pawn pawn, ref Name __result)
    {
        if (pawn?.def?.GetModExtension<NonOrganicPawn>() == null) return true;
        if (!pawn.RaceProps.Humanlike) return true;
        if (pawn.gender != Gender.None) return true;

        string name = Names[Rand.Range(0, Names.Length)];
        __result = new NameTriple(name, name, string.Empty);
        return false;
    }
}
