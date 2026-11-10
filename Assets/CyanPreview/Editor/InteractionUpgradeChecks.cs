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
            var surface = new DesktopSurfaces(new Rect(-500,-300,1000,600)) { UseWindowEdges = true };
            var window = new DesktopWindow(7, new Rect(-100,-100,200,200));
            surface.UpdateWindows(new[] { window });
            Check(surface.IsWindowChrome(new Vector2(80,90),1), "Close button region invited exploration");
            Check(!surface.IsWindowChrome(Vector2.zero,1), "Window content suppressed exploration");
            surface.UpdateWindows(new DesktopWindow[0], new Vector2(86,0), true);
            Check(surface.SupportLost, "Closed window did not release support");
            surface.UpdateWindows(new[] { window }, Vector2.zero, false);
            Check(!surface.SupportLost, "Unattached pet inherited support loss");
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
            }
            Debug.Log("PETTING_SWING_WINDOW_EDGE_CASES_PASSED");
        }
    }
}
