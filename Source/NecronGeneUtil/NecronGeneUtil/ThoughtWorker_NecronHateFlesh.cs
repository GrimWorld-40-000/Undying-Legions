using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class ThoughtWorker_NecronHateFlesh : ThoughtWorker
{
	public ThoughtExtension_NecronHateFlesh modExtension => ((Def)base.def).GetModExtension<ThoughtExtension_NecronHateFlesh>();

	protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		if (!otherPawn.RaceProps.Humanlike)
		{
			return ThoughtState.Inactive;
		}
		bool flag = p.genes.HasActiveGene(FMJ_DefOf.GW_UD_Hatred) && p.genes.HasActiveGene(modExtension.requireGeneDef);
		bool flag2 = otherPawn.genes.HasActiveGene(FMJ_DefOf.GW_UD_Hatred) && otherPawn.genes.HasActiveGene(modExtension.requireGeneDef);
		if (flag)
		{
			if (!flag2)
			{
				return ThoughtState.ActiveAtStage(modExtension.stateNotMatch);
			}
			return ThoughtState.ActiveAtStage(modExtension.stateMatch);
		}
		return ThoughtState.Inactive;
	}
}
