using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    // Shared clips belong to ReferenceContext; playback and voice state belong to one pet.
    public sealed class CyanPetAudio : IDisposable
    {
        readonly GameObject root;
        readonly PetMotion motion;
        readonly CyanJumpEffects effects;
        readonly AudioSource charge, voice;
        readonly AudioSource[] launch = new AudioSource[3];
        readonly AudioClip[] charges = new AudioClip[3], clangs = new AudioClip[3];
        readonly float[] samples = new float[128];
        float idleVoice = 18, bubbleTime;
        PetIntent lastIntent;
        bool enabled = true, disposed;
        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                if (!value) { charge.Stop(); voice.Stop(); foreach (var source in launch) source.Stop(); motion.VoiceIntensity = 0; }
            }
        }
        public CyanPetAudio(Transform parent, ReferenceContext context, PetMotion motion, CyanJumpEffects effects, int seed)
        {
            this.motion = motion; this.effects = effects;
            root = new GameObject("Cyan pet audio"); root.transform.SetParent(parent, false);
            try
            {
                charge = Source(); voice = Source();
                charges[0] = context.LoadSound("CyanChargeSm"); charges[1] = context.LoadSound("CyanChargeM"); charges[2] = context.LoadSound("CyanChargeL");
                clangs[0] = context.LoadSound("Clang2A"); clangs[1] = context.LoadSound("Clang1H"); clangs[2] = context.LoadSound("Clang1G");
                for (int i = 0; i < launch.Length; i++) launch[i] = Source();
                launch[0].clip = context.LoadSound("Flip3C"); launch[2].clip = context.LoadSound("CoralBreakA_7");
                // shortcut: deterministic A/B/C voice selection, not the game's voice personality generator.
                voice.clip = context.LoadSound("LizardVoiceCyan" + "ABC"[(int)((uint)seed % 3)]);
                voice.volume = .4f;
                motion.StateChanged += StateChanged; motion.JumpLaunched += Launched;
            }
            catch { Release(root); throw; }
        }
        AudioSource Source()
        {
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            return source;
        }
        static int Tier(float power) => power > .75f ? 2 : power > .25f ? 1 : 0;
        void StateChanged(PetMotionState state)
        {
            charge.Stop();
            if (enabled && state == PetMotionState.Charging)
            { charge.clip = charges[Tier(motion.JumpPower)]; charge.volume = .8f; charge.Play(); }
        }
        void Launched(Vector2 position, Vector2 velocity, float power)
        {
            if (!enabled) return;
            int tier = Tier(power);
            float volume = tier == 0 ? .4f : tier == 1 ? .7f : 1;
            float pitch = tier == 0 ? .8f : tier == 1 ? 1 : 1.2f;
            launch[1].clip = clangs[tier];
            launch[0].pitch = pitch; launch[0].volume = volume;
            launch[1].pitch = pitch * (tier == 0 ? 1 : tier == 1 ? .6f : .8f);
            launch[1].volume = volume * (tier == 0 ? .6f : tier == 1 ? .7f : .8f);
            launch[2].pitch = pitch * (2 + tier * .5f); launch[2].volume = volume * (.3f + tier * .1f);
            foreach (var source in launch) source.Play();
        }
        public void Vocalize()
        {
            if (!enabled || disposed || voice.isPlaying) return;
            voice.Play(); idleVoice = 18;
        }
        public void Advance(float seconds)
        {
            if (disposed) return;
            var intent = motion.Behavior.Intent;
            if (intent == PetIntent.Pursuing && lastIntent != intent) Vocalize();
            lastIntent = intent;
            if (intent == PetIntent.Idle && motion.State == PetMotionState.Crawling && (idleVoice -= seconds) <= 0) Vocalize();
            float intensity = 0;
            if (enabled && voice.isPlaying)
            {
                voice.GetOutputData(samples, 0);
                foreach (float sample in samples) intensity += sample * sample;
                intensity = Mathf.Clamp01(Mathf.Sqrt(intensity / samples.Length) * 12);
            }
            motion.VoiceIntensity = intensity;
            bubbleTime += seconds;
            while (bubbleTime >= .025f)
            {
                bubbleTime -= .025f;
                if (intensity > .5f) effects.HeadBubble(intensity);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            motion.StateChanged -= StateChanged; motion.JumpLaunched -= Launched;
            motion.VoiceIntensity = 0; charge.Stop(); voice.Stop(); foreach (var source in launch) source.Stop();
            Release(root);
        }
    }
}
