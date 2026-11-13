using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public sealed class PetSocialPerception
    {
        public const float SightBodyLengths=7,VoiceBodyLengths=12,FootstepBodyLengths=5,SpacingBodyLengths=1.5f;
        sealed class MotionObservation { public Vector2 position; public float body,speed; }
        readonly Dictionary<string,MotionObservation> movement=new Dictionary<string,MotionObservation>();
        readonly Dictionary<string,uint> calls=new Dictionary<string,uint>(),steps=new Dictionary<string,uint>();
        readonly HashSet<string> live=new HashSet<string>(),heard=new HashSet<string>(),stepped=new HashSet<string>();
        readonly List<string> removed=new List<string>();
        static bool Available(DesktopPet p) => !p.Feeding.Active && !p.Feeding.Frozen && !p.FollowingFood &&
            !p.Motion.RivalTarget.HasValue && !p.Motion.Behavior.HoldPosition && !p.Motion.Behavior.TaskTarget.HasValue && (p.Motion.State==PetMotionState.Crawling || p.Motion.State==PetMotionState.Grabbed);
        static bool Observable(DesktopPet p) => !p.Feeding.Frozen && !p.Feeding.UsingRoute;
        public void Advance(IReadOnlyList<DesktopPet> pets,bool paused,float dt=.025f)
        {
            live.Clear(); heard.Clear(); stepped.Clear();
            dt=float.IsNaN(dt)||float.IsInfinity(dt)?0:Mathf.Clamp(dt,0,.25f);
            foreach(var p in pets)
            {
                live.Add(p.Config.key);
                float body=p.Motion.BodyLength*p.Config.scale;
                if(!movement.TryGetValue(p.Config.key,out var observed))
                { observed=new MotionObservation {position=p.WorldPosition,body=body}; movement.Add(p.Config.key,observed); }
                if(paused || dt<=0 || Mathf.Abs(observed.body-body)>.001f) observed.speed=0;
                else
                {
                    float speed=Mathf.Clamp(Vector2.Distance(observed.position,p.WorldPosition)/Mathf.Max(1,body)/dt-.08f,0,6);
                    observed.speed=Mathf.Lerp(observed.speed,speed,1-Mathf.Exp(-(speed>observed.speed?8:3)*dt));
                }
                observed.position=p.WorldPosition; observed.body=body;
                uint revision=p.Audio?.VocalRevision??0;
                if(calls.TryGetValue(p.Config.key,out var previous) && revision!=previous && revision!=0 && !paused) heard.Add(p.Config.key);
                calls[p.Config.key]=revision;
                uint step=p.Motion.FootstepRevision;
                if(steps.TryGetValue(p.Config.key,out var previousStep) && step!=previousStep && step!=0 && !paused) stepped.Add(p.Config.key);
                steps[p.Config.key]=step;
            }
            removed.Clear(); foreach(var key in calls.Keys) if(!live.Contains(key)) removed.Add(key);
            foreach(var key in removed) { calls.Remove(key); steps.Remove(key); movement.Remove(key); }
            foreach(var listener in pets)
            {
                var social=listener.Motion.Behavior.Social;
                if(social.Source!=null && !live.Contains(social.Source)) social.Clear();
                if(paused) continue;
                if(listener.Motion.State==PetMotionState.Airborne || listener.Motion.State==PetMotionState.Charging) continue;
                if(!Available(listener)) { social.Clear(); continue; }
                DesktopPet best=null; float strength=0; string cue="sight";
                foreach(var source in pets)
                {
                    if(source==listener) continue;
                    if(!Observable(source)) { if(social.Source==source.Config.key) social.Clear(); continue; }
                    float distance=Vector2.Distance(listener.WorldPosition,source.WorldPosition)/(listener.Motion.BodyLength*listener.Config.scale);
                    social.TrackVisual(source.Config.key,listener.Local(source.WorldPosition),listener.Motion.BodyLength,distance<SightBodyLengths?dt:0,source.Motion.State==PetMotionState.Grabbed);
                    float speed=movement[source.Config.key].speed;
                    float motionBonus=Mathf.Clamp01(speed/(source.Motion.State==PetMotionState.Grabbed?1.5f:2.5f))*.65f;
                    float score=distance<SightBodyLengths ? (.35f+motionBonus)*(1-distance/(2*SightBodyLengths)) : 0;
                    string kind="sight";
                    if(stepped.Contains(source.Config.key) && distance<FootstepBodyLengths)
                    { score=Mathf.Max(score,.8f*(1-distance/(2*FootstepBodyLengths))); kind="footsteps"; }
                    if(heard.Contains(source.Config.key) && distance<VoiceBodyLengths)
                    { score=Mathf.Max(score,1f*(1-distance/(3*VoiceBodyLengths))); kind="voice"; }
                    if(score>strength) { strength=score; best=source; cue=kind; }
                }
                if(best!=null) social.Notice(best.Config.key,listener.Local(best.WorldPosition),strength,
                    SpacingBodyLengths*Mathf.Max(listener.Motion.BodyLength*listener.Config.scale,best.Motion.BodyLength*best.Config.scale)/listener.Config.scale,best.Config.name,cue,best.Motion.State!=PetMotionState.Grabbed,movement[best.Config.key].speed>.2f);
            }
        }
    }
}
