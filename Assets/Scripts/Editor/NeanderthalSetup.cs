using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Neanderthal.EditorTools
{
    /// <summary>
    /// Creates Assets/Scenes/Main.unity with the GameManager and registers it in Build Settings
    /// the first time the project is opened (also available via the Neanderthal menu).
    /// </summary>
    [InitializeOnLoad]
    public static class NeanderthalSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static NeanderthalSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!System.IO.File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode)
                    CreateMainScene();
            };
        }

        [MenuItem("Neanderthal/Create Main Scene")]
        public static void CreateMainScene()
        {
            if (!System.IO.Directory.Exists("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Neanderthal Game").AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AlwaysIncludeShaders();
            Debug.Log("Neanderthal: created " + ScenePath + " — press Play.");
        }

        /// <summary>Materials are created at runtime, so make sure their shaders survive player builds.</summary>
        static void AlwaysIncludeShaders()
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            foreach (var name in new[] { "Standard", "Unlit/Color", "Sprites/Default", "Skybox/Procedural" })
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedProperties();
        }

        [MenuItem("Neanderthal/Reset Player Profile")]
        static void ResetProfile()
        {
            Profile.Reset();
            Debug.Log("Neanderthal: profile reset.");
        }
    }
}
