#if NEANDERTHAL_URP
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Neanderthal.Rendering.EditorTools
{
    /// <summary>
    /// Once the URP package is installed, creates a high-quality URP asset (soft 4-cascade shadows, HDR, MSAA)
    /// and makes it the active render pipeline. Runs automatically; also available from the menu.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpAutoSetup
    {
        const string Dir = "Assets/Settings";
        const string RendererPath = Dir + "/Neanderthal_Renderer.asset";
        const string PipelinePath = Dir + "/Neanderthal_URP.asset";

        static UrpAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (GraphicsSettings.defaultRenderPipeline == null) Setup();
            };
        }

        [MenuItem("Neanderthal/Graphics/Setup URP (HD)")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            if (renderer.postProcessData == null)
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var so = new SerializedObject(pipeline);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 4);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_AdditionalLightShadowsSupported", true);
            Set(so, "m_AdditionalLightsShadowmapResolution", 2048);
            Set(so, "m_ShadowDistance", 140f);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("Neanderthal: URP (HD) is now the active render pipeline.");
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break; // enums too: intValue writes the underlying value
                case float f: p.floatValue = f; break;
            }
        }
    }
}
#endif
