using UnityEditor;
using UnityEngine;

namespace Neanderthal.EditorTools
{
    /// <summary>
    /// Animation FBX files dropped into Assets/Resources/Animations are imported as Humanoid (so they retarget onto
    /// the Neanderthal), without materials, and locomotion clips (idle / walk / run...) loop.
    /// </summary>
    public class AnimationImportRules : AssetPostprocessor
    {
        static bool IsAnimation(string path) => path.Replace('\\', '/').Contains("/Resources/Animations/");

        void OnPreprocessModel()
        {
            if (!IsAnimation(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnimation(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            foreach (var c in clips)
            {
                // Mixamo names every take "mixamo.com": the file name says what the clip is (Walk, Attack_Axe...).
                if (clips.Length == 1 || c.name == "mixamo.com") c.name = file;
                string n = c.name.ToLowerInvariant();
                bool loop = n.Contains("loop") || n.Contains("idle") || n.Contains("walk") || n.Contains("run")
                            || n.Contains("jog") || n.Contains("sprint") || n.Contains("swim");
                bool once = n.Contains("death") || n.Contains("hit") || n.Contains("attack") || n.Contains("start") || n.Contains("land");
                c.loopTime = loop && !once;
                // Turning and bobbing stay in the pose; forward travel becomes root motion, which the game discards
                // (the AI moves the body), so walk cycles play in place and their speed can be measured.
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = false;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
