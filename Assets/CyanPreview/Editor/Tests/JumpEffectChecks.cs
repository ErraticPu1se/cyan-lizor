using System;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class JumpEffectChecks
    {
        public static void Run(ReferenceContext context)
        {
            foreach (var name in new[] { "CyanChargeSm", "CyanChargeM", "CyanChargeL", "Flip3C", "Clang2A", "Clang1H", "Clang1G", "CoralBreakA_7", "LizardVoiceCyanA", "LizardVoiceCyanB", "LizardVoiceCyanC" })
            {
                var clip = context.LoadSound(name);
                if (clip.samples <= 0 || clip.length <= 0) throw new Exception("Empty game sound: " + name);
            }
            var frame = new FrameSurfaces(new Rect(-360, -180, 720, 360));
            if (PetInteraction.GrabDelay != .1f) throw new Exception("Grab hold delay changed");
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, frame, 42) { Roam = false };
                var root = new GameObject("Jump effect check");
                try
                {
                    using (var effects = new CyanJumpEffects(root.transform, motion, pet.EffectColor, pet.Seed))
                    {
                        var random = UnityEngine.Random.state;
                        for (int i = 0; i < 40; i++) motion.Advance(.025f);
                        if (!motion.JumpTo(new Vector2(150, 80))) throw new Exception("Large arena jump rejected");
                        motion.Advance(.025f);
                        if (!ReferenceContext.Get(pet.Lizard, "animation").Equals(context.Static("Lizard+Animation", "PrepareToJump"))) throw new Exception("Charge animation not connected");
                        pet.Draw();
                        effects.HeadBubble(.8f);
                        if (effects.ParticleCount != 1) throw new Exception("Head bubble missing");
                        if (effects.BurstCount != 0) throw new Exception("Particles emitted before launch");
                        bool visible = false;
                        for (int i = 0; i < 400; i++)
                        {
                            motion.Advance(.025f); effects.Advance(.025f);
                            visible |= effects.ParticleCount >= 9;
                        }
                        if (!visible || effects.BurstCount != 1 || effects.ParticleCount != 0) throw new Exception("Jump burst lifetime/event regression");
                        if (!UnityEngine.Random.state.Equals(random)) throw new Exception("Jump effects changed global random state");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("GRAB_TIMING_IDLE_ROAM_JUMP_EFFECT_CHECK_PASSED");
        }
    }
}
