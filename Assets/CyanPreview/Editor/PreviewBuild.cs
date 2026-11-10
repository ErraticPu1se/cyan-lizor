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
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
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

        static void VerifyIndependentPets(ReferenceContext context, ReferenceBridge first)
        {
            bool rejected = false;
            try { context.CreatePet("unknown"); }
            catch (NotSupportedException) { rejected = true; }
            if (!rejected) throw new Exception("Unknown species silently accepted");
            rejected = false;
            try { context.Dispose(); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Released shared assets while pets are alive");
            var firstGraphics = first.Graphics;
            var firstSprites = first.OrderedSprites.ToArray();
            var summary = first.Summary;
            var color = first.EffectColor;
            var root = new GameObject("Two-pet verification");
            using (var other = context.CreatePet())
            using (var firstView = new SpriteMeshView(root.transform, 0))
            using (var otherView = new SpriteMeshView(root.transform, 1))
            {
                other.Generate("12345");
                if (first.PetId == other.PetId || ReferenceEquals(first.Graphics, other.Graphics))
                    throw new Exception("Duplicate appearance IDs share mutable pet identity/state");
                if (summary != other.Summary || color != other.EffectColor)
                    throw new Exception("Duplicate IDs have different appearances");
                if (!ReferenceEquals(first.Textures, other.Textures))
                    throw new Exception("Atlas textures duplicated per pet");
                other.Generate("42");
                other.Pose(2, -.4f);
                other.Draw();
                if (first.Graphics != firstGraphics || !firstSprites.SequenceEqual(first.OrderedSprites) || first.Summary != summary)
                    throw new Exception("Updating another pet changed this pet");
                first.Pose(0, .7f);
                first.Draw();
                firstView.Refresh(first);
                otherView.Refresh(other);
                var groups = root.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>();
                if (groups.Length != 2 || groups[0].sortingOrder == groups[1].sortingOrder)
                    throw new Exception("Pet views do not have independent sorting groups");
                other.Generate("12345");
                if (summary != other.Summary || color != other.EffectColor)
                    throw new Exception("Interleaving pets changed appearance generation");
            }
            UnityEngine.Object.DestroyImmediate(root);
            if (first.Textures.Values.Any(t => t == null)) throw new Exception("Removing a pet destroyed shared textures");
            first.Generate("12345");
            if (summary != first.Summary || color != first.EffectColor)
                throw new Exception("Remaining pet cannot regenerate after removal");
            Debug.Log("MULTI_PET_OWNERSHIP_CHECK_PASSED");
        }

        public static void Verify()
        {
            if (!ReferenceBridge.TryParseId("ID.7.12345", out var spawner, out var seed) || spawner != 7 || seed != 12345)
                throw new Exception("Full ID parser failed");
            foreach (var invalid in new[] { "", "ID.a.2", "1.2.3", "2147483648", "ID.1.2.3" })
                if (ReferenceBridge.TryParseId(invalid, out _, out _)) throw new Exception("Accepted invalid ID: " + invalid);
            using (var context = new ReferenceContext(Path.GetFullPath("local-data")))
            using (var reference = context.CreatePet())
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
                VerifyIndependentPets(context, reference);
                JumpEffectChecks.Run(context);
                PetInteractionChecks.Run(context);
                VerifyAutonomousRoaming(context);
                VerifyMotion(context);
                reference.Pose(0, .7f);
                reference.Draw();
                var initialPose = SpriteGeometry(reference);
                reference.Pose(2, -.4f);
                reference.Draw();
                reference.Pose(0, .7f);
                reference.Draw();
                if (!initialPose.SequenceEqual(SpriteGeometry(reference)))
                    throw new Exception("Comparison pose depends on previous cosmetic spring history");
                if (reference.Summary != first || reference.EffectColor != color)
                    throw new Exception("Pose changed ID-generated appearance");
                if (!UnityEngine.Random.state.Equals(state)) throw new Exception("Pose leaked random state");
                Debug.Log("POSE_REPEATABILITY_CHECK_PASSED");
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

        static void VerifyAutonomousRoaming(ReferenceContext context)
        {
            foreach (int behaviorSeed in new[] { 0, 1, 42, 73 })
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var surfaces = new FrameSurfaces(new Rect(-130, -42, 180, 84));
                var motion = new PetMotion(pet, surfaces, behaviorSeed);
                var start = motion.Position;
                bool moved = false, rested = false, resumed = false;
                Vector2 restPosition = start;
                for (int tick = 0; tick < 4000; tick++)
                {
                    motion.LookAt(new Vector2(-40, 20));
                    motion.Advance(.025f);
                    moved |= Vector2.Distance(start, motion.Position) > 25;
                    if (moved && motion.Behavior.Intent == PetIntent.Idle && !rested)
                    { rested = true; restPosition = motion.Position; }
                    if (rested && motion.Behavior.Intent == PetIntent.Roaming && Vector2.Distance(restPosition, motion.Position) > 25)
                    { resumed = true; break; }
                }
                if (!moved || !rested || !resumed)
                    throw new Exception("Autonomous roam stalled without clicks: seed=" + behaviorSeed + " moved=" + moved + " rested=" + rested + " resumed=" + resumed + " position=" + motion.Position);
                Debug.Log("AUTONOMOUS_ROAM_REST_RESUME_CHECK_PASSED seed=" + behaviorSeed);
            }
        }

        static void VerifyMotion(ReferenceContext context)
        {
            var surfaces = new FrameSurfaces(new Rect(-130, -42, 180, 84));
            using (var a = context.CreatePet())
            using (var b = context.CreatePet())
            {
                a.Generate("12345"); b.Generate("12345");
                var first = new PetMotion(a, surfaces, 73);
                var second = new PetMotion(b, surfaces, 73);
                first.MoveTo(new Vector2(50, 30)); second.MoveTo(new Vector2(50, 30));
                var random = UnityEngine.Random.state;
                bool gripped = false, wall = false;
                for (int i = 0; i < 400; i++)
                {
                    first.Advance(.025f);
                    second.Advance(.0125f); second.Advance(.0125f);
                    if (Vector2.Distance(first.Position, second.Position) > .001f) throw new Exception("Motion depends on frame timing or other pet");
                    gripped |= first.GripCount > 0;
                    wall |= first.Position.y > -15;
                    if (float.IsNaN(first.Position.x) || !surfaces.Bounds.Contains(first.Position)) throw new Exception("Motion left surface bounds");
                }
                if (!gripped || !wall) throw new Exception("Crawl did not grip or climb wall: grips=" + gripped + " wall=" + wall + " position=" + first.Position);
                first.Roam = false;
                bool launched = false;
                for (int i = 0; i < 40 && !launched; i++) { first.Advance(.025f); launched = first.JumpTo(new Vector2(-40, 0)); }
                if (!launched) throw new Exception("Cyan did not accept supported jump");
                var unchanged = second.Position;
                bool airborne = false, landed = false;
                for (int i = 0; i < 240; i++)
                {
                    first.Advance(.025f);
                    if (first.State == PetMotionState.Airborne)
                    {
                        airborne = true;
                        if (first.GripCount != 0) throw new Exception("Jump retained planted feet");
                    }
                    if (airborne && first.State == PetMotionState.Crawling) landed = true;
                    a.Draw();
                }
                if (!airborne || !landed) throw new Exception("Cyan did not launch and land");
                if (second.Position != unchanged) throw new Exception("Moving one pet moved another");
                if (!UnityEngine.Random.state.Equals(random)) throw new Exception("Motion leaked random state");
                foreach (float value in SpriteGeometry(a)) if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("Motion produced invalid graphics");
                first.Roam = false;
                for (int i = 0; i < 120; i++) first.Advance(.025f);
                var body = (Array)ReferenceContext.Get(a.Lizard, "bodyChunks");
                var front = (Vector2)ReferenceContext.Get(body.GetValue(0), "pos");
                var back = (Vector2)ReferenceContext.Get(body.GetValue(1), "pos");
                var forward = (front - back).normalized;
                var side = new Vector2(-forward.y, forward.x);
                first.LookAt(front + forward * 60 + side * 100);
                for (int i = 0; i < 50; i++) first.Advance(.025f);
                var upper = (a.HeadPosition - first.Position).normalized;
                first.LookAt(front + forward * 60 - side * 100);
                for (int i = 0; i < 50; i++) first.Advance(.025f);
                var lower = (a.HeadPosition - first.Position).normalized;
                if (Vector2.Dot(upper - lower, side) < .2f) throw new Exception("Head did not track target independently");
                if (first.Behavior.Intent != PetIntent.Idle) throw new Exception("Disabled roaming did not settle to idle");
                first.MoveTo(surfaces.Advance(first.Position, 70));
                first.Advance(.025f);
                if (first.Behavior.Intent != PetIntent.Pursuing) throw new Exception("Clicked target did not start pursuit");
                Debug.Log("HEAD_TRACKING_IDLE_TARGET_CHECK_PASSED");
                Debug.Log("CRAWL_GRIP_PROPULSION_CHECK_PASSED");
            }
        }

        static float[] SpriteGeometry(ReferenceBridge pet)
        {
            return pet.OrderedSprites.SelectMany(sprite => new[] { "x", "y", "rotation", "scaleX", "scaleY" }
                .Select(name => (float)ReferenceContext.Get(sprite, name))
                .Concat(sprite.GetType().Name == "TriangleMesh"
                    ? ((Vector2[])ReferenceContext.Get(sprite, "vertices")).SelectMany(v => new[] { v.x, v.y })
                    : Enumerable.Empty<float>())).ToArray();
        }
    }
}
