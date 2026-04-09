using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public static class NecronStasisUtility
{
    /// <summary>Effective stasis length = <see cref="BaseStasisHours"/> × this factor (per race).</summary>
    public static float GetEternalSlumberLengthFactor(ThingDef raceDef)
    {
        if (raceDef == null)
            return 1f;
        NonOrganicPawn neo = raceDef.GetModExtension<NonOrganicPawn>();
        if (neo != null)
        {
            float s = neo.coreSize > 0f ? neo.coreSize : neo.eternalSlumberLengthFactor;
            return Mathf.Max(0.01f, s);
        }
        NecronMechExtension mech = raceDef.GetModExtension<NecronMechExtension>();
        if (mech != null)
        {
            float s = mech.coreSize > 0f ? mech.coreSize : mech.eternalSlumberLengthFactor;
            return Mathf.Max(0.01f, s);
        }
        return 1f;
    }

    public static float BaseStasisHours
    {
        get
        {
            NecronStasisSettingsDef def = NecronDefOfs.GW40K_NecronStasisSettings;
            if (def != null && def.baseStasisHours > 0f)
                return def.baseStasisHours;
            return 12f;
        }
    }

    /// <summary>Total crypt cycle ticks: (base hours × race factor) + injury-based extra hours, scaled to <see cref="GenDate.TicksPerDay"/>.</summary>
    public static int StasisCycleTicksFor(Pawn pawn)
    {
        float hours = BaseStasisHours * GetEternalSlumberLengthFactor(pawn?.def);
        if (pawn != null)
            hours += NecronStasisHealing.ExtraStasisHoursFromInjuries(pawn);
        return Mathf.Max(1, Mathf.RoundToInt(GenDate.TicksPerDay * (hours / 24f)));
    }
}
