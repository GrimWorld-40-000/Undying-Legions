using System.Collections.Generic;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Per-race list of available work modes and the default mode.
/// Added to the ThingDef for each commandable Necron race.
/// </summary>
public class CompProperties_NechWorkMode : CompProperties
{
    /// <summary>All modes this pawn type can be set to.</summary>
    public List<NechWorkModeDef> availableModes = new List<NechWorkModeDef>();

    /// <summary>Mode applied on first spawn / after load if no saved mode exists.</summary>
    public NechWorkModeDef defaultMode;

    public CompProperties_NechWorkMode()
    {
        compClass = typeof(ThingComp_NechWorkMode);
    }
}
