using UnityEngine;
namespace CyanPreview
{
    public sealed partial class DesktopApp
    {
        void CursorClick(DesktopPet p,Vector2 point)
        {
            var m=p.Motion; var t=p.Tongue;
            if(t!=null)
            {
                if(p.Feeding.Active || m.RivalTarget.HasValue || p.Input.CapturesPointer) return;
                if(t.Active) { if(t.Curious) p.AfterFlickTarget=point; return; }
                float distance=Vector2.Distance(m.MouthPosition,point);
                if(p.Config.clickToExplore && food.Owner==FoodOwner.None && m.State==PetMotionState.Crawling && !m.Behavior.TaskTarget.HasValue
                    && m.Behavior.CanConsiderCursorFlick(distance))
                {
                    p.PendingTongueClick=point; p.TongueTurnTime=0;
                    m.BeginIdleHold(); m.TongueLookTarget=point; m.LookAt(point); return;
                }
                ClearTongueTurn(p);
            }
            m.ExploreClick(point);
        }
        void ClearTongueTurn(DesktopPet p)
        { p.PendingTongueClick=null; p.TongueTurnTime=0; p.Motion.TongueLookTarget=null; p.Motion.Behavior.HoldPosition=false; }
        bool AdvanceTongue(DesktopPet p,float dt)
        {
            var t=p.Tongue; if(t==null) return false;
            var m=p.Motion; string key=p.Config.key;
            if(p.PendingTongueClick.HasValue)
            {
                var point=p.PendingTongueClick.Value;
                if(!p.Config.clickToExplore || food.Owner!=FoodOwner.None || p.Feeding.Active || m.RivalTarget.HasValue
                    || m.State!=PetMotionState.Crawling || p.Input.CapturesPointer)
                    ClearTongueTurn(p);
                else
                {
                    if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return true;
                    p.TongueTurnTime+=Mathf.Min(dt,.1f);
                    m.TongueLookTarget=point; m.LookAt(point);
                    var facing=(m.HeadPosition-m.BodyPosition(0)).normalized;
                    bool aligned=Vector2.Dot(facing,(point-m.HeadPosition).normalized)>=.8f;
                    if(p.TongueTurnTime<.15f || (!aligned && p.TongueTurnTime<2.5f)) return true;
                    // Roll once after the turn, never once per frame while waiting.
                    ClearTongueTurn(p);
                    if(aligned && m.Behavior.ChooseCursorFlick(Vector2.Distance(m.MouthPosition,point))
                        && t.TryStartCurious(m.MouthPosition,facing,point,m.BodyLength))
                    {
                        m.Behavior.CursorFlickStarted(); p.AfterFlickTarget=point;
                        m.Behavior.HoldPosition=true; m.TongueLookTarget=point; m.TongueActive=true;
                        return true;
                    }
                    m.ExploreClick(point); return false;
                }
            }
            if(t.Active && t.Curious)
            {
                if(food.Owner!=FoodOwner.None || p.Feeding.Active || m.RivalTarget.HasValue || m.State!=PetMotionState.Crawling || p.Input.CapturesPointer)
                    { t.Cancel(); p.AfterFlickTarget=null; }
                else t.Advance(dt,m.MouthPosition,(m.BodyPosition(0)-m.BodyPosition(1)).normalized,null,null);
                m.TongueActive=t.Active; m.Behavior.HoldPosition=t.Active;
                m.TongueLookTarget=t.Active?(Vector2?)t.Aim:null;
                if(t.Active) { m.LookAt(t.Aim); return true; }
                var destination=p.AfterFlickTarget; p.AfterFlickTarget=null;
                if(destination.HasValue && p.Config.clickToExplore) m.ExploreClick(destination.Value);
                return false;
            }
            bool own=food.Owner==FoodOwner.Tongue && food.Holder==key;
            bool eligible=m.State==PetMotionState.Crawling && !m.RivalTarget.HasValue;
            if(t.Active && (!eligible || (!food.Available && !own) || (t.Holding && !own)))
            {
                if(own) food.Drop(Clamp(t.Tip*p.Config.scale));
                t.Cancel(); m.TongueActive=false; m.Behavior.HoldPosition=false;
                ResetInterest(p); p.FoodRevision=-1; return false;
            }
            var forward=(m.BodyPosition(0)-m.BodyPosition(1)).normalized;
            if(!t.Active && eligible && food.Available && (p.Feeding.Stage==FeedingStage.Investigating || p.Feeding.Stage==FeedingStage.Approaching))
            {
                if(t.TryStart(m.MouthPosition,forward,p.Local(food.Position))) m.BeginIdleHold();
            }
            bool wasActive=t.Active;
            t.Advance(dt,m.MouthPosition,forward,food.Available?(Vector2?)p.Local(food.Position):null,()=>food.TakeTongue(key));
            if(t.Holding) food.Move(t.Tip*p.Config.scale);
            if(t.Delivered && food.SwallowTongue(key))
            {
                p.Feeding.CatchAtMouth(m.Position); p.Audio?.FoodPickupCall(); diagnostics?.Pickup(pets,food);
                foreach(var other in pets) if(other!=p && !other.Feeding.UsingRoute) { ResetInterest(other); other.FollowingFood=true; }
            }
            m.TongueActive=t.Active;
            if(t.Active)
            {
                m.Behavior.TaskTarget=null; m.Behavior.HoldPosition=true; m.LookAt(t.Aim); m.AutonomousJumps=false;
                return true;
            }
            if(wasActive) { m.Behavior.HoldPosition=false; m.Behavior.TaskTarget=p.Feeding.Target; }
            return false;
        }
    }
}
