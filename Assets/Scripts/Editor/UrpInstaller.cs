using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Neanderthal.EditorTools
{
    /// <summary>
    /// Installs the Universal Render Pipeline package (the version matching this Editor) on first open.
    /// After the install Unity recompiles and UrpAutoSetup switches the project to URP.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpInstaller
    {
        const string Package = "com.unity.render-pipelines.universal";
        const string AskedKey = "Neanderthal.UrpInstallRequested";
        static AddRequest request;

        static UrpInstaller()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime") != null) return;
                if (SessionState.GetBool(AskedKey, false)) return;
                Install();
            };
        }

        [MenuItem("Neanderthal/Graphics/Install URP package")]
        public static void Install()
        {
            SessionState.SetBool(AskedKey, true);
            Debug.Log("Neanderthal: installing Universal Render Pipeline for HD graphics...");
            request = Client.Add(Package);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success) Debug.Log("Neanderthal: URP installed (" + request.Result.version + ").");
            else Debug.LogWarning("Neanderthal: URP install failed: " + request.Error?.message);
            request = null;
        }
    }
}
