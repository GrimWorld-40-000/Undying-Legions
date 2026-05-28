namespace GW40K_Necrons;

#nullable disable

/// <summary>
/// Session clipboard for <see cref="ThingComp_CanoptekRepairPolicy"/> between constructs.
/// Mirrors the pattern used by <see cref="CanoptekConsumePolicyClipboard"/> for the consume filter.
/// </summary>
public static class CanoptekRepairPolicyClipboard
{
    private static bool   hasData;
    private static string sourceLabel;
    private static bool   allowSelf;
    private static bool   allowFriendlyNecrons;
    private static bool   allowFriendlyMechs;
    private static bool   allowNecronStructures;
    private static bool   allowStructures;

    public static bool   HasData     => hasData;
    public static string SourceLabel => sourceLabel ?? string.Empty;

    /// <summary>Copy all five policy flags from <paramref name="source"/> into the clipboard.</summary>
    public static void CopyFrom(ThingComp_CanoptekRepairPolicy source, string label)
    {
        if (source == null) return;
        hasData               = true;
        sourceLabel           = label ?? string.Empty;
        allowSelf             = source.allowSelf;
        allowFriendlyNecrons  = source.allowFriendlyNecrons;
        allowFriendlyMechs    = source.allowFriendlyMechs;
        allowNecronStructures = source.allowNecronStructures;
        allowStructures       = source.allowStructures;
    }

    /// <summary>Paste all five policy flags into <paramref name="target"/>. Returns false if the clipboard is empty.</summary>
    public static bool TryPasteTo(ThingComp_CanoptekRepairPolicy target)
    {
        if (!hasData || target == null) return false;
        target.allowSelf             = allowSelf;
        target.allowFriendlyNecrons  = allowFriendlyNecrons;
        target.allowFriendlyMechs    = allowFriendlyMechs;
        target.allowNecronStructures = allowNecronStructures;
        target.allowStructures       = allowStructures;
        return true;
    }
}
