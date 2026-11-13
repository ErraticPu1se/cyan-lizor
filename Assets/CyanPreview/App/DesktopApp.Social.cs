using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    [Serializable] public sealed class PetSocialStatus
    {
        public string key, state, rival;
        public float tension;
        public bool active;
    }
    [Serializable] public sealed class DesktopSocialStatus { public PetSocialStatus[] pets; public string message; public int request; }
    public sealed partial class DesktopApp
    {
        readonly Dictionary<string,PetScuffle> scuffles=new Dictionary<string,PetScuffle>();
        float socialRefresh;
        int handledScuffleRequest;
        string manualPair, manualMessage;
        bool ManualMember(DesktopPet pet) => manualPair!=null && ("|"+manualPair+"|").Contains("|"+pet.Config.key+"|");
        void ProcessScuffleRequest()
        {
            if(settings.scuffleRequest==handledScuffleRequest) return;
            handledScuffleRequest=settings.scuffleRequest; socialRefresh=0;
            var a=pets.Find(p=>p.Config.key==settings.scuffleFirst);
            var b=pets.Find(p=>p.Config.key==settings.scuffleSecond);
            if(!settings.allowFighting || settings.paused) { manualMessage="Enable fighting and resume the pets first."; return; }
            if(a==null || b==null || a==b) { manualMessage="Choose two different active pets."; return; }
            if(manualPair!=null) { manualMessage="A test scuffle is already running."; return; }
            if(food.Owner==FoodOwner.Placing || a.Feeding.UsingRoute || b.Feeding.UsingRoute || a.Input.CapturesPointer || b.Input.CapturesPointer
                || a.Motion.State!=PetMotionState.Crawling || b.Motion.State!=PetMotionState.Crawling)
            { manualMessage="Wait until both pets are on a surface, released, and back from their den."; return; }
            foreach(var pair in scuffles)
                if((pair.Key==a.Config.key+"|"+b.Config.key || pair.Key==b.Config.key+"|"+a.Config.key || ("|"+pair.Key+"|").Contains("|"+a.Config.key+"|") || ("|"+pair.Key+"|").Contains("|"+b.Config.key+"|"))
                    && (pair.Value.Active || pair.Value.Phase==ScufflePhase.Cooldown))
                { manualMessage="One selected pet is fighting or cooling down. Try again shortly."; return; }
            if(pets.IndexOf(a)>pets.IndexOf(b)) { var swap=a; a=b; b=swap; }
            string key=a.Config.key+"|"+b.Config.key;
            if(!scuffles.TryGetValue(key,out var fight)) { fight=new PetScuffle(); scuffles.Add(key,fight); }
            if(!fight.TryStartManual()) { manualMessage="Wait for this pair's cooldown."; return; }
            a.Motion.BeginIdleHold(); a.Motion.Behavior.HoldPosition=false;
            b.Motion.BeginIdleHold(); b.Motion.Behavior.HoldPosition=false;
            manualPair=key; manualMessage="Approaching: "+a.Config.name+" and "+b.Config.name;
        }
        bool CanRemainInScuffle(DesktopPet pet,bool carrierCompetition)
        {
            if(!PetContacts.Available(pet)) return false;
            if(pet.Motion.State==PetMotionState.Crawling) return true;
            // Keep both pursuit intentions through flight; actual bites still require a grip.
            return carrierCompetition
                && (pet.Motion.State==PetMotionState.Charging || pet.Motion.State==PetMotionState.Airborne);
        }
        PetSocialStatus[] socialStates;
        void AdvanceScuffles(float dt)
        {
            if(manualPair!=null && !scuffles.ContainsKey(manualPair))
            { manualPair=null; manualMessage="Scuffle cancelled because a pet was removed or rebuilt."; socialRefresh=0; }
            foreach(var p in pets)
            {
                if(p.Motion.RivalTarget.HasValue) { p.Motion.Behavior.TaskTarget=p.Feeding.Target; p.Motion.Behavior.HoldPosition=p.Feeding.UsingRoute; }
                p.Motion.Behavior.TaskRunSpeed=null;
                p.Motion.RivalTarget=null; p.Motion.RivalPursuingCarrier=false; p.Motion.RivalBiting=false; p.Motion.RivalStance=false; p.Motion.RivalPropelling=false; p.Motion.RivalRecovering=false;
            }
            socialStates=pets.ConvertAll(p=>new PetSocialStatus {key=p.Config.key,state=settings.allowFighting?"Calm":"Fighting off"}).ToArray();
            var engaged=new HashSet<string>();
            for(int i=0;i<pets.Count;i++) for(int j=i+1;j<pets.Count;j++)
            {
                var a=pets[i]; var b=pets[j]; string key=a.Config.key+"|"+b.Config.key;
                if(!scuffles.TryGetValue(key,out var fight)) { fight=new PetScuffle(); scuffles.Add(key,fight); }
                bool carrierCompetition=food.Owner==FoodOwner.Pet && (food.Holder==a.Config.key || food.Holder==b.Config.key);
                bool eligible=(food.Owner!=FoodOwner.Pet || carrierCompetition || key==manualPair) && !settings.paused && (manualPair==null || key==manualPair || (!ManualMember(a) && !ManualMember(b))) && food.Owner!=FoodOwner.Placing && !a.Feeding.UsingRoute && !b.Feeding.UsingRoute
                    && CanRemainInScuffle(a,carrierCompetition) && CanRemainInScuffle(b,carrierCompetition)
                    && !a.Input.CapturesPointer && !b.Input.CapturesPointer && !engaged.Contains(a.Config.key) && !engaged.Contains(b.Config.key);
                float scale=(a.Config.scale+b.Config.scale)*.5f;
                fight.Advance(dt,settings.allowFighting,eligible,Mathf.Min(Vector2.Distance(a.WorldPosition,PetContacts.TargetPoint(a,b)),Vector2.Distance(b.WorldPosition,PetContacts.TargetPoint(b,a)))/scale,
                    food.Owner==FoodOwner.Pet || food.Owner==FoodOwner.Loose,
                    (a.Pet.Personality.Aggression+b.Pet.Personality.Aggression)*.5f,
                    Mathf.Abs(a.Pet.Personality.Dominance-b.Pet.Personality.Dominance),carrierCompetition);
                if(key==manualPair && fight.Active) manualMessage="Test scuffle: "+fight.Phase;
                if(key==manualPair && !fight.Active)
                {
                    manualMessage=fight.Outcome??(eligible && settings.allowFighting?"Scuffle finished. Cooling down.":"Scuffle cancelled: a pet became unavailable or fighting was disabled.");
                    manualPair=null; socialRefresh=0;
                }
                if(settings.allowFighting)
                {
                    UpdateSocialStatus(socialStates[i],fight,b.Config.name,eligible);
                    UpdateSocialStatus(socialStates[j],fight,a.Config.name,eligible);
                }
                if(!fight.Active) continue;
                engaged.Add(a.Config.key); engaged.Add(b.Config.key);
                var targetA=PetContacts.TargetPoint(a,b); var targetB=PetContacts.TargetPoint(b,a);
                a.Motion.RivalTarget=a.Local(targetA); b.Motion.RivalTarget=b.Local(targetB);
                int contactA=PetContacts.BiteContact(a,b,out bool nearA), contactB=PetContacts.BiteContact(b,a,out bool nearB);
                nearA=Vector2.Distance(a.WorldPosition,targetA)<a.Bite.EngagementRange*a.Config.scale;
                nearB=Vector2.Distance(b.WorldPosition,targetB)<b.Bite.EngagementRange*b.Config.scale;
                bool canA=food.Holder!=a.Config.key && PetContacts.Available(a);
                bool canB=food.Holder!=b.Config.key && PetContacts.Available(b);
                // Decide both jaws from the same contact snapshot; order never chooses a winner.
                var decisionA=a.Bite.Advance(dt,true,canA,nearA,contactA>=0,a.Motion.FacingRival(a.Local(targetA)),a.Motion.State==PetMotionState.Crawling && a.Motion.GripCount>0, Vector2.Distance(a.WorldPosition,targetA)<=a.Pet.Species.Biting.AttemptRadius*a.Config.scale);
                var decisionB=b.Bite.Advance(dt,true,canB,nearB,contactB>=0,b.Motion.FacingRival(b.Local(targetB)),b.Motion.State==PetMotionState.Crawling && b.Motion.GripCount>0, Vector2.Distance(b.WorldPosition,targetB)<=b.Pet.Species.Biting.AttemptRadius*b.Config.scale);
                ApplyBite(a,b,decisionA,contactA); ApplyBite(b,a,decisionB,contactB);
                ConfigureRival(a,b,nearA,contactA>=0); ConfigureRival(b,a,nearB,contactB>=0);
                socialStates[i].state=BiteStatus(a,fight); socialStates[j].state=BiteStatus(b,fight);
            }
            foreach(var pet in pets) if(!engaged.Contains(pet.Config.key))
            { pet.Bite.Advance(dt,false,false,false,false); pet.Bite.Cancel(); pet.Motion.RivalLunging=false; }

            socialRefresh-=dt;
            if(socialRefresh<=0 && !string.IsNullOrEmpty(settingsFile))
            {
                socialRefresh=.2f;
                try
                {
                    string path=settingsFile+".social", temp=path+".tmp";
                    File.WriteAllText(temp,JsonUtility.ToJson(new DesktopSocialStatus {pets=socialStates,message=manualMessage,request=handledScuffleRequest}));
                    if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
                }
                catch(IOException) { }
            }
        }
        static string BiteStatus(DesktopPet pet,PetScuffle fight) => pet.Bite.Snapping?"Bite":pet.Bite.Recovering?"Recoil":pet.Bite.Lunging?"Lunge":pet.Bite.Threatening?"Threat":fight.Phase.ToString();
        void ConfigureRival(DesktopPet pet,DesktopPet rival,bool near,bool contact)
        {
            bool carrying=food.Owner==FoodOwner.Pet && food.Holder==pet.Config.key;
            if(carrying)
            {
                // ReturnPrey owns the carrier's destination; its occupied mouth cannot attack.
                pet.Motion.RivalTarget=null; pet.Motion.RivalJaw=0; pet.Motion.RivalBiting=false;
                pet.Motion.RivalStance=pet.Motion.RivalLunging=pet.Motion.RivalPropelling=pet.Motion.RivalRecovering=false;
                pet.Motion.Behavior.TaskRunSpeed=null;
                pet.Motion.Behavior.TaskTarget=pet.Feeding.Target;
                pet.Motion.Behavior.HoldPosition=pet.Feeding.UsingRoute;
                pet.Motion.AutonomousJumps=false;
                return;
            }
            bool pursuingCarrier=food.Owner==FoodOwner.Pet && food.Holder==rival.Config.key;
            pet.Motion.RivalPursuingCarrier=pursuingCarrier;
            var target=pet.Local(PetContacts.TargetPoint(pet,rival));
            pet.Motion.RivalJaw=pet.Bite.Jaw; pet.Motion.RivalBiting=pet.Bite.Snapping;
            if(pet.Bite.Lunging && !pet.Motion.RivalLunging)
                pet.Motion.RivalLungeDirection=(target-pet.Motion.Position).normalized;
            pet.Motion.RivalLunging=pet.Bite.Lunging;
            pet.Motion.RivalPropelling=pet.Bite.Propelling;
            pet.Motion.RivalRecovering=pet.Bite.Recovering;
            pet.Motion.RivalStance=pet.Bite.Threatening && !pursuingCarrier;
            pet.Motion.Behavior.HoldPosition=pet.Bite.Recovering || pet.Bite.Lunging || (!pursuingCarrier && (pet.Bite.Threatening || contact));
            pet.Motion.Behavior.TaskRunSpeed=PetContacts.ApproachSpeed(pet,rival);
            pet.Motion.Behavior.TaskTarget=target;
            pet.Motion.AutonomousJumps=pursuingCarrier && pet.Config.jumps && !pet.Motion.Behavior.HoldPosition;
        }
        void ApplyBite(DesktopPet attacker,DesktopPet defender,BiteDecision decision,int contact)
        {
            if(decision==BiteDecision.None) return;
            var target=contact>=0?defender.Motion.BodyPosition(contact)*defender.Config.scale:PetContacts.TargetPoint(attacker,defender);
            if(decision==BiteDecision.Home) { attacker.Motion.BiteHoming(attacker.Local(target),attacker.Pet.Species.Biting.Homing*attacker.Bite.HomingTicks); return; }
            if(!attacker.Motion.BiteToward(attacker.Local(target))) return;
            bool hit=decision==BiteDecision.Hit && contact>=0 && PetContacts.Available(defender);
            attacker.Audio?.Bite(hit);
            if(!hit) return;
            var direction=(target-attacker.WorldPosition).normalized;
            defender.Motion.ContactCorrection(contact,Vector2.zero,direction*5/defender.Config.scale);
            defender.Bite.ReceiveHit();
            if(food.DropFromBite(defender.Config.key,Clamp(defender.WorldMouth))) ResetInterest(defender);
        }
        void ResolvePetContacts(float dt)
        {
            for(int i=0;i<pets.Count;i++) for(int j=i+1;j<pets.Count;j++) PetContacts.Resolve(pets[i],pets[j],dt);
        }
        public static void UpdateSocialStatus(PetSocialStatus status,PetScuffle fight,string rival,bool eligible)
        {
            if(status.active || (!fight.Active && status.tension>fight.Tension)) return;
            if(!fight.Active && fight.Phase==ScufflePhase.Calm && status.state=="Cooldown") return;
            status.active=fight.Active; status.tension=fight.Tension; status.rival=rival;
            status.state=fight.Phase==ScufflePhase.Calm ? (eligible ? "Calm" : "Not engaging") : fight.Phase.ToString();
        }
    }
}
