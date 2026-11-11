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
        PetSocialStatus[] socialStates;
        void AdvanceScuffles(float dt)
        {
            if(manualPair!=null && !scuffles.ContainsKey(manualPair))
            { manualPair=null; manualMessage="Scuffle cancelled because a pet was removed or rebuilt."; socialRefresh=0; }
            foreach(var p in pets)
            {
                if(p.Motion.RivalTarget.HasValue) { p.Motion.Behavior.TaskTarget=p.Feeding.Target; p.Motion.Behavior.HoldPosition=p.Feeding.UsingRoute; }
                p.Motion.RivalTarget=null; p.Motion.RivalBiting=false;
            }
            socialStates=pets.ConvertAll(p=>new PetSocialStatus {key=p.Config.key,state=settings.allowFighting?"Calm":"Fighting off"}).ToArray();
            var engaged=new HashSet<string>();
            for(int i=0;i<pets.Count;i++) for(int j=i+1;j<pets.Count;j++)
            {
                var a=pets[i]; var b=pets[j]; string key=a.Config.key+"|"+b.Config.key;
                if(!scuffles.TryGetValue(key,out var fight)) { fight=new PetScuffle(); scuffles.Add(key,fight); }
                bool eligible=!settings.paused && (manualPair==null || key==manualPair || (!ManualMember(a) && !ManualMember(b))) && food.Owner!=FoodOwner.Placing && !a.Feeding.UsingRoute && !b.Feeding.UsingRoute
                    && a.Motion.State==PetMotionState.Crawling && b.Motion.State==PetMotionState.Crawling
                    && !a.Input.CapturesPointer && !b.Input.CapturesPointer && !engaged.Contains(a.Config.key) && !engaged.Contains(b.Config.key);
                float scale=(a.Config.scale+b.Config.scale)*.5f;
                fight.Advance(dt,settings.allowFighting,eligible,Vector2.Distance(a.WorldPosition,b.WorldPosition)/scale,
                    food.Owner==FoodOwner.Pet || food.Owner==FoodOwner.Loose,
                    (a.Pet.Personality.Aggression+b.Pet.Personality.Aggression)*.5f,
                    Mathf.Abs(a.Pet.Personality.Dominance-b.Pet.Personality.Dominance));
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
                a.Motion.RivalTarget=a.Local(b.WorldPosition); b.Motion.RivalTarget=b.Local(a.WorldPosition);
                bool bite=fight.Phase==ScufflePhase.Bite;
                a.Motion.RivalBiting=b.Motion.RivalBiting=bite;
                bool hold=fight.Phase==ScufflePhase.Bite || fight.Phase==ScufflePhase.Recoil;
                a.Motion.Behavior.HoldPosition=b.Motion.Behavior.HoldPosition=hold;
                a.Motion.Behavior.TaskTarget=a.Local(b.WorldPosition); b.Motion.Behavior.TaskTarget=b.Local(a.WorldPosition);
                a.Motion.AutonomousJumps=b.Motion.AutonomousJumps=false;
                var direction=(b.WorldPosition-a.WorldPosition).normalized;
                if(direction.sqrMagnitude<.1f) direction=Vector2.right;
                if(fight.BiteStarted)
                {
                    a.Motion.ScuffleImpulse(direction*2); b.Motion.ScuffleImpulse(-direction*2);
                    if(food.Owner==FoodOwner.Pet && (food.Holder==a.Config.key || food.Holder==b.Config.key))
                    { var holder=food.Holder==a.Config.key?a:b; food.Drop(Clamp(holder.WorldMouth)); ResetInterest(holder); }
                }
                if(fight.RecoilStarted)
                {
                    a.Motion.ScuffleImpulse(-direction*4); b.Motion.ScuffleImpulse(direction*4);
                    a.Audio?.Vocalize(); b.Audio?.Vocalize();
                }
            }
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
        public static void UpdateSocialStatus(PetSocialStatus status,PetScuffle fight,string rival,bool eligible)
        {
            if(status.active || (!fight.Active && status.tension>fight.Tension)) return;
            if(!fight.Active && fight.Phase==ScufflePhase.Calm && status.state=="Cooldown") return;
            status.active=fight.Active; status.tension=fight.Tension; status.rival=rival;
            status.state=fight.Phase==ScufflePhase.Calm ? (eligible ? "Calm" : "Not engaging") : fight.Phase.ToString();
        }
    }
}
