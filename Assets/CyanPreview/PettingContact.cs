using System;
using UnityEngine;
namespace CyanPreview
{
    // One contact timer per pet; no species, audio or desktop dependencies.
    public sealed class PettingContact
    {
        public const float CooldownSeconds = 3;
        float contact, cooldown, missed;
        public void ResetContact() { contact = missed = 0; }
        public bool Advance(float seconds, bool touching, bool canRespond = true)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            seconds = Mathf.Min(seconds, .25f);
            cooldown = Mathf.Max(0, cooldown - seconds);
            if (!touching)
            {
                missed += seconds;
                if (missed > .15f) contact = 0;
                return false;
            }
            missed = 0;
            contact = Mathf.Min(1, contact + seconds);
            if (contact < 1 || cooldown > 0 || !canRespond) return false;
            contact = 0; cooldown = CooldownSeconds; return true;
        }
    }
}
