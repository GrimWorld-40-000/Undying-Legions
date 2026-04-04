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

        private int cachedStage = -1;
        private int cachedRotInt = -1;
        private Material cachedMat;

        private string GetTexPath(Pawn pawn, Rot4 rot)
        {
            string bodyType = pawn.story?.bodyType?.defName ?? "Male";
            Rot4 texRot = (rot == Rot4.West) ? Rot4.East : rot;
            string dir = texRot == Rot4.North ? "North" : texRot == Rot4.South ? "South" : "East";
            // Female has no East texture — fall back to South
            if (bodyType == "Female" && dir == "East")
                dir = "South";
            return $"{TexBase}_{bodyType}_{dir}";
        }

        public void DrawOverlay(Pawn pawn)
        {
            int stage = parent.CurStageIndex;
            if ((uint)stage >= (uint)StageOpacities.Length) return;

            Rot4 rot = pawn.Rotation;

            // Rebuild material only when stage or facing changes
            if (stage != cachedStage || rot.AsInt != cachedRotInt)
            {
                cachedStage = stage;
                cachedRotInt = rot.AsInt;
                string texPath = GetTexPath(pawn, rot);
                float opacity = StageOpacities[stage];
                cachedMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, opacity));
            }

            if (cachedMat == null) return;

            Vector3 pos = pawn.DrawPos;
            pos.y += 0.001f;

            // Mirror East texture for West facing
            float scaleX = (rot == Rot4.West) ? -1.5f : 1.5f;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scaleX, 1f, 1.5f)),
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
                hediff?.TryGetComp<HediffComp_NecrodermisOverlay>()?.DrawOverlay(pawn);
            }
            catch { }
        }
    }
}
