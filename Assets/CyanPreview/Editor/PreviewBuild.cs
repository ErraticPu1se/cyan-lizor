using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class PreviewBuild
    {
        [MenuItem("Cyan Lizor/Create preview scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Preview.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Preview.unity", true) };
            PlayerSettings.companyName = "ErraticPu1se";
            PlayerSettings.productName = "Cyan Lizor Preview";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1100;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cyan Lizor/Build Windows preview")]
        public static void Windows()
        {
            CreateScene();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Preview.unity" },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/Windows/CyanLizor.exe",
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
        }

        public static void Verify()
        {
            if (!ReferenceBridge.TryParseId("ID.7.12345", out var spawner, out var seed) || spawner != 7 || seed != 12345)
                throw new Exception("Full ID parser failed");
            foreach (var invalid in new[] { "", "ID.a.2", "1.2.3", "2147483648", "ID.1.2.3" })
                if (ReferenceBridge.TryParseId(invalid, out _, out _)) throw new Exception("Accepted invalid ID: " + invalid);
            using (var reference = new ReferenceBridge(Path.GetFullPath("local-data")))
            {
                UnityEngine.Random.InitState(99);
                var state = UnityEngine.Random.state;
                reference.Generate("12345");
                string first = reference.Summary;
                Color color = reference.EffectColor;
                if (!UnityEngine.Random.state.Equals(state)) throw new Exception("Generator leaked random state");
                reference.Generate("ID.7.12345");
                if (first != reference.Summary || color != reference.EffectColor) throw new Exception("Spawner changed appearance");
                foreach (int id in new[] { 0, 1, 2, 42, 999, -1, int.MaxValue, int.MinValue })
                {
                    reference.Generate(id.ToString());
                    if (!reference.OrderedSprites.Any()) throw new Exception("No render sprites for " + id);
                    Debug.Log("REFERENCE_CHECK " + id + " " + reference.Summary);
                }
                reference.Generate("12345");
                if (reference.Summary != first || reference.EffectColor != color)
                    throw new Exception("Prior generations changed the result");
                var root = new GameObject("Verification view");
                using (var view = new SpriteMeshView(root.transform))
                {
                    view.Refresh(reference);
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                        foreach (var vertex in filter.sharedMesh.vertices)
                            if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y))
                                throw new Exception("Invalid sprite geometry");
                    var cameraObject = new GameObject("Verification camera");
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.orthographic = true;
                    camera.orthographicSize = 65;
                    camera.transform.position = new Vector3(-45, 0, -10);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.12f, .16f, .19f);
                    var target = new RenderTexture(1000, 400, 24);
                    camera.targetTexture = target;
                    camera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var capture = new Texture2D(1000, 400, TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0, 0, 1000, 400), 0, 0);
                    capture.Apply();
                    Directory.CreateDirectory("Builds/Checks");
                    File.WriteAllBytes("Builds/Checks/cyan-12345.png", capture.EncodeToPNG());
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(capture);
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("CYAN_PREVIEW_VERIFICATION_PASSED");
        }
    }
}
