using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class InteractionUpgradeChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var contact = new PettingContact(); var other = new PettingContact();
            for (int i = 0; i < 3; i++) Check(!contact.Advance(.25f, true), "Petting fired early");
            Check(contact.Advance(.25f, true), "One second petting failed");
            for (int i = 0; i < 40; i++) Check(!contact.Advance(.25f, true), "Continuous petting bypassed cooldown");
            for (int i = 0; i < 3; i++) Check(!contact.Advance(.25f, true), "Cooldown queued a call");
            Check(contact.Advance(.25f, true), "Sustained petting did not resume");
            for (int i = 0; i < 3; i++) Check(!other.Advance(.25f, true), "Other pet fired early");
            other.Advance(.25f, false);
            Check(!other.Advance(.25f, true), "Leaving sprite retained contact");
            other.ResetContact(); Check(!other.Advance(100, true), "Frame stall completed contact");
            bool rejected = false;
            try { other.Advance(float.NaN, true); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid contact time accepted");
            var waiting = new PettingContact();
            for (int i=0;i<8;i++) Check(!waiting.Advance(.25f,true,false), "Unavailable response spent contact");
            for (int i=0;i<3;i++) Check(!waiting.Advance(.25f,true), "Readiness skipped contact duration");
            Check(waiting.Advance(.25f,true), "Unavailable sound silently spent petting cooldown");
            var jitter = new PettingContact();
            for(int i=0;i<3;i++) jitter.Advance(.25f,true);
            Check(!jitter.Advance(.1f,false), "Outside sprite triggered petting");
            Check(jitter.Advance(.25f,true), "Brief sprite motion reset all contact");
            var surface = new DesktopSurfaces(new Rect(-500,-300,1000,600)) { UseWindowEdges = true };
            var window = new DesktopWindow(7, new Rect(-100,-100,200,200));
            surface.UpdateWindows(new[] { window });
            Check(surface.IsWindowChrome(new Vector2(80,90),1), "Close button region invited exploration");
            Check(!surface.IsWindowChrome(Vector2.zero,1), "Window content suppressed exploration");
            surface.UpdateWindows(new DesktopWindow[0], new Vector2(86,0), true);
            Check(surface.SupportLost, "Closed window did not release support");
            surface.UpdateWindows(new[] { window }, Vector2.zero, false);
            Check(!surface.SupportLost, "Unattached pet inherited support loss");
            surface.UpdateWindows(new[] { window });
            var movedPlane = new DesktopWindow(7, new Rect(-20,-60,200,200));
            Check(surface.UpdateWindows(new[] { movedPlane }, Vector2.zero, true)==new Vector2(80,40), "Window interior did not translate with window");
            surface.UpdateWindows(new[] { window });
            Check(surface.UpdateWindows(new[] { new DesktopWindow(7,new Rect(-100,-100,400,300)) }, Vector2.zero, true)==new Vector2(100,50), "Window interior resize lost relative position");
            surface.UpdateWindows(new[] { window });
            surface.UpdateWindows(new DesktopWindow[0], Vector2.zero, true);
            Check(surface.SupportLost,"Interior attachment survived closing window");
            surface.UpdateWindows(new[] { window });
            var front = new DesktopWindow(8, new Rect(-200,-200,400,400));
            surface.UpdateWindows(new[] { front,window }, Vector2.zero, true);
            Check(surface.SupportLost && surface.WindowAtSurface(Vector2.zero)==8,"Interior occlusion retained covered window");
            surface.UpdateWindows(new[] { window });
            Check(surface.UpdateWindows(new[] { movedPlane },Vector2.zero,false)==Vector2.zero,"Window carried a grabbed or airborne pet");
            using (var first = context.CreatePet())
            using (var second = context.CreatePet())
            {
                first.Generate("9320"); second.Generate("9320");
                var a = new PetMotion(first, surface) { Roam = false };
                var b = new PetMotion(second, new BackgroundSurfaces(surface.Bounds)) { Roam = false };
                var original = b.Position;
                a.BeginGrab(a.Position); a.DragTo(new Vector2(0,100));
                for (int i=0;i<100;i++) a.Advance(.025f);
                var chunks = (Array)Get(first.Lizard,"bodyChunks");
                var before = (Vector2)Get(chunks.GetValue(2),"pos") - a.Position;
                a.DragTo(new Vector2(20,100)); a.Advance(.025f);
                var after = (Vector2)Get(chunks.GetValue(2),"pos") - a.Position;
                Check(Vector2.Distance(before,after)>1, "Dragging translated a rigid pose");
                Check(b.Position==original && b.State==PetMotionState.Crawling,"Grab leaked between duplicate IDs");
                var tail=(Array)Get(first.Graphics,"tail");
                for(int i=0;i<150;i++)
                {
                    a.DragTo(new Vector2(i%2==0?-10000:10000, i%3==0?10000:80)); a.Advance(.025f);
                    foreach(var chunk in chunks) Check(surface.Bounds.Contains((Vector2)Get(chunk,"pos")),"Extreme drag escaped work area");
                    foreach(var segment in tail) Check(!float.IsNaN(((Vector2)Get(segment,"pos")).x),"Tail became invalid");
                }
                a.EndGrab(); Check(a.State==PetMotionState.Airborne,"Release stayed grabbed");
                foreach(var chunk in chunks) Check(((Vector2)Get(chunk,"vel")).magnitude<=18.01f,"Release momentum unbounded");
                Check(context.LoadSound(first.Species.AffectionSound(first.Seed))!=null,"Cyan affection clip unavailable");
                int landings=0; float impact=0;
                b.Landed += speed => { landings++; impact=speed; };
                b.BeginGrab(b.Position); b.DragTo(new Vector2(0,100));
                for(int i=0;i<60;i++) b.Advance(.025f);
                b.EndGrab();
                for(int i=0;i<180;i++) b.Advance(.025f);
                Check(landings==1 && impact>1.5f,"Falling did not emit exactly one impact");
                for(int i=0;i<180;i++) b.Advance(.025f);
                Check(landings==1,"Resting/crawling repeated landing impact");
            }
            Debug.Log("PETTING_SWING_WINDOW_EDGE_CASES_PASSED");
        }
    }
}
