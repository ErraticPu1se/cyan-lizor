using System;
using UnityEngine;

namespace CyanPreview
{
    public sealed class LandingTuning
    {
        public readonly float MinSeconds, MaxSeconds, Compression, Damping;
        public LandingTuning(float minSeconds, float maxSeconds, float compression, float damping)
        {
            foreach (float value in new[] { minSeconds, maxSeconds, compression, damping })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (minSeconds < 0 || maxSeconds < minSeconds || maxSeconds > 1 || compression < 0 || compression > 6 || damping < 0 || damping > 1)
                throw new ArgumentOutOfRangeException(nameof(minSeconds));
            MinSeconds = minSeconds; MaxSeconds = maxSeconds; Compression = compression; Damping = damping;
        }
    }

    // Per-pet physical response; species share only immutable tuning.
    public sealed class LandingRecovery
    {
        readonly LandingTuning tuning;
        float elapsed, duration, strength;
        public bool Active { get; private set; }
        public Vector2 Normal { get; private set; }
        public Vector2 ContactPoint { get; private set; }
        public float Weight => Active ? strength * (1 - Mathf.SmoothStep(0, 1, elapsed / duration)) : 0;
        public float Compression => tuning.Compression * Weight;
        public float DriveScale => 1 - Weight;
        public float VelocityRetention => Mathf.Lerp(.75f, tuning.Damping, Weight);
        public LandingRecovery(LandingTuning tuning) { this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning)); }
        static bool Finite(Vector2 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y));
        public void Begin(float speed, Vector2 normal, Vector2 point)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0 || !Finite(normal) || !Finite(point)) throw new ArgumentOutOfRangeException(nameof(speed));
            Cancel();
            if (speed <= 1.5f || tuning.MaxSeconds == 0) return;
            strength = Mathf.InverseLerp(1.5f, 14, speed);
            duration = Mathf.Lerp(tuning.MinSeconds, tuning.MaxSeconds, strength);
            Normal = normal.normalized; ContactPoint = point; Active = duration > 0;
        }
        public void Advance(float seconds, int grips, float bodySpeed)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || float.IsNaN(bodySpeed) || float.IsInfinity(bodySpeed) || bodySpeed < 0 || grips < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Active) return;
            elapsed += Mathf.Min(seconds, .25f);
            if (elapsed >= duration || (elapsed >= tuning.MinSeconds && Weight < .15f && grips >= 2 && bodySpeed < 1.5f)) Cancel();
        }
        public void Shift(Vector2 delta)
        {
            if (!Finite(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            if (Active) ContactPoint += delta;
        }
        public void Cancel() { Active = false; elapsed = duration = strength = 0; Normal = ContactPoint = Vector2.zero; }
    }
}
