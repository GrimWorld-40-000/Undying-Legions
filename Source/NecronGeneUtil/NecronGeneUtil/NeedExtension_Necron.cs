using Verse;

namespace NecronGeneUtil;

public class NeedExtension_Necron : DefModExtension
{
	public float fallPerTick;

	/// <summary>Body degradation hediff (GW_UD_NecrodermisMaintenanceDeficit) while necrodermis need is empty; severity scales extra <see cref="Need_Necrodermis"/> drain.</summary>
	public HediffDef maintenanceDeficitHediffDef;

	public float severityPerInterval;

	/// <summary>Extra necrodermis loss per hediff severity: fall multiplier = 1 + this × clamp01(severity). ~0.55 matches old hungerRateFactorOffset scale.</summary>
	public float extraNecrodermisFallPerSeverity = 0.55f;
}
