using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Mark a pawn race (ThingDef) as a Canoptek construct: scarab swarms, Spyders, and similar.
/// Used for Control Node targets, material necrodermis metabolism, and related gating.
/// </summary>
public class RaceExtension_Canoptek : DefModExtension
{
    public bool isCanoptek;
}
