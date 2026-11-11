using System;
using UnityEngine;

namespace CyanPreview
{
    // Tracks one fall per pet; upward throws start their drop at the apex.
    public sealed class FallCatchProgress
    {
        readonly float bodyLength;
        float peak, elapsed;
        public float DropDistance { get; private set; }
        public bool Ready { get; private set; }
        public FallCatchProgress(float bodyLength)
        {
            if (float.IsNaN(bodyLength) || float.IsInfinity(bodyLength) || bodyLength <= 0) throw new ArgumentOutOfRangeException(nameof(bodyLength));
            this.bodyLength = bodyLength;
        }
        public void Reset(float height)
        {
            Validate(height); peak = height; elapsed = DropDistance = 0; Ready = false;
        }
        public void Advance(float seconds, float height, float verticalSpeed)
        {
            Validate(seconds); Validate(height); Validate(verticalSpeed);
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            elapsed += Mathf.Min(seconds,.25f);
            peak = Mathf.Max(peak,height); DropDistance = peak-height;
            Ready = elapsed >= .1f && DropDistance >= bodyLength && verticalSpeed < 0;
        }
        static void Validate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
