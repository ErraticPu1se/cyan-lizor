using System;
using UnityEngine;
namespace CyanPreview
{
    public enum FoodOwner { None, Placing, Loose, Cursor, Pet, Den }
    // One authoritative owner; interest is maintained independently by every pet.
    public sealed class SharedFood
    {
        public FoodOwner Owner { get; private set; }
        public string Holder { get; private set; }
        public Vector2 Position { get; private set; }
        public int Revision { get; private set; }
        public bool Available => Owner==FoodOwner.Loose;
        public bool Begin() { if(Owner!=FoodOwner.None) return false; Owner=FoodOwner.Placing; return true; }
        public void Move(Vector2 p) { if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.x)||float.IsInfinity(p.y)) throw new ArgumentOutOfRangeException(nameof(p)); Position=p; }
        public void Place(Vector2 p) { if(Owner!=FoodOwner.Placing) return; Move(p); Owner=FoodOwner.Loose; Revision++; }
        public bool Take(string pet)
        { if(!Available || string.IsNullOrEmpty(pet)) return false; Holder=pet; Owner=FoodOwner.Pet; return true; }
        public bool TakeCursor() { if(!Available) return false; Owner=FoodOwner.Cursor; Holder=null; Revision++; return true; }
        public void Drop(Vector2 p)
        { if(Owner!=FoodOwner.Pet && Owner!=FoodOwner.Cursor) return; Move(p); Holder=null; Owner=FoodOwner.Loose; Revision++; }
        public bool EnterDen(string pet)
        { if(Owner!=FoodOwner.Pet || Holder!=pet) return false; Owner=FoodOwner.Den; Revision++; return true; }
        public void Remove(string pet,Vector2 drop)
        { if(Holder!=pet) return; if(Owner==FoodOwner.Pet) Drop(drop); else Cancel(); }
        public void Cancel() { Owner=FoodOwner.None; Holder=null; Revision++; }
    }
    public enum ScufflePhase { Calm, Posture, Bite, Recoil, Cooldown }
    // Pair-local tension and bounded, non-lethal encounters. No damage/health API exists here.
    public sealed class PetScuffle
    {
        public ScufflePhase Phase { get; private set; }
        public float Tension { get; private set; }
        public bool Active => Phase==ScufflePhase.Posture || Phase==ScufflePhase.Bite || Phase==ScufflePhase.Recoil;
        public bool BiteStarted { get; private set; }
        public bool RecoilStarted { get; private set; }
        float remaining; int exchanges;
        public void Interrupt() { Phase=ScufflePhase.Cooldown; remaining=8; Tension=0; BiteStarted=RecoilStarted=false; }
        public void Advance(float seconds,bool allowed,bool eligible,float distance,bool competing,float aggression,float dominanceDifference)
        {
            BiteStarted=RecoilStarted=false;
            if(!allowed || !eligible) { if(Active) Interrupt(); else Tension=0; return; }
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0) return;
            float dt=Mathf.Min(seconds,.1f);
            if(Phase==ScufflePhase.Cooldown) { remaining-=dt; if(remaining<=0) Phase=ScufflePhase.Calm; return; }
            if(Phase==ScufflePhase.Calm)
            {
                Tension=Mathf.Clamp01(Tension+(distance<160?(competing?.35f:.035f)*Mathf.Lerp(.4f,1.6f,Mathf.Clamp01(aggression)):-.2f)*dt);
                if(Tension>=1) { Phase=ScufflePhase.Posture; remaining=.9f; exchanges=0; }
                return;
            }
            if(distance>260) { Interrupt(); return; }
            remaining-=dt;
            if(Phase==ScufflePhase.Posture && remaining<=0)
            {
                if(distance>65) { Interrupt(); return; }
                Phase=ScufflePhase.Bite; remaining=.22f; BiteStarted=true;
            }
            else if(Phase==ScufflePhase.Bite && remaining<=0) { Phase=ScufflePhase.Recoil; remaining=.55f; RecoilStarted=true; }
            else if(Phase==ScufflePhase.Recoil && remaining<=0)
            {
                exchanges++;
                if(exchanges>=2 || dominanceDifference>.45f) Interrupt();
                else { Phase=ScufflePhase.Posture; remaining=.65f; }
            }
        }
    }
}
