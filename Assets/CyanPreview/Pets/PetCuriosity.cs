using UnityEngine;
namespace CyanPreview
{
    // Desktop social interest, independent of hostility, bites and food ownership.
    public sealed class PetCuriosity
    {
        public string Source { get; private set; }
        public string SourceName { get; private set; }
        public string Cue { get; private set; }
        public float Spacing { get; private set; }=55;
        public Vector2? Point { get; private set; }
        public Vector2? GazePoint { get; private set; }
        readonly float sympathy;
        float visibleAge=1, motionAge=1;
        public Vector2? LastSeenPoint { get; private set; }
        public bool Visible => LastSeenPoint.HasValue && visibleAge<.3f;
        public bool Following { get; set; }
        public PetCuriosity(float sympathy=.5f) { this.sympathy=Finite(sympathy)?Mathf.Clamp01(sympathy):.5f; }
        float acceptedStrength;
        public float Utility { get; private set; }
        float remaining,cooldown,voiceCooldown,age;
        public bool HoldingGlance => Point.HasValue && age<1;
        public bool ApproachAllowed { get; private set; }=true;
        public bool CanApproach => Point.HasValue && !HoldingGlance && ApproachAllowed;
        public bool Notice(string key,Vector2 point,float strength,float spacing=55,string name=null,string cue="sight",bool approachAllowed=true,bool moving=false)
        {
            bool strongerMotion=moving && cue!="voice" && Cue!="voice" && Source!=key && Point.HasValue && age>=.75f && strength>acceptedStrength+.18f;
            if(string.IsNullOrEmpty(key) || !Finite(point.x) || !Finite(point.y) || !Finite(strength) || strength<=0 || !Finite(spacing) || spacing<=0 || (cooldown>0 && !strongerMotion && (cue!="voice" || voiceCooldown>0))) return false;
            if(cue=="voice") voiceCooldown=12;
            Following=false; LastSeenPoint=null; visibleAge=motionAge=1;
            ApproachAllowed=approachAllowed;
            Source=key; SourceName=name??key; Cue=cue; Spacing=spacing; Point=GazePoint=point; acceptedStrength=Mathf.Clamp01(strength); Utility=Mathf.Clamp01(strength); remaining=cue=="voice"?Mathf.Lerp(6,12,sympathy):Mathf.Lerp(2,6,sympathy); age=0; cooldown=12;
            return true;
        }
        public void TrackVisual(string key,Vector2 point,float bodyLength,float dt,bool held)
        {
            if(Source!=key || !Point.HasValue) return;
            if(held) ApproachAllowed=false;
            if(!Finite(point.x) || !Finite(point.y) || !Finite(dt) || dt<=0 || !Finite(bodyLength) || bodyLength<=0)
            { visibleAge=1; return; }
            if(LastSeenPoint.HasValue && Vector2.Distance(LastSeenPoint.Value,point)>bodyLength*.002f) motionAge=0;
            visibleAge=0;
            if(!held) LastSeenPoint=point;
            if(Cue!="voice" || Following || held)
                GazePoint=Vector2.MoveTowards(GazePoint??Point.Value,point,bodyLength*6*Mathf.Min(dt,.25f));
        }
        static bool Finite(float x) => !float.IsNaN(x)&&!float.IsInfinity(x);
        public void Advance(float dt)
        {
            if(!Finite(dt)||dt<=0) return;
            dt=Mathf.Min(dt,.25f); cooldown=Mathf.Max(0,cooldown-dt); voiceCooldown=Mathf.Max(0,voiceCooldown-dt); remaining-=dt*(Following && (!Visible || motionAge>1) ? 2 : 1); age+=dt; visibleAge+=dt; motionAge+=dt;
            if(remaining<=0) Clear(); else Utility=Mathf.Max(0,Utility-dt*(Cue=="voice"?.035f:.1f)*Mathf.Lerp(1.5f,.65f,sympathy));
        }
        public void Clear() { Following=false; LastSeenPoint=null; visibleAge=motionAge=1; Source=null; SourceName=null; Cue=null; Point=GazePoint=null; Utility=0; remaining=0; }
    }
}
