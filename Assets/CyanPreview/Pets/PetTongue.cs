using System;
using UnityEngine;
namespace CyanPreview
{
    public enum TongueStage { Hidden, Aiming, Flying, Retracting }
    // Room-free adaptation of White's 40 Hz LizardTongue small-object path.
    public sealed class PetTongue
    {
        public const float AttackRange=440, Range=594;
        public TongueStage Stage {get;private set;}
        public bool Active => Stage!=TongueStage.Hidden;
        public bool Holding {get;private set;}
        public bool Curious {get;private set;}
        public bool Delivered {get;private set;}
        public Vector2 Tip {get;private set;}
        public Vector2 PreviousTip {get;private set;}
        public Vector2 Aim {get;private set;}
        public readonly Vector2[] Points=new Vector2[10], PreviousPoints=new Vector2[10];
        readonly Vector2[] velocities=new Vector2[10];
        Vector2 velocity;
        float clock, cooldown;
        int ticks;
        public event Action<int> Sound;
        public static float CursorRange(float bodyLength) => Mathf.Min(AttackRange,bodyLength*7);
        public static bool CursorInRange(float distance,float bodyLength) => !float.IsNaN(distance) && !float.IsInfinity(distance)
            && distance>=24 && distance<=CursorRange(bodyLength);
        public bool TryStartCurious(Vector2 mouth,Vector2 forward,Vector2 cursor,float bodyLength)
        {
            float distance=Vector2.Distance(mouth,cursor);
            if(Active||cooldown>0||!CursorInRange(distance,bodyLength)
                ||Vector2.Dot(forward.normalized,(cursor-mouth).normalized)<.3f) return false;
            BeginShot(mouth,cursor,true); return true;
        }
        public bool TryStart(Vector2 mouth,Vector2 forward,Vector2 target)
        {
            float distance=Vector2.Distance(mouth,target);
            if(Active || cooldown>0 || distance<45 || distance>AttackRange || Vector2.Dot(forward.normalized,(target-mouth).normalized)<.3f) return false;
            BeginShot(mouth,target,false); return true;
        }
        void BeginShot(Vector2 mouth,Vector2 target,bool curious)
        {
            Curious=curious; Aim=target; Tip=PreviousTip=mouth; Stage=TongueStage.Aiming; ticks=0; Delivered=Holding=false;
            for(int i=0;i<10;i++) { Points[i]=PreviousPoints[i]=mouth; velocities[i]=Vector2.zero; }
            Sound?.Invoke(0);
        }
        public void Cancel()
        { Stage=TongueStage.Hidden; Holding=false; Delivered=false; cooldown=.5f; clock=0; }
        public void Advance(float dt,Vector2 mouth,Vector2 forward,Vector2? target,Func<bool> claim)
        {
            Delivered=false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return;
            clock+=Mathf.Min(dt,.1f);
            while(clock>=.025f) { clock-=.025f; Step(mouth,forward,target,claim); }
        }
        void Step(Vector2 mouth,Vector2 forward,Vector2? target,Func<bool> claim)
        {
            cooldown=Mathf.Max(0,cooldown-.025f);
            if(!Active) return;
            PreviousTip=Tip;
            if(Stage==TongueStage.Aiming)
            {
                Tip=mouth;
                if(!Curious && (!target.HasValue || Vector2.Distance(mouth,target.Value)>AttackRange || Vector2.Dot(forward.normalized,(target.Value-mouth).normalized)<.3f)) { Cancel(); return; }
                if(!Curious) Aim=target.Value;
                if(++ticks>=40)
                {
                    var aim=Aim+Vector2.up*Vector2.Distance(mouth,Aim)*.05f;
                    velocity=(aim-mouth).normalized*(30*Mathf.Lerp(Mathf.InverseLerp(Range*.05f,Range,Vector2.Distance(mouth,Aim)),1,.75f));
                    Stage=TongueStage.Flying; ticks=0; Sound?.Invoke(1);
                }
            }
            else if(Stage==TongueStage.Flying)
            {
                Tip+=velocity; velocity.y-=.1f;
                if(Curious && (SegmentDistance(PreviousTip,Tip,Aim)<=10 || Vector2.Dot(Aim-Tip,velocity)<=0))
                    Stage=TongueStage.Retracting;
                if(!Curious && target.HasValue && SegmentDistance(PreviousTip,Tip,target.Value)<=10 && claim!=null && claim())
                { Tip=target.Value; Holding=true; Stage=TongueStage.Retracting; Sound?.Invoke(2); }
                else
                {
                    float distance=Vector2.Distance(mouth,Tip);
                    velocity+=(mouth-Tip).normalized*Mathf.Max(0,distance-Range*.1f)*.01f;
                    if(distance>=Range || ++ticks>=60 || Vector2.Dot(velocity,Tip-mouth)<0) Stage=TongueStage.Retracting;
                }
            }
            else
            {
                var destination=mouth+forward.normalized*Vector2.Distance(mouth,Tip)*.5f;
                velocity=Vector2.Lerp(velocity,(destination-Tip).normalized*(Vector2.Distance(Tip,destination)+5)*.5f,.5f);
                Tip+=velocity;
                if(SegmentDistance(PreviousTip,Tip,mouth)<10)
                { Delivered=Holding; Holding=false; Stage=TongueStage.Hidden; cooldown=.5f; Tip=mouth; Sound?.Invoke(3); }
            }
            // Elastic visual segments follow the game's stretch-dependent centerline and width.
            float stretch=Mathf.Clamp01(Vector2.Distance(mouth,Tip)/Range);
            for(int i=0;i<10;i++)
            {
                PreviousPoints[i]=Points[i]; float t=i/9f;
                var desired=Vector2.Lerp(mouth,Tip,t);
                velocities[i]*=.9f; Points[i]+=velocities[i];
                float end=Mathf.Abs(t-.5f)*2;
                Points[i]=Vector2.Lerp(Points[i],desired,Mathf.Max(stretch*.25f,Mathf.Pow(end,3)));
                velocities[i]+=(desired-Points[i])*(.2f+stretch*.3f);
            }
        }
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 p)
        { var v=b-a; return Vector2.Distance(p,a+v*(v.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude))); }
    }
}
