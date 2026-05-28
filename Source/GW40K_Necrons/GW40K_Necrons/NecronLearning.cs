using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Learning-helper (ConceptDef) triggers for Undying Legions Necron systems.</summary>
internal static class NecronLearning
{
    internal static void OnInspectNechConstruct(Pawn pawn)
    {
        if (pawn?.needs == null)
            return;

        Demonstrate(NecronDefOfs.GW_UD_Concept_NecronOverview);

        if (pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux) != null)
            Demonstrate(NecronDefOfs.GW_UD_Concept_CoreFluxStasis);

        NeedDef necroDef = DefDatabase<NeedDef>.GetNamedSilentFail("GW_UD_Necrodermis");
        if (necroDef != null && pawn.needs.TryGetNeed(necroDef) != null)
            Demonstrate(NecronDefOfs.GW_UD_Concept_Necrodermis);

        if (HediffComp_NecronCommandTracker.GetCommanderOf(pawn) != null
            || !NechInspectStringUtility.IsNechProperlyCommanded(pawn))
            Demonstrate(NecronDefOfs.GW_UD_Concept_CommandProtocol);
    }

    /// <summary>Humanlike Necron xenotypes that use core flux but do not use the Nech inspect rewrite.</summary>
    internal static void OnInspectHumanlikeNecron(Pawn pawn)
    {
        if (pawn?.needs == null)
            return;

        Demonstrate(NecronDefOfs.GW_UD_Concept_NecronOverview);

        if (pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux) != null)
            Demonstrate(NecronDefOfs.GW_UD_Concept_CoreFluxStasis);

        NeedDef necroDef = DefDatabase<NeedDef>.GetNamedSilentFail("GW_UD_Necrodermis");
        if (necroDef != null && pawn.needs.TryGetNeed(necroDef) != null)
            Demonstrate(NecronDefOfs.GW_UD_Concept_Necrodermis);
    }

    private static void Demonstrate(ConceptDef def)
    {
        if (def == null)
            return;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(def, KnowledgeAmount.FrameDisplayed);
    }
}
