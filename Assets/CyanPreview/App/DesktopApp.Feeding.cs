using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class DesktopApp
    {
        readonly SharedFood food=new SharedFood();
        FoodMorselView foodView;
        string feedingStatus;
        void BeginFeeding()
        {
            if(pets.Exists(p=>p.Feeding.UsingRoute) || !food.Begin()) return;
            CancelPointer(); native.ClearButtonEvents();
            foreach(var p in pets) { p.Motion.BeginIdleHold(); p.Input.Cancel(); }
        }
        void CancelFeeding()
        {
            food.Cancel(); CancelPointer();
            foreach(var p in pets)
            {
                p.Feeding.RequestReturn(); p.FollowingFood=false;
                p.Motion.Behavior.TaskTarget=p.Feeding.Target; p.Motion.Behavior.HoldPosition=p.Feeding.UsingRoute;
                if(!p.Feeding.Active) p.Motion.Behavior.Rest();
            }
            foodView.Show(false,Vector2.zero);
        }
        void PlaceFoodInput(Vector2 point)
        {
            if(native.EscapePressed) { CancelFeeding(); native.ClearButtonEvents(); return; }
            food.Move(Clamp(point)); native.SetClickThrough(false);
            while(native.TryReadButton(out var button))
            {
                if(!button.Down && native.WorkArea.Contains(button.Position))
                {
                    food.Place(OfferPoint(World(button.Position)));
                    foreach(var p in pets) p.Motion.Behavior.HoldPosition=false;
                    native.ClearButtonEvents(); break;
                }
            }
        }
        Vector2 OfferPoint(Vector2 point)
        {
            point=Clamp(point); Vector2 best=point; float distance=float.MaxValue;
            foreach(var p in pets)
            {
                var candidate=(p.Surfaces.Nearest(p.Local(point),out var normal)+normal*10)*p.Config.scale;
                float d=(candidate-point).sqrMagnitude;
                if(d<distance) { distance=d; best=candidate; }
            }
            return Clamp(best);
        }
        void ResetInterest(DesktopPet p)
        {
            p.Feeding.Cancel(); p.FollowingFood=false;
            // Clearing food interest must not reset an active rival's movement or posture.
            if(p.Motion.RivalTarget.HasValue) return;
            p.Motion.BeginIdleHold(); p.Motion.Behavior.TaskTarget=null;
            p.Motion.Behavior.HoldPosition=false; p.Motion.Behavior.Rest();
        }
        void AdvanceFeeding(float dt)
        {
            // Ownership decisions run serially; a second mouth can never claim an already-held morsel.
            foreach(var p in pets)
            {
                var f=p.Feeding; var m=p.Motion; string key=p.Config.key;
                if(food.Holder==key && food.Owner==FoodOwner.Pet && m.State==PetMotionState.Grabbed)
                { food.Drop(Clamp(p.WorldMouth)); ResetInterest(p); }
                bool owner=food.Holder==key;
                if(!owner && m.RivalTarget.HasValue && !f.UsingRoute)
                {
                    f.Cancel(); p.FoodRevision=-1; p.FollowingFood=food.Owner==FoodOwner.Pet || food.Owner==FoodOwner.Cursor;
                    m.AutonomousJumps=false;
                    continue;
                }
                if(!owner && !f.UsingRoute)
                {
                    if(food.Available)
                    {
                        if(p.FoodRevision!=food.Revision)
                        {
                            ResetInterest(p); p.FoodRevision=food.Revision;
                            f.Begin(); f.Place(p.Local(food.Position),p.Surfaces.Bounds,m.HeadToTailLength);
                            m.ExploreClick(f.ChooseInspectionPoint(m.Position,p.Surfaces)); m.LookAt(f.Food); p.Audio?.Vocalize();
                        }
                    }
                    else if(food.Owner==FoodOwner.Pet || food.Owner==FoodOwner.Cursor)
                    {
                        if(f.Active || !p.FollowingFood) ResetInterest(p);
                        p.FollowingFood=true;
                        var target=food.Position; var away=(p.WorldPosition-target).normalized;
                        if(away.sqrMagnitude<.1f) away=Vector2.right;
                        // Followers investigate the carrier's vicinity, never its off-screen den.
                        m.Behavior.TaskTarget=p.Local(Clamp(target+away*75)); m.LookAt(p.Local(target));
                    }
                    else if(f.Active || p.FollowingFood) ResetInterest(p);
                }
                var previous=f.Stage;
                f.Advance(dt,m.Position,m.MouthPosition,m.State==PetMotionState.Grabbed,p.Surfaces.Bounds,m.State==PetMotionState.Crawling,f.UsingRoute?m.OccupiedBounds:(Rect?)null);
                if(previous!=FeedingStage.Carrying && f.Stage==FeedingStage.Carrying)
                {
                    if(food.Take(key))
                    {
                        owner=true; p.Audio?.FoodPickupCall();
                        // Apply to every loser now, including those visited earlier in this frame.
                        foreach(var other in pets) if(other!=p && !other.Feeding.UsingRoute)
                        { ResetInterest(other); other.FollowingFood=true; other.Motion.AutonomousJumps=false; }
                    }
                    else ResetInterest(p);
                }
                if(owner && food.Owner==FoodOwner.Pet)
                {
                    food.Move(p.WorldMouth);
                    if(f.Stage==FeedingStage.Entering)
                    {
                        food.EnterDen(key);
                        foreach(var other in pets) if(other!=p && !other.Feeding.UsingRoute) ResetInterest(other);
                    }
                    else if(!f.Active) food.Drop(Clamp(p.WorldMouth));
                }
                if(f.Active) m.Behavior.TaskTarget=f.Target;
                if(f.Stage==FeedingStage.Investigating || f.Stage==FeedingStage.Approaching) m.LookAt(f.Food);
                if(previous!=FeedingStage.None && f.Stage==FeedingStage.None)
                { m.Behavior.Rest(); if(owner && food.Owner==FoodOwner.Den) food.Cancel(); }
                if(f.UsingRoute && !p.Den.Active) p.Den.Open(f.DenDistance);
                else if(!f.UsingRoute && p.Den.Active) { p.Den.Close(); m.ReleaseSupport(); }
                m.Behavior.HoldPosition=f.UsingRoute;
                if(f.Stage==FeedingStage.Entering || f.Stage==FeedingStage.Emerging)
                {
                    var target=f.Stage==FeedingStage.Entering?f.Den:f.ReturnPoint;
                    m.LookAt(target); if(Vector2.Distance(m.Position,target)>35) m.TryTaskJump(target);
                }
                m.AutonomousJumps=p.Config.jumps && !f.UsingRoute && !p.FollowingFood;
            }
        }
        void RefreshFoodVisual()
        {
            var owner=pets.Find(p=>p.Config.key==food.Holder);
            if(owner!=null && (owner.Feeding.Stage==FeedingStage.Carrying || owner.Feeding.Stage==FeedingStage.Entering)) owner.View.HoldFood(foodView,owner.Pet);
            else foodView.Show(food.Owner==FoodOwner.Placing || food.Owner==FoodOwner.Loose || food.Owner==FoodOwner.Cursor,food.Position);
        }
        void PublishFeedingStatus()
        {
            var owner=pets.Find(p=>p.Config.key==food.Holder);
            string status=food.Owner==FoodOwner.None?"":food.Owner==FoodOwner.Placing?"Click to place food · Escape cancels":food.Owner==FoodOwner.Cursor?"Release to offer the morsel":owner!=null?owner.Config.name+" · "+owner.Feeding.Status:"Shared food · hold for 0.1s to drag";
            if(status==feedingStatus) return; feedingStatus=status;
            try { File.WriteAllText(settingsFile+".feeding",status); } catch(IOException) { }
        }
    }
}
