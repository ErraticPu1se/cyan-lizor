using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class RenderOptimizationChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var root = new GameObject("Crop regression");
            var renderers = new List<Renderer>();
            var mesh = new Mesh(); var empty = new Mesh();
            try
            {
                var petObject = new GameObject("Visible geometry"); petObject.transform.SetParent(root.transform);
                petObject.transform.position = new Vector3(800, -400);
                mesh.vertices = new[] { Vector3.zero, Vector3.right * 20, Vector3.up * 10 };
                mesh.triangles = new[] { 0, 1, 2 };
                petObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var visible = petObject.AddComponent<MeshRenderer>();
                var effect = new GameObject("Empty effect at origin"); effect.transform.SetParent(root.transform);
                effect.AddComponent<MeshFilter>().sharedMesh = empty; effect.AddComponent<MeshRenderer>();
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform, renderers, out var bounds) && bounds.min.x > 790 && bounds.max.y < -380,
                    "Empty effect expanded the capture to scene origin");
                var work = new Rect(0, 0, 2560, 1440);
                var crop = DesktopAlphaRenderer.CaptureRect(bounds, work, 1);
                Require(crop.width * crop.height < 4096, "Small distant pet caused a large pixel copy");
                empty.vertices = new[] { new Vector3(-10,0), new Vector3(10,0), new Vector3(0,10) }; empty.triangles = new[] {0,1,2};
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform, renderers, out bounds) && bounds.min.x <= -10, "Live particles were excluded");
                var viewport=new Rect(-1280,-720,2560,1440);
                effect.transform.position=new Vector3(-10000,0);
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform,renderers,out bounds,viewport) && bounds.min.x>790,
                    "Off-screen game placeholder expanded capture");
                effect.transform.position=new Vector3(1285,0);
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform,renderers,out bounds,viewport) && bounds.max.x==1280,
                    "Partially visible effect was dropped or not clipped");
                effect.transform.position=Vector3.zero;
                effect.SetActive(false);
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform, renderers, out bounds) && bounds.min.x > 790, "Inactive effects expanded capture");
                visible.enabled = false;
                Require(!DesktopAlphaRenderer.VisibleBounds(root.transform, renderers, out bounds), "Hidden pet retained a capture region");
                visible.enabled = true;
                petObject.transform.position=new Vector3(10000,0);
                Require(!DesktopAlphaRenderer.VisibleBounds(root.transform,renderers,out bounds,viewport),"Fully off-screen pet kept capture alive");
                petObject.transform.position=new Vector3(800,-400);
                effect.SetActive(true); empty.Clear();
                Require(DesktopAlphaRenderer.VisibleBounds(root.transform, renderers, out bounds) && bounds.min.x > 790, "Expired effects retained old bounds");
                using (var pixels = new NativeArray<Color32>(6, Allocator.Temp))
                {
                    var writable = pixels; // NativeArray is a value type; the using binding is readonly.
                    for (int i=0; i<6; i++) writable[i] = new Color32((byte)(200+i), (byte)(100+i), (byte)(50+i), (byte)(i*50));
                    var output = new byte[24]; DesktopAlphaRenderer.PackPixels(pixels, output, 3, 2);
                    for (int y=0; y<2; y++) for (int x=0; x<3; x++)
                    {
                        var pixel=pixels[(1-y)*3+x]; int offset=(y*3+x)*4;
                        Require(output[offset]==Math.Min(pixel.b,pixel.a) && output[offset+1]==Math.Min(pixel.g,pixel.a)
                            && output[offset+2]==Math.Min(pixel.r,pixel.a) && output[offset+3]==pixel.a, "Pixel optimization changed transparency or orientation");
                    }
                    bool rejected=false;
                    try { DesktopAlphaRenderer.PackPixels(pixels, new byte[23], 3, 2); } catch(ArgumentException) { rejected=true; }
                    Require(rejected, "Short destination accepted");
                }
                using(var pet=context.CreatePet())
                {
                    pet.Generate("9320");
                    var motion=new PetMotion(pet,new FrameSurfaces(new Rect(-500,-300,1000,600)),42) {Roam=false};
                    using(var effects=new CyanJumpEffects(root.transform,motion,pet.EffectColor,pet.Seed))
                    {
                        var effectRenderer=root.transform.Find("Cyan jump effects").GetComponent<MeshRenderer>();
                        Require(!effectRenderer.enabled,"Empty effect starts visible");
                        effects.HeadBubble(1); effects.Advance(.001f);
                        Require(effectRenderer.enabled && effects.ParticleCount>0,"New head bubbles failed to render");
                        for(int i=0;i<100;i++) effects.Advance(.25f);
                        Require(!effectRenderer.enabled && effects.ParticleCount==0,"Expired particle batch stayed visible");
                        effects.HeadBubble(1); effects.Advance(.001f);
                        Require(effectRenderer.enabled,"Effects did not restart after empty batch");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(empty); }
            VerifyCropPixels(context);
            Debug.Log("RENDER_OPTIMIZATION_CHECKS_PASSED");
        }
        static void VerifyCropPixels(ReferenceContext context)
        {
            var root = new GameObject("Pixel comparison pet");
            var cameraObject = new GameObject("Pixel comparison camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled=false; camera.orthographic=true; camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
            camera.allowHDR=false; camera.allowMSAA=false;
            var work=new Rect(0,0,1280,720);
            var originalActive=RenderTexture.active;
            try
            {
                using(var pet=context.CreatePet())
                using(var view=new SpriteMeshView(root.transform) {DesktopTransparency=true})
                {
                    pet.Generate("9320");
                    var motion=new PetMotion(pet,new FrameSurfaces(new Rect(-640,-360,1280,720)),42) {Roam=false};
                    motion.ShiftWithSurface(new Vector2(380,-240)-motion.Position);
                    using(var effects=new CyanJumpEffects(root.transform,motion,pet.EffectColor,pet.Seed))
                    {
                        for(int pass=0;pass<2;pass++)
                        {
                            if(pass==1) effects.HeadBubble(1);
                            effects.Advance(.001f); pet.Draw(); view.Refresh(pet);
                            var renderers=new List<Renderer>();
                            foreach(var renderer in root.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer=30;
                            Require(DesktopAlphaRenderer.VisibleBounds(root.transform,renderers,out var bounds,new Rect(-640,-360,1280,720)),"Comparison pet has no visible bounds");
                            var crop=DesktopAlphaRenderer.CaptureRect(bounds,work,1);
                            var fullPixels=Capture(camera,new RectInt(0,0,1280,720),work);
                            var cropPixels=Capture(camera,crop,work);
                            int mismatch=0, escaped=0, visible=0;
                            int bottom=720-crop.yMax;
                            for(int y=0;y<720;y++) for(int x=0;x<1280;x++)
                            {
                                var full=fullPixels[y*1280+x]; if(full.a>2) visible++;
                                bool inside=x>=crop.x && x<crop.xMax && y>=bottom && y<bottom+crop.height;
                                if(!inside) { if(full.a>2) escaped++; continue; }
                                var clipped=cropPixels[(y-bottom)*crop.width+x-crop.x];
                                if(Math.Abs(full.r-clipped.r)>2 || Math.Abs(full.g-clipped.g)>2 || Math.Abs(full.b-clipped.b)>2 || Math.Abs(full.a-clipped.a)>2) mismatch++;
                            }
                            Require(visible>100,"Pixel comparison rendered no pet");
                            Require(escaped==0,"Crop clipped visible lizard/glow/particle pixels");
                            // Different orthographic centers may round rasterization at a few edge pixels.
                            Require(mismatch<=8,"Cropped rendering differs from full viewport: "+mismatch+" pixels");
                            Debug.Log("CROP_PIXEL_PARITY pass="+pass+" visible="+visible+" mismatches="+mismatch+" escaped="+escaped);
                        }
                    }
                }
            }
            finally { RenderTexture.active=originalActive; UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root); }
        }
        static Color32[] Capture(Camera camera,RectInt rect,Rect work)
        {
            var target=new RenderTexture(rect.width,rect.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {antiAliasing=1};
            var texture=new Texture2D(rect.width,rect.height,TextureFormat.RGBA32,false,true);
            try
            {
                var center=DesktopCoordinates.ToWorld(rect.center,work,1);
                camera.transform.position=new Vector3(center.x,center.y,-10);
                camera.orthographicSize=rect.height/2f; camera.aspect=rect.width/(float)rect.height;
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,rect.width,rect.height),0,0,false);
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
