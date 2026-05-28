using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// ContentFinder / GraphicDatabase must never run during worker-thread render-tree init —
/// RimWorld logs "Attempted to load a graphic off the main thread" and can crash otherwise.
/// This ID is sampled from RimWorld's main-thread bootstrap (see attribute).
/// </summary>
[StaticConstructorOnStartup]
internal static class ScarabPaint_MainThreadBootstrap
{
    internal static readonly int ManagedThreadId;

    static ScarabPaint_MainThreadBootstrap()
    {
        ManagedThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    internal static bool CanLoadGraphics =>
        Thread.CurrentThread.ManagedThreadId == ManagedThreadId;
}

/// <summary>
/// Dual-mask scarab paint. XML <c>workerClass</c> replaces the default Spastic worker on the node, so we cannot rely
/// on <c>PawnRenderNodeProperties_Spastic</c> ctor to supply <see cref="PawnRenderNodeWorker_Spastic"/> — we delegate
/// offset/rotation/scale to a vanilla instance (subclassing Spastic from a mod DLL caused CTDs for some players).
/// </summary>
public class PawnRenderNodeWorker_ScarabPaint : PawnRenderNodeWorker
{
    private static readonly PawnRenderNodeWorker_Spastic SpasticWorker = new PawnRenderNodeWorker_Spastic();

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot) =>
        SpasticWorker.OffsetFor(node, parms, out pivot);

    public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms) =>
        SpasticWorker.RotationFor(node, parms);

    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms) =>
        SpasticWorker.ScaleFor(node, parms);

    /// <summary>
    /// World draw path: base only sets <see cref="ShaderPropertyIDs.Color"/> on the MPB. CutoutComplex needs
    /// <see cref="ShaderPropertyIDs.ColorTwo"/> too or the second mask tint flickers / reads as white.
    /// </summary>
    public override void PreDraw(PawnRenderNode node, Material mat, PawnDrawParms parms)
    {
        base.PreDraw(node, mat, parms);
        if (mat == null || GetGraphic(node, parms) is not Graphic_ScarabDualMask)
            return;
        if (parms.Statue)
        {
            if (parms.statueColor.HasValue)
                node.MatPropBlock.SetColor(ShaderPropertyIDs.ColorTwo, parms.statueColor.Value);
        }
        else if (mat.HasProperty(ShaderPropertyIDs.ColorTwo))
            node.MatPropBlock.SetColor(ShaderPropertyIDs.ColorTwo, parms.tint * mat.GetColor(ShaderPropertyIDs.ColorTwo));
    }

    /// <summary>
    /// Base only sets <see cref="ShaderPropertyIDs.Color"/> on the MPB. CutoutComplex dual-mask also needs
    /// <see cref="ShaderPropertyIDs.ColorTwo"/> scaled by <see cref="PawnDrawParms.tint"/> or portrait breaks.
    /// </summary>
    public override MaterialPropertyBlock GetMaterialPropertyBlock(PawnRenderNode node, Material material, PawnDrawParms parms)
    {
        MaterialPropertyBlock block = base.GetMaterialPropertyBlock(node, material, parms);
        if (block == null || material == null)
            return block;
        if (GetGraphic(node, parms) is not Graphic_ScarabDualMask)
            return block;

        if (parms.Statue)
        {
            if (parms.statueColor.HasValue)
                block.SetColor(ShaderPropertyIDs.ColorTwo, parms.statueColor.Value);
        }
        else if (material.HasProperty(ShaderPropertyIDs.ColorTwo))
        {
            block.SetColor(ShaderPropertyIDs.ColorTwo, parms.tint * material.GetColor(ShaderPropertyIDs.ColorTwo));
        }

        return block;
    }

    protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
    {
        Pawn pawn = parms.pawn;
        if (pawn != null &&
            pawn.def?.defName == "GW40K_ScarabSwarm" &&
            !(node?.Props?.texPath).NullOrEmpty())
        {
            CompScarabPaint comp = pawn.TryGetComp<CompScarabPaint>();
            if (comp != null)
            {
                Vector2 drawSize = node.Props.drawSize;
                if (drawSize.x <= 0f || drawSize.y <= 0f)
                    drawSize = ScarabSwarmPaintDefs.DrawSize;

                string path = node.Props.texPath;
                int slot = ScarabSwarmPaintDefs.SlotForLinkedGroup(node?.Props?.linkedBodyPartsGroup?.defName);

                if (ScarabPaint_MainThreadBootstrap.CanLoadGraphics)
                    comp.RebuildSwarmPaintGraphicsIfNeeded(path, drawSize);

                if (!ScarabPaint_MainThreadBootstrap.CanLoadGraphics)
                {
                    comp.NotifyPaintGraphicDeferredFromWorkerThread();
                    if (slot >= 0)
                    {
                        Graphic cached = comp.GetCachedSwarmPaintGraphic(slot);
                        if (cached != null)
                            return cached;
                    }

                    return base.GetGraphic(node, parms);
                }

                if (slot >= 0)
                {
                    Graphic cached = comp.GetCachedSwarmPaintGraphic(slot);
                    if (cached != null)
                        return cached;
                }

                // Unknown body slot or cache miss: build one instance (must not share one Graphic across four nodes).
                GraphicRequest req = default;
                req.graphicClass = typeof(Graphic_ScarabDualMask);
                req.path = path;
                req.shader = ShaderDatabase.CutoutComplex;
                req.drawSize = drawSize;
                req.color = comp.EffectivePrimaryForRendering();
                req.colorTwo = comp.Secondary;
                var g = (Graphic)Activator.CreateInstance(typeof(Graphic_ScarabDualMask));
                g.Init(req);
                return g;
            }
        }

        return base.GetGraphic(node, parms);
    }
}

