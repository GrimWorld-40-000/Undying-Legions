using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class GeneExtension_Necron : DefModExtension
{
	public bool canBeWornByNecron = false;

	public BodyPartDef part;

	public GeneDef requiredGene;

	public HediffDef hediffDef;

	public float severityOnAdd = 0.01f;

	public List<ThingDef> allowedApparels = new List<ThingDef>();

	public List<NeedDef> needDefs = new List<NeedDef>();

	public bool isGiveBuffIfAnyPassThreshold = true;

	public float threshold = 0f;
}
