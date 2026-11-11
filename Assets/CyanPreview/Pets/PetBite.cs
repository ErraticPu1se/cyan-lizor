using System;
using UnityEngine;
namespace CyanPreview
{
    public enum BiteDecision { None, Home, Hit, Miss }
    public sealed class BiteTuning
    {
        public readonly float Forward, RadiusBonus, AttemptRadius, Homing, Chance;
        public readonly int DelayTicks;
        public BiteTuning(float forward,float radiusBonus,float attemptRadius,float homing,float chance,int delayTicks)
        { Forward=forward; RadiusBonus=radiusBonus; AttemptRadius=attemptRadius; Homing=homing; Chance=chance; DelayTicks=delayTicks; }
    }
    // One jaw and random stream per runtime pet, on the same 40 Hz clock as locomotion.
    public sealed class PetBite
    {
        readonly System.Random random;
        readonly BiteTuning tuning;
        float clock;
        int delay, snap, recovery, attempts;
        public float Jaw { get; private set; }
        public bool Snapping => snap>0;
        public bool Recovering => recovery>0;
        public PetBite(int seed,BiteTuning tuning) { random=new System.Random(seed); this.tuning=tuning; }
        public void Cancel() { Jaw=0; attempts=0; snap=0; }
        public void ReceiveHit() { recovery=8; Jaw=0; attempts=0; }
        public BiteDecision Advance(float seconds,bool engaged,bool canBite,bool near,bool contact)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0) return BiteDecision.None;
            clock+=Mathf.Min(seconds,.1f); BiteDecision decision=BiteDecision.None;
            while(clock>=.025f)
            {
                clock-=.025f; if(delay>0) delay--; if(snap>0) snap--; if(recovery>0) recovery--;
                if(!engaged || !canBite || recovery>0) { Jaw=0; attempts=0; continue; }
                if(delay>0) { Jaw=0; continue; }
                if(!near) { Jaw=Mathf.Max(0,Jaw-.05f); attempts=0; continue; }
                Jaw=Mathf.Min(1,Jaw+.05f);
                if(Jaw<=.9f) continue;
                if(contact && random.NextDouble()<tuning.Chance)
                { decision=BiteDecision.Hit; Snap(); }
                else if(++attempts>5) { decision=BiteDecision.Miss; Snap(); }
                else if(decision==BiteDecision.None) decision=BiteDecision.Home;
            }
            return decision;
        }
        void Snap() { Jaw=0; delay=tuning.DelayTicks; snap=5; attempts=0; }
    }
}