/// <summary>
/// Manual init same idea as GW4KArmor.Graphic_TriColorMask — do NOT call
/// <see cref="Graphic_Multi.Init(GraphicRequest)"/> (double-init / native draw issues).
/// <see cref="Graphic_Multi"/> keeps <c>mats</c> private; set via reflection.
/// </summary>
public class Graphic_ScarabDualMask : Graphic_Multi
{
    private static readonly FieldInfo MatsField = AccessTools.Field(typeof(Graphic_Multi), "mats");

    /// <summary>
    /// Dual-mask CutoutComplex: mask <b>red = primary (_Color)</b>, <b>green = secondary (_ColorTwo)</b> per wiki
    /// “Texture Masking”. With distinct N/E/S/W diffuse textures, vanilla <see cref="Graphic_Multi.ShouldDrawRotated"/>
    /// can be false, which breaks pawn rotation / tint consistency for this art set — force rotated draw.
    /// </summary>
    public override bool ShouldDrawRotated => true;

    public override void Init(GraphicRequest req)
    {
        data = req.graphicData;
        path = req.path;
        color = req.color;
        colorTwo = req.colorTwo;
        drawSize = req.drawSize;

        if (MatsField == null)
        {
            Log.ErrorOnce(
                "[Undying-Legions] Graphic_ScarabDualMask: reflection failed for Graphic_Multi.mats; falling back to vanilla Graphic_Multi init.",
                7349201);
            base.Init(req);
            return;
        }

        Material[] mats = MatsField.GetValue(this) as Material[];
        if (mats == null || mats.Length < 4)
        {
            mats = new Material[4];
            MatsField.SetValue(this, mats);
        }

        Texture2D[] diffuse = LoadDiffuseRotations(req.path);
        Texture2D[] mask = LoadMaskRotations(req.path);

        for (int i = 0; i < mats.Length; i++)
        {
            Texture2D diffuseTex = diffuse[i];
            Texture2D maskTex = mask[i];

            if (diffuseTex == null)
                diffuseTex = diffuse[0] ?? BaseContent.BadTex;

            if (maskTex == null)
                maskTex = diffuseTex;

            MaterialRequest matReq = default;
            matReq.mainTex = diffuseTex;
            matReq.shader = req.shader;
            matReq.color = color;
            matReq.colorTwo = colorTwo;
            matReq.maskTex = maskTex;
            matReq.shaderParameters = req.shaderParameters;
            matReq.renderQueue = req.renderQueue;
            mats[i] = MaterialPool.MatFrom(matReq);
            if (mats[i] != null)
                mats[i].hideFlags = HideFlags.DontUnloadUnusedAsset;
        }
    }

    private static Texture2D[] LoadDiffuseRotations(string basePath)
    {
        Texture2D[] tex = new Texture2D[4];
        tex[0] = ContentFinder<Texture2D>.Get(basePath + "_north", false);
        // Folder shipped as GW40k_Scarab_East.png on some installs; ContentFinder is case-sensitive on Linux.
        tex[1] = ContentFinder<Texture2D>.Get(basePath + "_east", false)
            ?? ContentFinder<Texture2D>.Get(basePath + "_East", false);
        tex[2] = ContentFinder<Texture2D>.Get(basePath + "_south", false);
        tex[3] = ContentFinder<Texture2D>.Get(basePath + "_west", false);

        if (tex[0] == null)
            tex[0] = ContentFinder<Texture2D>.Get(basePath, false);
        if (tex[2] == null)
            tex[2] = tex[0];
        if (tex[1] == null)
            tex[1] = tex[0];
        if (tex[3] == null)
            tex[3] = tex[1] ?? tex[0];

        return tex;
    }

    private static Texture2D[] LoadMaskRotations(string basePath)
    {
        Texture2D[] tex = new Texture2D[4];
        tex[0] = ContentFinder<Texture2D>.Get(basePath + "_northMask", false);
        tex[1] = ContentFinder<Texture2D>.Get(basePath + "_eastMask", false);
        tex[2] = ContentFinder<Texture2D>.Get(basePath + "_southMask", false);
        tex[3] = ContentFinder<Texture2D>.Get(basePath + "_westMask", false);

        if (tex[0] == null)
            tex[0] = tex[2] ?? tex[1] ?? tex[3];
        if (tex[2] == null)
            tex[2] = tex[0];
        if (tex[1] == null)
            tex[1] = tex[3] ?? tex[0];
        if (tex[3] == null)
            tex[3] = tex[1] ?? tex[0];

        return tex;
    }
}
