using System;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Load gizmo textures on RimWorld's static startup pass — do NOT combine this with Harmony patch static
/// init on the same type (lifecycle order can call ContentFinder before content is safe and hard-CTD).
/// </summary>
[StaticConstructorOnStartup]
internal static class NechGizmoAssetBootstrap
{
    internal static Texture2D SpyderRangedAttackFallbackIcon;
    private const int SpyderRangedAttackIconWarnKey = 79354102;

    static NechGizmoAssetBootstrap()
    {
        try
        {
            SpyderRangedAttackFallbackIcon = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K_SpyderAttack", false);
        }
        catch (Exception ex)
        {
            Log.WarningOnce(
                $"[Undying-Legions] Failed to load GW40K_SpyderAttack for Nech gizmos — using vanilla icon. {ex}",
                SpyderRangedAttackIconWarnKey);
        }
    }
}
