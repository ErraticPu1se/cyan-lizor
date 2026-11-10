using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class DesktopChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            foreach (float scale in new[] { .5f, 1f, 2f })
            {
                var work = new Rect(-1920, 40, 1920, 1040);
                Require(DesktopCoordinates.ToWorld(work.center, work, scale) == Vector2.zero, "Desktop centre mapping failed");
                var topLeft = DesktopCoordinates.ToWorld(work.position, work, scale);
                Require(topLeft == new Vector2(-960, 520) / scale, "Negative origin or top-left mapping failed");
                var mapped = DesktopCoordinates.ToWorld(work, work, scale);
                Require(mapped.center == Vector2.zero && mapped.size == work.size / scale, "DPI/size mapping failed");
            }
            var surfaces = new DesktopSurfaces(new Rect(-500, -300, 1000, 600)) { UseWindowEdges = true };
            var back = new DesktopWindow(1, new Rect(-100, -100, 200, 200));
            var cover = new DesktopWindow(2, new Rect(-120, -120, 240, 240));
            surfaces.UpdateWindows(new[] { back });
            Require(surfaces.WindowAtEdge(new Vector2(110, 0), out var edge, out var normal) == 1 && edge.x == 100 && normal == Vector2.left, "Window edge attachment failed");
            surfaces.UpdateWindows(new[] { cover, back });
            Require(surfaces.WindowAtEdge(new Vector2(100, 0), out _, out _) == 2, "Covered window edge remained active");
            surfaces.UpdateWindows(new DesktopWindow[0]);
            Require(surfaces.WindowAtEdge(new Vector2(100, 0), out _, out _) == 0, "Closed/minimized window retained edge");
            surfaces.UpdateWindows(new[] { back }); surfaces.UseWindowEdges = false;
            Require(surfaces.Nearest(Vector2.zero, out _) == new Vector2(0, -300), "Frame-only mode used a window");
            using (var pet = context.CreatePet())
            {
                pet.Generate("42"); var motion = new PetMotion(pet, surfaces, 42) { Roam = false };
                for (int i = 0; i < 40; i++) motion.Advance(.025f);
                var position = motion.Position;
                var limbs = (Array)Get(pet.Graphics, "limbs"); var foot = (Vector2)Get(limbs.GetValue(0), "pos");
                motion.ShiftWithSurface(new Vector2(80, 20));
                Require(Vector2.Distance(motion.Position, position + new Vector2(80, 20)) < .001f, "Moving window did not carry body");
                Require(Vector2.Distance((Vector2)Get(limbs.GetValue(0), "pos"), foot + new Vector2(80, 20)) < .001f, "Moving window left feet behind");
                Require(motion.State == PetMotionState.Crawling, "Moving window changed interaction state");
                for (int i = 0; i < 200; i++) { motion.Advance(.025f); pet.Draw(); }
                Require(!float.IsNaN(motion.Position.x), "Desktop motion became invalid");
            }
            Debug.Log("DESKTOP_COORDINATES_OCCLUSION_REMOVAL_MOVING_SURFACE_CHECK_PASSED");
        }
    }
}
