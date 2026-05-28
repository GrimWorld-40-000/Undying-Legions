using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Must match <c>PawnRenderTreeDef_Scarab.xml</c> swarm body nodes.</summary>
/// <summary>Who may open the scarab palette and when rendering uses faction tint instead of saved primary.</summary>
internal static class ScarabPaintUtility
{
    /// <summary>Colony scarabs that are not hostile to the player (excludes rogue / broken same-faction).</summary>
    internal static bool PlayerMayConfigurePaint(Pawn pawn)
    {
        if (pawn == null || pawn.Dead)
            return false;
        if (pawn.Faction != Faction.OfPlayer)
            return false;
        if (pawn.HostileTo(Faction.OfPlayer))
            return false;
        return true;
    }

    /// <summary>Enemy / neutral / hostile-to-player: carapace mask follows <see cref="Faction.Color"/>.</summary>
    internal static bool UseFactionPrimaryTint(Pawn pawn) =>
        pawn?.Faction != null
        && (pawn.Faction != Faction.OfPlayer || pawn.HostileTo(Faction.OfPlayer));
}

public static class ScarabSwarmPaintDefs
{
    public const string TexPath = "GW40K/Scarab/GW40k_Scarab";
    public static readonly Vector2 DrawSize = new Vector2(1.6f, 1.6f);
    private static readonly string[] BodyPartSlotOrder = { "GW40K_ScarabA", "GW40K_ScarabB", "GW40K_ScarabC", "GW40K_ScarabD" };

    public static int SlotForLinkedGroup(string defName)
    {
        if (defName.NullOrEmpty())
            return -1;
        for (int i = 0; i < BodyPartSlotOrder.Length; i++)
        {
            if (BodyPartSlotOrder[i] == defName)
                return i;
        }

        return -1;
    }
}

public class CompProperties_ScarabPaint : CompProperties
{
    /// <summary>Carapace / primary mask (CutoutComplex red channel).</summary>
    public Color primaryDefault = new Color(0.24f, 0.82f, 0.30f, 1f);
    /// <summary>Head / secondary mask (CutoutComplex green channel).</summary>
    public Color secondaryDefault = Color.white;

    public CompProperties_ScarabPaint()
    {
        compClass = typeof(CompScarabPaint);
    }
}

public class CompScarabPaint : ThingComp
{
    private Color primary;
    private Color secondary;
    private bool initialized;
    private bool needsRefresh;
    /// <summary>
    /// Four separate <see cref="Graphic_ScarabDualMask"/> instances (one per spastic body node). Worker-thread
    /// render must not call <see cref="ContentFinder{T}"/> / <c>Init</c>; those paths reuse this cache.
    /// </summary>
    private Graphic[] swarmPaintGraphics;
    private Color swarmPaintGraphicsPrimary;
    private Color swarmPaintGraphicsSecondary;
    private Vector2 swarmPaintGraphicsDrawSize = Vector2.zero;
    private string swarmPaintGraphicsTexPath;
    private bool swarmPaintGraphicsBuilt;
    /// <summary>
    /// Render-tree init can resolve scarab node graphics on a worker thread; we fall back to vanilla
    /// graphics then and must force a main-thread rebuild.
    /// </summary>
    private bool deferredPaintGraphicFromWorkerThread;
    /// <summary>Apply <see cref="SetSecondaryToCommanderFavorite"/> on next <see cref="CompTick"/> to avoid graphic/renderer reentrancy during jobs (CTD reports on bind).</summary>
    private Pawn pendingCommanderFavoriteTint;

    public CompProperties_ScarabPaint Props => (CompProperties_ScarabPaint)props;
    public Color Primary => primary;
    public Color Secondary => secondary;

