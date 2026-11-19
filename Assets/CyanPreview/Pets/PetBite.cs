using System;
using UnityEngine;
namespace CyanPreview
{
    public enum BiteDecision { None, Home, Hit, Miss }
    public sealed class BiteTuning
    {
        public readonly float Forward, RadiusBonus, AttemptRadius, Homing, Chance;
        public readonly int DelayTicks;
        public readonly float LungeRange=130, LungeSpeed=4.5f;
        public readonly int PreparationTicks=35, LungeTicks=20, PropulsionTicks=10, RecoveryTicks=40;
        public BiteTuning(float forward,float radiusBonus,float attemptRadius,float homing,float chance,int delayTicks)
        { Forward=forward; RadiusBonus=radiusBonus; AttemptRadius=attemptRadius; Homing=homing; Chance=chance; DelayTicks=delayTicks; }
    }
    // One jaw and random stream per runtime pet, on the same 40 Hz clock as locomotion.
    public sealed class PetBite
    {
        readonly System.Random random;
        readonly BiteTuning tuning;
        float clock;
        int delay, snap, recovery, preparation, lunge, closeAttempts;
        readonly int preparationTicks;
        public float Jaw { get; private set; }
        public int HomingTicks { get; private set; }
        public int CooldownTicks => delay;
        public bool Snapping => snap>0;
        public bool Recovering => recovery>0;
        public bool Threatening => preparation>0;
        // A crouching pet may pull back without repeatedly cancelling at the range boundary.
        public float EngagementRange => (tuning?.LungeRange??0)*(Threatening?1.35f:1);
        public bool Lunging => lunge>0;
        public bool Propelling => tuning!=null && lunge>tuning.LungeTicks-tuning.PropulsionTicks;
        public PetBite(int seed,BiteTuning tuning,float aggression=.5f)
        {
            random=new System.Random(seed); this.tuning=tuning;
            preparationTicks=(tuning?.PreparationTicks??35)+(int)(random.NextDouble()*12*(1-Mathf.Clamp01(aggression)));
        }
        public void Cancel() { HomingTicks=0; Jaw=0; preparation=0; lunge=0; snap=0; closeAttempts=0; }
        public void ReceiveHit() { recovery=tuning?.RecoveryTicks??40; Cancel(); }
        public BiteDecision Advance(float seconds,bool engaged,bool canBite,bool near,bool contact,bool aligned=true,bool canLunge=true,bool canHome=false)
        {
            HomingTicks=0;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0) return BiteDecision.None;
            clock+=Mathf.Min(seconds,.1f); BiteDecision decision=BiteDecision.None;
            while(clock>=.025f)
            {
                clock-=.025f; if(delay>0) delay--; if(snap>0) snap--; if(recovery>0) recovery--;
                if(!engaged || !canBite || tuning==null) { Cancel(); continue; }
                if(recovery>0 || delay>0) { Jaw=0; preparation=0; lunge=0; continue; }
                if(!canLunge) { lunge=0; preparation=0; }
                if(lunge>0)
                {
                    lunge--; Jaw=1;
                    if(contact && random.NextDouble()<tuning.Chance) { decision=BiteDecision.Hit; Snap(); }
                    else if(lunge==0) { decision=BiteDecision.Miss; Snap(); }
                    continue;
                }
                // AttemptBite is independent of Lounge: actual mouth contact can snap a ready jaw.
                if(contact)
                {
                    preparation=0; Jaw=Mathf.Min(1,Jaw+.05f);
                    if(Jaw>.9f)
                    {
                        if(random.NextDouble()<tuning.Chance) { decision=BiteDecision.Hit; Snap(false); }
                        else if(++closeAttempts>=6) { decision=BiteDecision.Miss; Snap(false); }
                    }
                    continue;
                }
                closeAttempts=0;
                if(!near) { preparation=0; Jaw=Mathf.Max(0,Jaw-.05f); continue; }
                // Ready the jaw on approach, including flight; only a grounded, aligned pet lunges.
                Jaw=Mathf.Min(1,Jaw+.05f);
                if(canHome && canLunge && aligned && Jaw>.9f && decision!=BiteDecision.Hit && decision!=BiteDecision.Miss)
                { HomingTicks++; decision=BiteDecision.Home; }
                if(!canLunge || !aligned) { preparation=0; continue; }
                preparation++;
                if(preparation>=preparationTicks) { preparation=0; lunge=tuning.LungeTicks; HomingTicks=0; if(decision==BiteDecision.Home) decision=BiteDecision.None; }
            }
            return decision;
        }
        void Snap(bool afterLunge=true) { HomingTicks=0; Jaw=0; delay=tuning.DelayTicks; snap=5; lunge=0; preparation=0; recovery=afterLunge?tuning.RecoveryTicks:0; closeAttempts=0; }
    }
}
