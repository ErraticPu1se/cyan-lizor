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
        public bool DropFromBite(string victim,Vector2 position)
        { if(Owner!=FoodOwner.Pet || Holder!=victim) return false; Drop(position); return true; }
        public bool EnterDen(string pet)
        { if(Owner!=FoodOwner.Pet || Holder!=pet) return false; Owner=FoodOwner.Den; Revision++; return true; }
        public void Remove(string pet,Vector2 drop)
        { if(Holder!=pet) return; if(Owner==FoodOwner.Pet) Drop(drop); else Cancel(); }
        public void Cancel() { Owner=FoodOwner.None; Holder=null; Revision++; }
    }
    public enum ScufflePhase { Calm, Posture, Bite, Recoil, Cooldown, Approaching }
    // Pair-local tension and bounded, non-lethal encounters. No damage/health API exists here.
    public sealed class PetScuffle
    {
        public ScufflePhase Phase { get; private set; }
        public float Tension { get; private set; }
        public bool Active => Phase==ScufflePhase.Approaching || Phase==ScufflePhase.Posture;
        float remaining, engagement;
        public string Outcome { get; private set; }
        public bool TryStartManual()
        {
            if(Active || Phase==ScufflePhase.Cooldown) return false;
            Phase=ScufflePhase.Approaching; remaining=20; engagement=0; Tension=1; Outcome=null; return true;
        }
        public void Interrupt() { Phase=ScufflePhase.Cooldown; remaining=8; Tension=0; }
        public void Advance(float seconds,bool allowed,bool eligible,float distance,bool competing,float aggression,float dominanceDifference,bool carrierCompetition=false)
        {
            bool validTime=!float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds>0;
            float dt=validTime?Mathf.Min(seconds,.1f):0;
            // Rest time elapses even when a pet is unavailable; pause supplies zero time.
            if(Phase==ScufflePhase.Cooldown) { remaining-=dt; if(remaining<=0) Phase=ScufflePhase.Calm; return; }
            if(!allowed || !eligible) { if(Active) Interrupt(); else Tension=0; return; }
            if(!validTime) return;
            if(Phase==ScufflePhase.Calm)
            {
                // A visible food carrier is an immediate chase, not another tension waiting period.
                if(carrierCompetition) { TryStartManual(); return; }
                Tension=Mathf.Clamp01(Tension+(distance<160?(competing?.35f:.035f)*Mathf.Lerp(.4f,1.6f,Mathf.Clamp01(aggression)):-.2f)*dt);
                if(Tension>=1) { Phase=ScufflePhase.Approaching; remaining=20; engagement=0; }
                return;
            }
            if(distance<=90 || engagement>0)
            {
                engagement+=dt; Phase=distance<=90?ScufflePhase.Posture:ScufflePhase.Approaching;
                // Desktop safety limit, independent of number/order of successful bites.
                if(engagement>=8) { Interrupt(); Outcome="Encounter finished. Cooling down."; }
            }
            else if((remaining-=dt)<=0) { Interrupt(); Outcome="Could not reach each other. Move them closer and retry after cooldown."; }
        }
    }
}
