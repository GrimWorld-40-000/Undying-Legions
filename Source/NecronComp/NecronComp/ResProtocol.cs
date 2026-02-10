using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons
{
    public class NonOrganicPawn : DefModExtension
    {
        public bool comfortNeed = false;
        public bool socialNeed = false;
        public string passion;
        public Passion passionType;
    }

    public class GeneExtension_NonOrganic : DefModExtension
    {
        public bool makeNonOrganic = true;
    }

    public class Gene_ResurrectionProtocol : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
        }

        public static bool PawnHasResurrectionProtocol(Pawn pawn)
        {
            return pawn.genes?.GetGene(NecronDefOfs.GW_UD_ResurrectionProtocol)
                   is Gene_ResurrectionProtocol;
        }
    }
}
