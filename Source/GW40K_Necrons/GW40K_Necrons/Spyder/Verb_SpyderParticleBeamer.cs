using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Integrated particle beamer for the Canoptek Spyder.
///
/// Auto ON  → beamer is fully available: right-click attack uses it, AI auto-attack uses it.
/// Auto OFF → beamer is completely unavailable: right-click shows no ranged option so the
///            float menu defaults to melee; AI auto-attack also disabled.
///            The gizmo still works because it calls TryStartCastOn directly,
///            which does not re-check Available().
///
/// Siege mode → beamer always unavailable (siege cannon replaces it).
/// </summary>
public class Verb_SpyderParticleBeamer : Verb_Shoot
{
    public override bool Available()
    {
        if (!base.Available()) return false;

        Pawn pawn = CasterPawn;
        if (pawn == null) return true;

        // Siege mode: long-range cannon replaces this verb.
        if (HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) return false;

        var comp = pawn.TryGetComp<Comp_SpyderAutoAttack>();
        // Auto OFF → unavailable to the right-click float menu and AI verb selection.
        // The beamer gizmo bypasses this by calling BeginTargeting directly (see Command_SpyderAutoAttack).
        if (comp != null && !comp.autoAttackEnabled) return false;

        return true;
    }
}
