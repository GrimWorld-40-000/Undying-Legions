using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Per–Canoptek construct toggles for autonomous Repair-mode targets (think tree job giver).
/// </summary>
public class ThingComp_CanoptekRepairPolicy : ThingComp
{
    public bool allowSelf = true;
    public bool allowFriendlyNecrons = true;
    public bool allowFriendlyMechs = true;
    public bool allowNecronStructures = true;
    public bool allowStructures = true;

    public static ThingComp_CanoptekRepairPolicy Get(Pawn pawn) =>
        pawn?.TryGetComp<ThingComp_CanoptekRepairPolicy>();

    /// <summary>When no comp is present, all categories are allowed (backward compatible).</summary>
    public static bool AllowSelf(Pawn pawn) => Get(pawn)?.allowSelf ?? true;

    public static bool AllowFriendlyNecrons(Pawn pawn) => Get(pawn)?.allowFriendlyNecrons ?? true;

    public static bool AllowFriendlyMechs(Pawn pawn) => Get(pawn)?.allowFriendlyMechs ?? true;

    public static bool AllowNecronStructures(Pawn pawn) => Get(pawn)?.allowNecronStructures ?? true;

    public static bool AllowStructures(Pawn pawn) => Get(pawn)?.allowStructures ?? true;

    public void SetAll(bool value)
    {
        allowSelf = value;
        allowFriendlyNecrons = value;
        allowFriendlyMechs = value;
        allowNecronStructures = value;
        allowStructures = value;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref allowSelf, "allowSelf", true);
        Scribe_Values.Look(ref allowFriendlyNecrons, "allowFriendlyNecrons", true);
        Scribe_Values.Look(ref allowFriendlyMechs, "allowFriendlyMechs", true);
        Scribe_Values.Look(ref allowNecronStructures, "allowNecronStructures", true);
        Scribe_Values.Look(ref allowStructures, "allowStructures", true);
    }
}
