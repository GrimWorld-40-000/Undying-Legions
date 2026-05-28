using RimWorld;
using Verse;

namespace GW40K_Necrons;

[DefOf]
public static class NechWorkModeDefOf
{
    public static NechWorkModeDef GW40K_NechMode_Patrol;
    public static NechWorkModeDef GW40K_NechMode_Escort;
    public static NechWorkModeDef GW40K_NechMode_Maintain;
    public static NechWorkModeDef GW40K_NechMode_Work;
    public static NechWorkModeDef GW40K_NechMode_Hold;

    static NechWorkModeDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NechWorkModeDefOf));
}
