using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    sealed class CyanSpecies : LizardSpecies
    {
        // Motion settings are immutable; each pet owns its simulation state.
        public override MotionTuning Motion { get; } = new MotionTuning();
        public override string Id => "cyan";
        public override string VoiceSound(int seed) => "LizardVoiceCyan"+"ABC"[(int)((uint)seed%3)];
        // Installed cyan breed values; damage fields deliberately omitted.
        public override BiteTuning Biting { get; } = new BiteTuning(25,0,90,1.4f,.46511626f,12);
        public override LandingTuning Landing { get; } = new LandingTuning(.15f, .35f, 5, .55f);
        public override string[] ImpactSounds { get; } = new[] { "thuds1A", "bassOnly", "thuds1E" };
        public override string AffectionSound(int seed) => "LizardLoveVoiceCyan" + "ABC"[(int)((uint)seed % 3)];
        public override string DisplayName => "Cyan lizard";
        public override float HeadLightRadius => 100f;
        public override MovementCapabilities Movement { get; } = new MovementCapabilities(true, 2.4f, 14f, 50f, climbPoles:true, climbBackgrounds:true);
        internal override object CreateLizard(ReferenceContext context, int spawner, int seed) => LizardBodyFactory.Create(context,spawner,seed,false);
    }
}
