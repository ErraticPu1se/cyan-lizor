using System;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class UtilityChecks
    {
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var surface = new BackgroundSurfaces(new Rect(-360, -180, 720, 360));
            PetPersonality lowEnergy = null, highEnergy = null, lowSympathy = null, highSympathy = null;
            int timidId = 0, braveId = 0; float minBravery = 2, maxBravery = -1;
            using (var pet = context.CreatePet())
            for (int id = 0; id < 24; id++)
            {
                pet.Generate(id.ToString()); var p = pet.Personality;
                if (p.Bravery < minBravery) { minBravery = p.Bravery; timidId = id; }
                if (p.Bravery > maxBravery) { maxBravery = p.Bravery; braveId = id; }
                if (lowEnergy == null || p.Energy < lowEnergy.Energy) lowEnergy = p;
                if (highEnergy == null || p.Energy > highEnergy.Energy) highEnergy = p;
                if (lowSympathy == null || p.Sympathy < lowSympathy.Sympathy) lowSympathy = p;
                if (highSympathy == null || p.Sympathy > highSympathy.Sympathy) highSympathy = p;
            }
            int lowTotal = 0, highTotal = 0;
            for (int seed = 0; seed < 8; seed++)
            {
                lowTotal += FirstRoam(seed, lowEnergy, surface);
                highTotal += FirstRoam(seed, highEnergy, surface);
            }
            Require(highTotal < lowTotal, "Energy did not influence exploration decisions");
            var a = new PetBehavior(42, lowSympathy) { Roam = false };
            var b = new PetBehavior(42, highSympathy) { Roam = false };
            a.Target(Vector2.right * 100); b.Target(Vector2.right * 100);
            Require(b.OwnerCueUtility > a.OwnerCueUtility, "Sympathy did not weight owner cue");
            a.Rest(); b.Rest();
            for (int i = 0; i < 300; i++) { a.ObserveOwner(Vector2.right * 300); a.Update(Vector2.zero, surface); }
            Require(a.Intent == PetIntent.Idle, "Disabled autonomy followed owner");
            var stuck = new PetBehavior(42) { Roam = false };
            stuck.Target(Vector2.right * 300);
            for (int i = 0; i < 1200; i++) stuck.Update(Vector2.zero, surface);
            Require(stuck.Intent == PetIntent.Idle && !stuck.InterestPoint.HasValue, "Unreachable cue never expired");
            var behavior = new PetBehavior(42); var position = Vector2.zero;
            int changes = 0, lastSwitch = 0; bool rested = false, explored = false;
            var previous = behavior.Intent;
            for (int tick = 0; tick < 4000; tick++)
            {
                var goal = behavior.Update(position, surface);
                if (goal.HasValue) position = Vector2.MoveTowards(position, goal.Value, 1);
                rested |= tick > 500 && behavior.Intent == PetIntent.Idle;
                explored |= behavior.Intent == PetIntent.Roaming;
                if (behavior.Intent != previous)
                {
                    Require(tick - lastSwitch > 5, "Utilities flickered between decisions");
                    changes++; lastSwitch = tick; previous = behavior.Intent;
                }
            }
            Require(rested && explored && changes > 2 && changes < 80, "Utilities failed sustained rest/explore cycle");
            var follower = new PetBehavior(42) { Roam = false };
            follower.Target(Vector2.zero);
            for (int i = 0; i < 500; i++) follower.Update(Vector2.zero, surface);
            follower.Roam = true;
            bool followed = false;
            for (int i = 0; i < 180; i++)
            {
                follower.ObserveOwner(new Vector2(350, 0)); follower.Update(Vector2.zero, surface);
                followed |= follower.Intent == PetIntent.FollowingOwner;
            }
            Require(followed, "Stable distant owner never attracted friendly following");
            follower.Roam = false; follower.Update(Vector2.zero, surface);
            Require(follower.Intent == PetIntent.Idle, "Disabling autonomy failed to interrupt following");
            using (var timid = context.CreatePet())
            using (var brave = context.CreatePet())
            {
                timid.Generate(timidId.ToString()); brave.Generate(braveId.ToString());
                var timidMotion = new PetMotion(timid, surface, 42) { Roam = false };
                var braveMotion = new PetMotion(brave, surface, 42) { Roam = false };
                for (int i = 0; i < 30; i++) { timidMotion.Advance(.025f); braveMotion.Advance(.025f); }
                timidMotion.MoveTo(timidMotion.Position + Vector2.right * 160);
                braveMotion.MoveTo(braveMotion.Position + Vector2.right * 160);
                bool braveJump = false, timidJump = false;
                for (int i = 0; i < 25; i++)
                {
                    timidMotion.Advance(.025f); braveMotion.Advance(.025f);
                    braveJump |= braveMotion.State == PetMotionState.Charging;
                    timidJump |= timidMotion.State == PetMotionState.Charging;
                }
                Require(braveJump && !timidJump, "Bravery did not affect safe propulsion choice");
            }
            var rapid = new PetBehavior(42);
            rapid.Target(Vector2.zero);
            for (int i = 0; i < 600; i++) { rapid.ObserveOwner(new Vector2(i % 2 == 0 ? -300 : 300, 100)); rapid.Update(Vector2.zero, surface); }
            Require(rapid.FriendUtility < .01f, "Rapid cursor motion caused owner chasing");
            Debug.Log("UTILITY_PERSONALITY_STABILITY_UNREACHABLE_CUE_CHECK_PASSED");
        }
        static int FirstRoam(int seed, PetPersonality personality, IPetSurfaces surface)
        {
            var behavior = new PetBehavior(seed, personality);
            for (int tick = 0; tick < 2500; tick++)
            { behavior.Update(Vector2.zero, surface); if (behavior.Intent == PetIntent.Roaming) return tick; }
            throw new Exception("Personality never explored");
        }
    }
}
