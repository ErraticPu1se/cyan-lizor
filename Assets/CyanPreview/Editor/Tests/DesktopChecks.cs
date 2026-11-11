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
            var menuSettings = new DesktopSettings();
            foreach (var action in new[] { DesktopMenuAction.Small, DesktopMenuAction.Medium, DesktopMenuAction.Large })
            {
                menuSettings.ApplyMenu(action);
                Require(menuSettings.scale == (action == DesktopMenuAction.Small ? 1 : action == DesktopMenuAction.Medium ? 1.5f : 2), "Menu size preset is wrong");
            }
            bool initialRoam = menuSettings.roam, initialSound = menuSettings.sound, initialClicks = menuSettings.clickToExplore;
            foreach (var action in new[] { DesktopMenuAction.Roaming, DesktopMenuAction.Sound, DesktopMenuAction.ExploreClicks })
            { menuSettings.ApplyMenu(action); menuSettings.ApplyMenu(action); }
            Require(menuSettings.roam == initialRoam && menuSettings.sound == initialSound && menuSettings.clickToExplore == initialClicks,
                "Repeated menu toggles lost settings");
            var beforeClimb = JsonUtility.ToJson(menuSettings);
            menuSettings.ApplyMenu(DesktopMenuAction.Climbing);
            Require(menuSettings.climbingMode == ClimbingMode.EdgesOnly, "Menu did not select edges only");
            menuSettings.ApplyMenu(DesktopMenuAction.Climbing);
            Require(JsonUtility.ToJson(menuSettings) == beforeClimb, "Climbing toggle changed unrelated settings");
            Require((int)DesktopMenuAction.Stop == 8, "New action changed existing menu wire IDs");
            bool invalidMenu = false;
            try { menuSettings.ApplyMenu((DesktopMenuAction)999); } catch (ArgumentOutOfRangeException) { invalidMenu = true; }
            Require(invalidMenu && menuSettings.scale == 2, "Invalid menu action changed settings");
            foreach (float scale in new[] { .5f, 1f, 2f })
            {
                var work = new Rect(-1920, 40, 1920, 1040);
                Require(DesktopCoordinates.ToWorld(work.center, work, scale) == Vector2.zero, "Desktop centre mapping failed");
                var topLeft = DesktopCoordinates.ToWorld(work.position, work, scale);
                Require(topLeft == new Vector2(-960, 520) / scale, "Negative origin or top-left mapping failed");
                var mapped = DesktopCoordinates.ToWorld(work, work, scale);
                Require(mapped.center == Vector2.zero && mapped.size == work.size / scale, "DPI/size mapping failed");
            }
            var surfaces = new DesktopSurfaces(new Rect(-500, -300, 1000, 600)) { Mode = ClimbingMode.EdgesAndBackgrounds };
            var back = new DesktopWindow(1, new Rect(-100, -100, 200, 200));
            var cover = new DesktopWindow(2, new Rect(-120, -120, 240, 240));
            surfaces.UpdateWindows(new[] { back });
            Require(surfaces.WindowAtEdge(new Vector2(110, 0), out var edge, out var normal) == 1 && edge.x == 100 && normal == Vector2.left, "Window edge attachment failed");
            surfaces.UpdateWindows(new[] { cover, back });
            Require(surfaces.WindowAtEdge(new Vector2(100, 0), out _, out _) == 2, "Covered window edge remained active");
            surfaces.UpdateWindows(new DesktopWindow[0]);
            Require(surfaces.WindowAtEdge(new Vector2(100, 0), out _, out _) == 0, "Closed/minimized window retained edge");
            surfaces.UpdateWindows(new DesktopWindow[0]); surfaces.Mode = ClimbingMode.EdgesOnly;
            Require(surfaces.Nearest(Vector2.zero, out _) == new Vector2(0, -300), "Frame-only mode used a window");
            surfaces.Mode = ClimbingMode.EdgesAndBackgrounds; surfaces.UpdateWindows(new[] { back });
            var edgePosition = new Vector2(86, 0);
            var moved = new DesktopWindow(1, new Rect(-20, -60, 200, 200));
            Require(surfaces.UpdateWindows(new[] { moved }, edgePosition, true) == new Vector2(80, 40), "Window translation lost attachment");
            surfaces.UpdateWindows(new[] { back });
            var resized = new DesktopWindow(1, new Rect(-100, -100, 300, 240));
            Require(surfaces.UpdateWindows(new[] { resized }, edgePosition, true) == new Vector2(100, 20), "Resize did not preserve edge-relative grip");
            surfaces.UpdateWindows(new[] { back });
            Require(surfaces.UpdateWindows(new[] { moved }, edgePosition, false) == Vector2.zero, "Window carried an airborne or grabbed pet");
            surfaces.UpdateWindows(new[] { back });
            Require(surfaces.UpdateWindows(new DesktopWindow[0], edgePosition, true) == Vector2.zero, "Removed window moved pet");
            surfaces.UpdateWindows(new[] { back });
            var occluder = new DesktopWindow(3, new Rect(-40, -80, 240, 240));
            Require(surfaces.UpdateWindows(new[] { occluder, moved }, edgePosition, true) == Vector2.zero, "Newly covered support moved pet");
            surfaces.UpdateWindows(new[] { back });
            var offscreen = new DesktopWindow(1, new Rect(450, -100, 200, 200));
            Require(surfaces.UpdateWindows(new[] { offscreen }, edgePosition, true) == Vector2.zero, "Off-screen window pulled pet outside work area");
            surfaces.Mode = ClimbingMode.EdgesOnly;
            using (var pet = context.CreatePet())
            {
                pet.Generate("42"); var motion = new PetMotion(pet, surfaces, 42) { Roam = false };
                for (int i = 0; i < 40; i++) motion.Advance(.025f);
                var position = motion.Position;
                var limbs = (Array)Get(pet.Graphics, "limbs"); var foot = (Vector2)Get(limbs.GetValue(0), "pos");
                var chunks = (Array)Get(pet.Lizard, "bodyChunks");
                Set(chunks.GetValue(0), "vel", new Vector2(2, 1));
                motion.ShiftWithSurface(new Vector2(80, 20), false);
                Require((Vector2)Get(chunks.GetValue(0), "vel") == new Vector2(2, 1), "Window movement erased crawling velocity");
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
