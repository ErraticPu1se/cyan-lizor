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
        readonly AudioSource[] impacts = new AudioSource[3];
        readonly AudioClip regularVoice, affectionVoice;
        readonly AudioSource[] launch = new AudioSource[3];
        readonly AudioClip[] charges = new AudioClip[3], clangs = new AudioClip[3];
        readonly float[] samples = new float[128];
        readonly System.Random voiceRandom;
        float idleVoice, bubbleTime, vocalCooldown, affectionCooldown, impactCooldown;
        public const float VocalCooldownSeconds = 10;
        PetIntent lastIntent;
        bool enabled = true, disposed;
        public bool Enabled
        {
            get => enabled;
            set
            {
                if (disposed || enabled == value) return;
                enabled = value;
                if (!value) { charge.Stop(); voice.Stop(); foreach (var source in launch) source.Stop(); foreach (var source in impacts) if (source != null) source.Stop(); motion.VoiceIntensity = 0; }
            }
        }
        public CyanPetAudio(Transform parent, ReferenceContext context, PetMotion motion, CyanJumpEffects effects, int seed)
        {
            this.motion = motion; this.effects = effects;
            voiceRandom = new System.Random(seed ^ 9319);
            idleVoice = NextIdleDelay();
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
                regularVoice = voice.clip;
                var affection = motion.Species.AffectionSound(seed);
                if (affection != null)
                    try { affectionVoice = context.LoadSound(affection); }
                    catch (System.IO.IOException e) { Debug.LogWarning("Affection sound unavailable: " + e.Message); }
                voice.volume = .4f;
                var impactNames = motion.Species.ImpactSounds;
                if (impactNames != null)
                    for (int i = 0; i < impacts.Length; i++)
                    {
                        impacts[i] = Source(); impacts[i].clip = context.LoadSound(impactNames[i]);
                    }
                motion.StateChanged += StateChanged; motion.JumpLaunched += Launched; motion.Landed += Landed;
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
        float NextIdleDelay() => 30 + (float)voiceRandom.NextDouble() * 30;
        public bool CanPettingCall => !disposed && enabled && affectionVoice != null && affectionCooldown <= 0 && !voice.isPlaying;
        public bool PettingCall()
        {
            if (!CanPettingCall) return false;
            voice.clip = affectionVoice; voice.volume = .55f; voice.Play();
            affectionCooldown = PettingContact.CooldownSeconds; vocalCooldown = VocalCooldownSeconds; idleVoice = NextIdleDelay();
            Debug.Log("PETTING_AFFECTION_PLAYED " + affectionVoice.name);
            return true;
        }
        void Landed(float speed) { Impact(speed); }
        public bool Impact(float speed)
        {
            if (disposed || !enabled || impacts[0] == null || impactCooldown > 0 || float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 1.5f) return false;
            // Lizard.TerrainImpact blends light/heavy contact between speeds 6 and 14.
            float heavy = Mathf.InverseLerp(6, 14, speed);
            impacts[0].volume = .1f * (1 - heavy); impacts[0].pitch = .9f;
            impacts[1].volume = .4f * heavy; impacts[1].pitch = .7f;
            impacts[2].volume = .2f * heavy; impacts[2].pitch = 1;
            foreach (var source in impacts) if (source.volume > 0) source.Play();
            impactCooldown = .15f;
            return true;
        }
        public bool Vocalize() => PlayVoice(regularVoice, .4f);
        bool PlayVoice(AudioClip clip, float volume)
        {
            // Drop repeat requests; never queue calls or extend the cooldown.
            if (!enabled || disposed || clip == null || vocalCooldown > 0 || voice.isPlaying) return false;
            voice.clip = clip; voice.volume = volume;
            voice.Play(); vocalCooldown = VocalCooldownSeconds; idleVoice = NextIdleDelay();
            return true;
        }
        public void Advance(float seconds)
        {
            if (disposed) return;
            seconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : Mathf.Clamp(seconds, 0, .25f);
            vocalCooldown = Mathf.Max(0, vocalCooldown - seconds);
            affectionCooldown = Mathf.Max(0, affectionCooldown - seconds);
            impactCooldown = Mathf.Max(0, impactCooldown - seconds);
            var intent = motion.Behavior.Intent;
            if (intent == PetIntent.Investigating && lastIntent != intent) Vocalize();
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
            motion.StateChanged -= StateChanged; motion.JumpLaunched -= Launched; motion.Landed -= Landed;
            motion.VoiceIntensity = 0; charge.Stop(); voice.Stop(); foreach (var source in launch) source.Stop(); foreach (var source in impacts) if (source != null) source.Stop();
            Release(root);
        }
    }
}
