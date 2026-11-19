using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    sealed class WhiteSpecies : LizardSpecies
    {
        // Motion settings are immutable; each pet owns its simulation state.
        public override MotionTuning Motion { get; } =
            new MotionTuning(new AttentionTuning(
                deliberateSmoothing: .3f,
                deliberateDegreesPerTick: 12f)
        );
        public override string Id => "white";
        public override bool CanCamouflage => true;
        public override bool CanTongueFeed => true;
        public override float RestPreference => 1;
        public override string DisplayName => "White lizard";
        public override MovementCapabilities Movement {get;} = new MovementCapabilities(true,2.1f,climbPoles:true,climbBackgrounds:true);
        public override BiteTuning Biting {get;} = new BiteTuning(25,0,85,1.7f,.5f,15);
        public override string[] ImpactSounds {get;} = new[]{"thuds1A","bassOnly","thuds1E"};
        public override string VoiceSound(int seed) => "LizardVoiceWhite"+"ABC"[(int)((uint)seed%3)];
        public override string AffectionSound(int seed) => "LizardLoveVoiceGen"+"AB"[(int)((uint)seed%2)];
        internal override object CreateLizard(ReferenceContext context,int spawner,int seed) => LizardBodyFactory.Create(context,spawner,seed,true);
    }
}
