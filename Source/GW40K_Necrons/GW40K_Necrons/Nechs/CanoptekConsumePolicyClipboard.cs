using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Session clipboard for <see cref="ThingComp_CanoptekConsumePolicy.consumeFilter"/> between scarabs.</summary>
public static class CanoptekConsumePolicyClipboard
{
    private static ThingFilter clipboard;
    private static string sourceLabel;

    public static bool HasData => clipboard != null;

    public static string SourceLabel => sourceLabel ?? string.Empty;

    public static void CopyFrom(ThingFilter source, string label)
    {
        if (source == null)
            return;
        clipboard ??= new ThingFilter();
        clipboard.ResolveReferences();
        clipboard.CopyAllowancesFrom(source);
        sourceLabel = label ?? string.Empty;
    }

    public static bool TryPasteTo(ThingFilter target)
    {
        if (clipboard == null || target == null)
            return false;
        target.CopyAllowancesFrom(clipboard);
        return true;
    }
}
