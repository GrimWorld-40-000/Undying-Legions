using System.Linq;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

internal static class NechCommandHotkeys
{
    /// <summary>Vanilla draft toggle binding; defName varies by version.</summary>
    internal static KeyBindingDef DraftToggle()
    {
        foreach (string name in new[] { "Command_ToggleDraft", "CommandToggleDraft", "ToggleDraft", "Draft" })
        {
            KeyBindingDef k = DefDatabase<KeyBindingDef>.GetNamedSilentFail(name);
            if (k != null)
                return k;
        }

        return DefDatabase<KeyBindingDef>.AllDefsListForReading
            .FirstOrDefault(d => d != null
                && d.category != null
                && d.defName != null
                && d.defName.IndexOf("Draft", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
