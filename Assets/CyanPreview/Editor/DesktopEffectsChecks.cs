using System;
using System.IO;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class DesktopEffectsChecks
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var packed = new byte[8];
            DesktopAlphaRenderer.PackPixels(new[] { new Color32(10,20,30,40), new Color32(0,0,0,0) }, packed, 1, 2);
            Require(packed[3] == 0 && packed[4] == 30 && packed[5] == 20 && packed[6] == 10 && packed[7] == 40, "Alpha bitmap orientation/channel mapping failed");
            var work = new Rect(-1920,40,1920,1040);
            foreach (float scale in new[] { .5f, 1f, 2f })
            {
                var rect = DesktopAlphaRenderer.CaptureRect(new Bounds(Vector3.zero, new Vector3(100,100,0)), work, scale);
                Require(rect.width >= 100 * scale && rect.height >= 100 * scale && work.Contains(rect.min) && rect.xMax <= work.xMax && rect.yMax <= work.yMax, "Alpha crop mapping failed");
            }
            var outside = DesktopAlphaRenderer.CaptureRect(new Bounds(new Vector3(100000,100000,0), Vector3.one), work, 1);
            Require(outside.width == 1 && outside.height == 1, "Off-screen effects did not clear their surface");
            bool invalid = false;
            try { DesktopAlphaRenderer.PackPixels(new Color32[1], new byte[4], 2, 2); }
            catch (ArgumentException) { invalid = true; }
            Require(invalid, "Invalid alpha buffer was accepted");
            VerifyCoverage("CyanPreview/HeadGlow"); VerifyCoverage("CyanPreview/JumpParticle");
            var root = new GameObject("Desktop effects checks");
            try
            {
                using (var pet = context.CreatePet())
                {
                    pet.Generate("12345");
                    var motion = new PetMotion(pet, new FrameSurfaces(new Rect(-360, -180, 720, 360)), 42) { Roam = false };
                    using (var effects = new CyanJumpEffects(root.transform, motion, pet.EffectColor, pet.Seed))
                    {
                        string gamePath = context.Manifest.gamePath;
                        bool rejected = false;
                        try
                        {
                            context.Manifest.gamePath = Path.Combine(gamePath, "nonexistent-audio-test");
                            using (var unavailable = new CyanPetAudio(root.transform, context, motion, effects, pet.Seed)) { }
                        }
                        catch (IOException) { rejected = true; }
                        finally { context.Manifest.gamePath = gamePath; }
                        Require(rejected && root.GetComponentsInChildren<AudioSource>().Length == 0, "Missing audio leaked playback objects");
                        using (var audio = new CyanPetAudio(root.transform, context, motion, effects, pet.Seed))
                        {
                            audio.Enabled = false; audio.Vocalize(); audio.Advance(float.PositiveInfinity);
                            Require(motion.VoiceIntensity == 0, "Muted voice intensity persisted");
                            foreach (var source in root.GetComponentsInChildren<AudioSource>()) Require(!source.isPlaying, "Mute left a sound playing");
                            for (int i = 0; i < 40; i++) motion.Advance(.025f);
                            Require(motion.JumpTo(new Vector2(150, 80)), "Desktop effect jump rejected");
                            for (int i = 0; i < 100; i++) { motion.Advance(.025f); effects.Advance(.025f); audio.Advance(.025f); }
                            Require(effects.BurstCount == 1, "Mute disabled propulsion particles");
                            audio.Enabled = true; audio.Advance(0); audio.Dispose(); audio.Dispose(); audio.Enabled = false;
                            Require(motion.VoiceIntensity == 0, "Disposed audio retained voice state");
                        }
                        effects.Dispose(); effects.Dispose(); effects.HeadBubble(1); effects.Advance(.1f);
                        Require(effects.ParticleCount == 0, "Disposed effects accepted emissions");
                        int bursts = effects.BurstCount;
                        for (int i = 0; i < 400; i++) motion.Advance(.025f);
                        Require(motion.JumpTo(new Vector2(-150, 80)), "Post-disposal jump rejected");
                        for (int i = 0; i < 100; i++) motion.Advance(.025f);
                        Require(effects.BurstCount == bursts && effects.ParticleCount == 0, "Replaced effects retained motion subscriptions");
                    }
                    Require(root.transform.childCount == 0, "Effects rebuild leaked objects");
                    using (var view = new SpriteMeshView(root.transform) { DesktopTransparency = true })
                    {
                        motion.ShiftWithSurface(new Vector2(30, 20)); pet.Draw(); view.Refresh(pet);
                        var light = root.transform.Find("Reference lizard/Head light");
                        Require(light.gameObject.activeSelf && Vector2.Distance(light.localPosition, pet.HeadPosition) < .001f, "Desktop head light did not follow head");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("DESKTOP_AUDIO_MUTE_MISSING_ASSETS_DISPOSAL_GLOW_TRANSPARENCY_CHECK_PASSED");
        }
        static void VerifyCoverage(string shaderName)
        {
            var root = new GameObject("Coverage check"); root.layer = 31;
            var mesh = new Mesh { vertices = new[] { new Vector3(-1,-1), new Vector3(1,-1), new Vector3(1,1), new Vector3(-1,1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                colors = new[] { Color.cyan, Color.cyan, Color.cyan, Color.cyan }, triangles = new[] { 0,1,2,0,2,3 } };
            var material = new Material(Shader.Find(shaderName));
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", 10);
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", new Color(0,1,1,.6f));
            root.AddComponent<MeshFilter>().sharedMesh = mesh; root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var cameraObject = new GameObject("Coverage camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1; camera.transform.position = new Vector3(0,0,-10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 31;
            var target = new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) { antiAliasing = 1 }; var capture = new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                capture.ReadPixels(new Rect(0,0,64,64),0,0); capture.Apply();
                int clear = 0, drawn = 0;
                var levels = new System.Collections.Generic.HashSet<byte>();
                foreach (var pixel in capture.GetPixels32())
                {
                    levels.Add(pixel.a);
                    if (pixel.a == 0) clear++; else drawn++;
                    Require(pixel.r <= pixel.a + 1 && pixel.g <= pixel.a + 1 && pixel.b <= pixel.a + 1, shaderName + " is not premultiplied");
                }
                Require(clear > 0 && drawn > 0 && levels.Count > 8, shaderName + " lacks smooth alpha falloff");

            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                foreach (var item in new UnityEngine.Object[] { root, cameraObject, mesh, material, target, capture }) UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }
}
