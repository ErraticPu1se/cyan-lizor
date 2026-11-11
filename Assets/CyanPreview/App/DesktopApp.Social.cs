using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class DesktopApp
    {
        readonly Dictionary<string,PetScuffle> scuffles=new Dictionary<string,PetScuffle>();
        void AdvanceScuffles(float dt)
        {
            foreach(var p in pets)
            {
                if(p.Motion.RivalTarget.HasValue) { p.Motion.Behavior.TaskTarget=p.Feeding.Target; p.Motion.Behavior.HoldPosition=p.Feeding.UsingRoute; }
                p.Motion.RivalTarget=null; p.Motion.RivalBiting=false;
            }
            var engaged=new HashSet<string>();
            for(int i=0;i<pets.Count;i++) for(int j=i+1;j<pets.Count;j++)
            {
                var a=pets[i]; var b=pets[j]; string key=a.Config.key+"|"+b.Config.key;
                if(!scuffles.TryGetValue(key,out var fight)) { fight=new PetScuffle(); scuffles.Add(key,fight); }
                bool eligible=food.Owner!=FoodOwner.Placing && !a.Feeding.UsingRoute && !b.Feeding.UsingRoute
                    && a.Motion.State==PetMotionState.Crawling && b.Motion.State==PetMotionState.Crawling
                    && !a.Input.CapturesPointer && !b.Input.CapturesPointer && !engaged.Contains(a.Config.key) && !engaged.Contains(b.Config.key);
                float scale=(a.Config.scale+b.Config.scale)*.5f;
                fight.Advance(dt,settings.allowFighting,eligible,Vector2.Distance(a.WorldPosition,b.WorldPosition)/scale,
                    food.Owner==FoodOwner.Pet || food.Owner==FoodOwner.Loose,
                    (a.Pet.Personality.Aggression+b.Pet.Personality.Aggression)*.5f,
                    Mathf.Abs(a.Pet.Personality.Dominance-b.Pet.Personality.Dominance));
                if(!fight.Active) continue;
                engaged.Add(a.Config.key); engaged.Add(b.Config.key);
                a.Motion.RivalTarget=a.Local(b.WorldPosition); b.Motion.RivalTarget=b.Local(a.WorldPosition);
                bool bite=fight.Phase==ScufflePhase.Bite;
                a.Motion.RivalBiting=b.Motion.RivalBiting=bite;
                bool hold=fight.Phase!=ScufflePhase.Posture;
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
        }
    }
}
