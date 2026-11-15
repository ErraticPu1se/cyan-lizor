using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public abstract class LizardSpecies
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public virtual bool CanCamouflage => false;
        public virtual bool CanTongueFeed => false;
        public virtual float RestPreference => 0;
        public static readonly string[] AvailableIds = { "cyan", "white" };
        public virtual float HeadLightRadius => 0;
        public virtual BiteTuning Biting => null;
        public virtual string VoiceSound(int seed) => null;
        public virtual string AffectionSound(int seed) => null;
        public virtual string[] ImpactSounds => null;
        public virtual LandingTuning Landing { get; } = new LandingTuning(.15f, .3f, 4, .6f);
        public virtual MotionTuning Motion { get; } = new MotionTuning();
        public abstract MovementCapabilities Movement { get; }
        internal abstract object CreateLizard(ReferenceContext context, int spawner, int seed);
        static readonly LizardSpecies cyan = new CyanSpecies(), white = new WhiteSpecies();
        public static LizardSpecies Find(string id)
        {
            if (id == cyan.Id) return cyan;
            if (id == white.Id) return white;
            throw new NotSupportedException("Unsupported lizard species: " + id);
        }
    }






}
