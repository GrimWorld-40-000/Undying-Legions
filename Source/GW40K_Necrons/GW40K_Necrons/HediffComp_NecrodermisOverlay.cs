using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace NecronMod
{
    public class HediffCompProperties_NecrodermisOverlay : HediffCompProperties
    {
        public HediffCompProperties_NecrodermisOverlay()
        {
            compClass = typeof(HediffComp_NecrodermisOverlay);
        }
    }

    public class HediffComp_NecrodermisOverlay : HediffComp
    {
        private static readonly float[] StageOpacities = { 0.25f, 0.50f, 0.75f, 1.00f };
        private const string TexBase = "Things/Pawn/Overlays/Necrodermis";
        private const float OverlaySize = 0.7225f; // 15% smaller

        private int cachedStage = -1;
        private int cachedRotInt = -1;
        private Material cachedMat;

        private string GetTexPath(Pawn pawn, Rot4 rot)
        {
            string bodyType = pawn.story?.bodyType?.defName ?? "Male";
            // West uses East texture, flipped via Y rotation
            Rot4 texRot = (rot == Rot4.West) ? Rot4.East : rot;
            string dir = texRot == Rot4.North ? "North" : texRot == Rot4.South ? "South" : "East";
            // Female has no East texture — fall back to South
            if (bodyType == "Female" && dir == "East")
                dir = "South";
            return $"{TexBase}_{bodyType}_{dir}";
        }

        public void DrawOverlay(Pawn pawn, Vector3 drawLoc, Rot4 rot)
        {
            int stage = parent.CurStageIndex;
            if ((uint)stage >= (uint)StageOpacities.Length) return;

            if (stage != cachedStage || rot.AsInt != cachedRotInt)
            {
                cachedStage = stage;
                cachedRotInt = rot.AsInt;
                string texPath = GetTexPath(pawn, rot);
                float opacity = StageOpacities[stage];
                cachedMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, opacity));
            }

            if (cachedMat == null) return;

            Vector3 pos = drawLoc;
            pos.y += 0.001f;

            Quaternion meshRot;
            if (pawn.Downed)
            {
                // Match the body's downed rotation so the overlay rotates with the pawn
                float downedAngle = pawn.Drawer.renderer.wiggler.downedAngle;
                meshRot = Quaternion.AngleAxis(downedAngle, Vector3.up);
            }
            else
            {
                // Rotate 180° around Y to mirror East texture for West — avoids negative-scale winding culling
                meshRot = (rot == Rot4.West)
                    ? Quaternion.Euler(0f, 180f, 0f)
                    : Quaternion.identity;
            }

            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, meshRot, new Vector3(OverlaySize, 1f, OverlaySize)),
                cachedMat,
                0);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    [HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
    public static class Patch_DrawNecrodermisOverlay
    {
        [HarmonyPostfix]
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride, bool neverAimWeapon)
        {
            try
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn?.health?.hediffSet == null) return;
                if (!pawn.Spawned) return;

                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("Necron_NecrodermisGrowth"));
                if (hediff == null) return;

                Rot4 rot = rotOverride ?? pawn.Rotation;
                hediff.TryGetComp<HediffComp_NecrodermisOverlay>()?.DrawOverlay(pawn, drawLoc, rot);
            }
            catch { }
        }
    }
}
