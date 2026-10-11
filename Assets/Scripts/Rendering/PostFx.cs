using UnityEngine;
using UnityEngine.Rendering;
#if NEANDERTHAL_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Neanderthal.Rendering
{
    /// <summary>
    /// Cinematic post-processing (ACES tonemapping, bloom, colour grading, vignette) when the project runs on URP.
    /// Without URP every call is a no-op, so the game still works on the Built-in pipeline.
    /// </summary>
    public static class PostFx
    {
        public static bool UrpActive
        {
            get
            {
#if NEANDERTHAL_URP
                return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                return false;
#endif
            }
        }

        public static void SetupCamera(Camera cam)
        {
#if NEANDERTHAL_URP
            if (!UrpActive || cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
#endif
        }

        /// <param name="tint">Overall colour filter of the level (warm canyon, cool valley...).</param>
        public static void CreateVolume(Transform parent, Color tint, float exposure = 0.45f, float bloom = 1.1f)
        {
#if NEANDERTHAL_URP
            if (!UrpActive) return;
            var go = new GameObject("PostProcessing");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.Override(18f);
            color.saturation.Override(14f);
            color.colorFilter.Override(tint);

            var b = profile.Add<Bloom>(true);
            b.threshold.Override(1.05f);
            b.intensity.Override(bloom);
            b.scatter.Override(0.65f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 1f, 1.08f, -0.04f));
            smh.highlights.Override(new Vector4(1.06f, 1.02f, 0.94f, 0f));

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
#endif
        }
    }
}