    /// <summary>Mask red channel: saved paint for friendly colony; <see cref="Faction.Color"/> for others.</summary>
    public Color EffectivePrimaryForRendering()
    {
        if (parent is not Pawn p)
            return primary;
        if (ScarabPaintUtility.UseFactionPrimaryTint(p))
            return p.Faction.Color;
        return primary;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref primary, "primary", Props.primaryDefault);
        Scribe_Values.Look(ref secondary, "secondary", Props.secondaryDefault);
        Scribe_Values.Look(ref initialized, "initialized", false);
        Scribe_Values.Look(ref needsRefresh, "needsRefresh", false);
        Scribe_Values.Look(ref deferredPaintGraphicFromWorkerThread, "deferredPaintGraphicFromWorkerThread", false);
        InvalidateSwarmPaintGraphics();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        // Only apply defaults on first real spawn. On load, PostExposeData already restored colors — do not wipe them
        // when older saves had initialized=false or missing keys.
        if (!initialized && !respawningAfterLoad)
        {
            primary = Props.primaryDefault;
            secondary = Props.secondaryDefault;
        }

        initialized = true;
        needsRefresh = true;

        if (parent is Pawn p0 && p0.def?.defName == "GW40K_ScarabSwarm" && ScarabPaint_MainThreadBootstrap.CanLoadGraphics)
            RebuildSwarmPaintGraphicsIfNeeded(ScarabSwarmPaintDefs.TexPath, ScarabSwarmPaintDefs.DrawSize, force: true);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        swarmPaintGraphics = null;
        swarmPaintGraphicsBuilt = false;
        base.PostDestroy(mode, previousMap);
    }

    public override void CompTick()
    {
        base.CompTick();
        if (parent is not Pawn p || !p.Spawned)
            return;

        if (pendingCommanderFavoriteTint != null)
        {
            Pawn cmd = pendingCommanderFavoriteTint;
            pendingCommanderFavoriteTint = null;
            try
            {
                SetSecondaryToCommanderFavorite(cmd);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Undying-Legions] Deferred commander favorite tint failed: {ex}");
            }
        }

        // Worker-thread render init: rebuild tinted graphic on the next main-thread ticks ASAP.
        if (deferredPaintGraphicFromWorkerThread)
        {
            deferredPaintGraphicFromWorkerThread = false;
            needsRefresh = true;
            TryApplyRefresh(p);
            return;
        }

        if (needsRefresh && p.IsHashIntervalTick(15))
            TryApplyRefresh(p);
    }

    /// <summary>Called when scarab paint graphic construction was skipped (non-main thread).</summary>
    public void NotifyPaintGraphicDeferredFromWorkerThread()
    {
        deferredPaintGraphicFromWorkerThread = true;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent is not Pawn pawn || !ScarabPaintUtility.PlayerMayConfigurePaint(pawn))
            yield break;

        yield return new Command_Action
        {
            defaultLabel = "Choose construct color",
            defaultDesc = "Primary: carapace (mask red). Secondary: head (mask green).",
            icon = ResolvePaintIcon(),
            action = () =>
            {
                if (!ScarabPaintUtility.PlayerMayConfigurePaint(pawn))
                    return;

                var comps = new List<CompScarabPaint>();
                foreach (object sel in Find.Selector.SelectedObjects)
                {
                    if (sel is Pawn p && ScarabPaintUtility.PlayerMayConfigurePaint(p))
                    {
                        CompScarabPaint c = p.TryGetComp<CompScarabPaint>();
                        if (c != null && !comps.Contains(c))
                            comps.Add(c);
                    }
                }
                if (!comps.Contains(this))
                    comps.Insert(0, this);

                Find.WindowStack.Add(new Window_ScarabColorPicker(comps));
            }
        };
    }

    public void SetPrimary(Color c)
    {
        primary = c;
        InvalidateSwarmPaintGraphics();
        QueueRefresh();
    }

    public void SetSecondary(Color c)
    {
        secondary = c;
        InvalidateSwarmPaintGraphics();
        QueueRefresh();
    }

    /// <summary>Schedules favorite tint on the next tick (safe from job/bind stack).</summary>
    public void QueueCommanderFavoriteTint(Pawn commander)
    {
        pendingCommanderFavoriteTint = commander;
    }

    public void SetSecondaryToCommanderFavorite(Pawn commander)
    {
        if (commander == null)
            return;
        Color color;
        try
        {
            if (!TryGetCommanderFavoriteColor(commander, out color))
                return;
        }
        catch (Exception ex)
        {
            Log.Warning($"[Undying-Legions] Failed to read commander favorite color for scarab bind: {ex}");
            return;
        }
        if (!IsFiniteColor(color))
            return;
        secondary = color;
        InvalidateSwarmPaintGraphics();
        QueueRefresh();
    }

    private static bool IsFiniteColor(Color c) =>
        !(float.IsNaN(c.r) || float.IsInfinity(c.r)
            || float.IsNaN(c.g) || float.IsInfinity(c.g)
            || float.IsNaN(c.b) || float.IsInfinity(c.b)
            || float.IsNaN(c.a) || float.IsInfinity(c.a));

    private static bool TryGetCommanderFavoriteColor(Pawn commander, out Color color)
    {
        color = default;
        object story = commander?.story;
        if (story == null)
            return false;

        // Vanilla: RimWorld.Pawn_StoryTracker.favoriteColor is a ColorDef, not UnityEngine.Color (Ideology-era).
        if (story is Pawn_StoryTracker storyTracker && storyTracker.favoriteColor != null)
        {
            color = storyTracker.favoriteColor.color;
            return true;
        }

        Type storyType = story.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] candidateNames = { "favoriteColor", "FavoriteColor", "_favoriteColor" };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            string name = candidateNames[i];

            FieldInfo field = storyType.GetField(name, Flags);
            if (field != null && TryReadColorValue(field.GetValue(story), field.FieldType, out color))
                return true;

            PropertyInfo prop = storyType.GetProperty(name, Flags);
            if (prop != null && prop.GetIndexParameters().Length == 0 && TryReadColorValue(prop.GetValue(story, null), prop.PropertyType, out color))
                return true;
        }

        // Harmony walks base types (matches vanilla Pawn_StoryTracker etc. across RW versions).
        FieldInfo accessField = AccessTools.Field(storyType, "favoriteColor");
        if (accessField != null && TryReadColorValue(accessField.GetValue(story), accessField.FieldType, out color))
            return true;

        // Any member whose name suggests "favorite" and yields a color (mods / renames).
        foreach (FieldInfo fi in AccessTools.GetDeclaredFields(storyType))
        {
            if (fi.Name.IndexOf("favorite", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!TryReadColorValue(fi.GetValue(story), fi.FieldType, out color))
                continue;
            return true;
        }

        foreach (PropertyInfo pi in AccessTools.GetDeclaredProperties(storyType))
        {
            if (pi.GetIndexParameters().Length > 0)
                continue;
            if (pi.Name.IndexOf("favorite", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!TryReadColorValue(pi.GetValue(story, null), pi.PropertyType, out color))
                continue;
            return true;
        }

        return false;
    }

    private static bool TryReadColorValue(object value, Type declaredType, out Color color)
    {
        color = default;
        if (value == null)
            return false;

        Type runtimeType = value.GetType();
        if (runtimeType.Name == "ColorDef")
        {
            FieldInfo cdf = runtimeType.GetField("color", BindingFlags.Instance | BindingFlags.Public);
            if (cdf != null && cdf.GetValue(value) is Color fromDef)
            {
                color = fromDef;
                return true;
            }
        }

        if (value is Color c)
        {
            color = c;
            return true;
        }
        if (value is Color32 c32)
        {
            color = c32;
            return true;
        }

        Type underlying = Nullable.GetUnderlyingType(declaredType);
        if (underlying == typeof(Color))
        {
            if (value is Color boxedColor)
            {
                color = boxedColor;
                return true;
            }

            // Nullable<Color> boxed as the nullable struct
            if (declaredType.IsValueType && value != null)
            {
                PropertyInfo hasValue = declaredType.GetProperty("HasValue", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo valueProp = declaredType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (hasValue != null && valueProp != null
                    && hasValue.GetValue(value) is bool hv && hv
                    && valueProp.GetValue(value) is Color nv)
                {
                    color = nv;
                    return true;
                }
            }
        }

        return false;
    }

    private void InvalidateSwarmPaintGraphics()
    {
        swarmPaintGraphicsBuilt = false;
    }

    /// <summary>Worker-thread safe when cache already built on the main thread.</summary>
    public Graphic GetCachedSwarmPaintGraphic(int slot)
    {
        if (slot < 0 || slot > 3 || swarmPaintGraphics == null)
            return null;
        return swarmPaintGraphics[slot];
    }

    /// <summary>Builds four graphics on the main thread only; no-op off main thread.</summary>
    public void RebuildSwarmPaintGraphicsIfNeeded(string texPath, Vector2 drawSize, bool force = false)
    {
        if (!ScarabPaint_MainThreadBootstrap.CanLoadGraphics)
            return;
        if (texPath.NullOrEmpty() || drawSize.x <= 0f || drawSize.y <= 0f)
            return;

        Color effPrimary = EffectivePrimaryForRendering();
        if (!force
            && swarmPaintGraphicsBuilt
            && swarmPaintGraphics != null
            && swarmPaintGraphics.Length == 4
            && swarmPaintGraphicsTexPath == texPath
            && swarmPaintGraphicsDrawSize == drawSize
            && swarmPaintGraphicsPrimary == effPrimary
            && swarmPaintGraphicsSecondary == secondary)
            return;

        if (swarmPaintGraphics == null || swarmPaintGraphics.Length != 4)
            swarmPaintGraphics = new Graphic[4];

        GraphicRequest req = default;
        req.graphicClass = typeof(Graphic_ScarabDualMask);
        req.path = texPath;
        req.shader = ShaderDatabase.CutoutComplex;
        req.drawSize = drawSize;
        req.color = effPrimary;
        req.colorTwo = secondary;

        for (int i = 0; i < 4; i++)
        {
            var g = (Graphic)Activator.CreateInstance(typeof(Graphic_ScarabDualMask));
            g.Init(req);
            swarmPaintGraphics[i] = g;
        }

        swarmPaintGraphicsPrimary = effPrimary;
        swarmPaintGraphicsSecondary = secondary;
        swarmPaintGraphicsDrawSize = drawSize;
        swarmPaintGraphicsTexPath = texPath;
        swarmPaintGraphicsBuilt = true;
    }

    private void QueueRefresh()
    {
        needsRefresh = true;
        if (parent is Pawn p)
            TryApplyRefresh(p);
    }

    private void TryApplyRefresh(Pawn p)
    {
        if (p == null || !p.Spawned || Current.ProgramState != ProgramState.Playing)
            return;
        try
        {
            parent.Notify_ColorChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            if (p.def?.defName == "GW40K_ScarabSwarm" && ScarabPaint_MainThreadBootstrap.CanLoadGraphics)
                RebuildSwarmPaintGraphicsIfNeeded(ScarabSwarmPaintDefs.TexPath, ScarabSwarmPaintDefs.DrawSize);
            // Scarab render-tree init can cache worker-thread graphics; queue one more dirty pass next frame.
            if (p.def?.defName == "GW40K_ScarabSwarm")
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    if (p.Spawned && !p.Destroyed)
                        p.Drawer?.renderer?.SetAllGraphicsDirty();
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[Undying-Legions] Scarab paint refresh failed: {ex}");
        }

        needsRefresh = false;
    }

    private static Texture2D ResolvePaintIcon()
    {
        Texture2D fallback = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K_SpyderAttack", false) ?? TexCommand.Attack;
        try
        {
            // GrimWorld framework icon: GW4KArmor.PaintContent.PaintIcon
            var t = System.Type.GetType("GW4KArmor.PaintContent, GW4KArmor", throwOnError: false);
            var field = t?.GetField("PaintIcon", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field?.GetValue(null) is Texture2D tex && tex != null)
                return tex;
        }
        catch { }
        return fallback;
    }
}
