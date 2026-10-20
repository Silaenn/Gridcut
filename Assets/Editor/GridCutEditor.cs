using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GridCut.EditorTools
{
    /// <summary>
    /// Batch helpers used from the CLI:
    ///   -executeMethod GridCut.EditorTools.GridCutEditor.PrepareProject
    ///   -executeMethod GridCut.EditorTools.GridCutEditor.BuildWebGL
    /// </summary>
    public static class GridCutEditor
    {
        const string MainScene = "Assets/Scenes/Main.unity";

        /// <summary>One-time project prep: TMP essentials, scene UI, clean pipeline leftovers.</summary>
        public static void PrepareProject()
        {
            CleanUrRemnants();
            ImportTmpEssentialsOnce();
            SceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[GridCutEditor] PrepareProject OK");
        }

        static void CleanUrRemnants()
        {
            // URP package has been removed from the manifest; drop lingering global
            // settings references so the console stays clean.
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            SetRendererPipelineMap();
        }

        static void SetRendererPipelineMap()
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var map = so.FindProperty("m_RenderPipelineGlobalSettingsMap");
            if (map == null || !map.isArray) return;
            for (int i = map.arraySize - 1; i >= 0; i--)
                map.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ImportTmpEssentialsOnce()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;

            string pkg = FindTmpEssentialsPackage();
            Debug.Log("[GridCutEditor] Importing TMP Essentials from " + pkg);
            if (pkg != null)
                AssetDatabase.ImportPackage(pkg, false);
            else
                Debug.LogWarning("[GridCutEditor] TMP Essential Resources unitypackage not found in PackageCache.");
        }

        /// <summary>Locates "TMP Essential Resources.unitypackage" inside the ugui package cache.</summary>
        static string FindTmpEssentialsPackage()
        {
            string cache = Path.GetFullPath("Library/PackageCache");
            if (!Directory.Exists(cache)) return null;
            foreach (var dir in Directory.GetDirectories(cache, "com.unity.ugui@*"))
            {
                var p = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        [MenuItem("GridCut/Prepare Project")]
        public static void PrepareFromMenu() => PrepareProject();

        static void SetBuildScenes(params string[] scenes)
        {
            var list = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                list[i] = new EditorBuildSettingsScene(scenes[i], true);
            EditorBuildSettings.scenes = list;
        }

        /// <summary>WebGL build; logs total size. Output: Builds/WebGL.</summary>
        public static void BuildWebGL()
        {
            PrepareProject();
            SetBuildScenes(MainScene);

            PlayerSettings.WebGL.template = "APPLICATION:Default";
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTarget.WebGL);
            SetGraphicsApisForWebGl();

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[GridCutEditor] Build failed: " + report.summary.result);
                EditorApplication.Exit(1);
                return;
            }
            long total = (long)report.summary.totalSize;
            Debug.Log($"[GridCutEditor] BUILD OK, totalSize = {total / 1024 / 1024} MB ({total} bytes)");
        }

        static void SetGraphicsApisForWebGl()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);
        }
    }
}
