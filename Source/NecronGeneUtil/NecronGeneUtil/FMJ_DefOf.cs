using RimWorld;
using Verse;

namespace NecronGeneUtil;

[DefOf]
internal static class FMJ_DefOf
{
	public static GeneDef GW_UD_ApparelRestriction = null;

	public static GeneDef GW_UD_LifeLess = null;

	public static GeneDef GW_UD_Hatred = null;

	// Fixed: was GW40k_Necron_Necrodemis (missing 'r') — caused null def at startup, AI feeding completely broken
	public static ThingDef GW40k_Necron_Necrodermis = null;

	public static ThingDef GW_NecrodermisInjector = null;

	public static TraitDef Transhumanist = null;
}
