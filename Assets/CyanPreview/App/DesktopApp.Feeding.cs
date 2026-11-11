using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class DesktopApp
    {
        readonly PetFeeding feeding=new PetFeeding();
        FoodMorselView foodView;
        string feedingStatus;
        void ResetFeeding()
        {
            feeding.Cancel(); denSurfaces.Close(); if(view!=null) { view.Visible=true; }
            foodView?.Dispose(); foodView=new FoodMorselView(transform); PublishFeedingStatus();
        }
        void BeginFeeding()
        { if(feeding.Begin()) { motion.BeginIdleHold(); input.Cancel(); buttonHeld=false; native.ClearButtonEvents(); PublishFeedingStatus(); } }
        void CancelFeeding()
        {
            feeding.RequestReturn();
            motion.Behavior.TaskTarget=feeding.Target; motion.Behavior.HoldPosition=feeding.UsingRoute;
            if(!feeding.Active) motion.Behavior.Rest();
            foodView.Show(false,Vector2.zero); PublishFeedingStatus();
        }
        bool PlaceFoodInput(Vector2 point)
        {
            if(feeding.Stage!=FeedingStage.Placing) return false;
            if(native.EscapePressed) { CancelFeeding(); native.ClearButtonEvents(); return true; }
            feeding.Preview(point,surfaces); native.SetClickThrough(false);
            while(native.TryReadButton(out var button))
            {
                if(!button.Down && native.WorkArea.Contains(button.Position))
                {
                    feeding.Preview(DesktopCoordinates.ToWorld(button.Position,native.WorkArea,settings.scale),surfaces);
                    if(feeding.Place(feeding.Food,surfaces.Bounds,motion.HeadToTailLength))
                    {
                        motion.Behavior.TaskTarget=null; motion.Behavior.HoldPosition=false;
                        motion.ExploreClick(feeding.ChooseInspectionPoint(motion.Position,surfaces));
                        motion.LookAt(feeding.Food); petAudio?.Vocalize();
                    }
                    native.ClearButtonEvents(); break;
                }
            }
            RefreshWindows(); motion.ObserveCursor(point);
            motion.Advance(Time.unscaledDeltaTime);
            petAudio?.Advance(Time.unscaledDeltaTime); jumpEffects.Advance(Time.unscaledDeltaTime);
            foodView.Show(feeding.FoodVisible,feeding.Food); PublishFeedingStatus();
            pet.Draw(motion.Interpolation); view.Refresh(pet); RefreshFoodVisual(); desktopRenderer.Render(native.WorkArea,settings.scale);
            return true;
        }
        void AdvanceFeeding(float seconds)
        {
            var previous=feeding.Stage;
            feeding.Advance(seconds,motion.Position,motion.MouthPosition,motion.State==PetMotionState.Grabbed,surfaces.Bounds,motion.State==PetMotionState.Crawling,feeding.UsingRoute?motion.OccupiedBounds:(Rect?)null);
            motion.Behavior.TaskTarget=feeding.Target;
            if(previous!=FeedingStage.Carrying && feeding.Stage==FeedingStage.Carrying) petAudio?.FoodPickupCall();
            if(feeding.Stage==FeedingStage.Investigating || feeding.Stage==FeedingStage.Approaching) motion.LookAt(feeding.Food);
            if(previous!=FeedingStage.None && feeding.Stage==FeedingStage.None) motion.Behavior.Rest();
            if(feeding.Frozen) jumpEffects.Advance(seconds);
            if(feeding.UsingRoute && !denSurfaces.Active) denSurfaces.Open(feeding.DenDistance);
            else if(!feeding.UsingRoute && denSurfaces.Active) { denSurfaces.Close(); motion.ReleaseSupport(); }
            motion.Behavior.HoldPosition=feeding.UsingRoute;
            if(feeding.Stage==FeedingStage.Entering || feeding.Stage==FeedingStage.Emerging)
            {
                var target=feeding.Stage==FeedingStage.Entering?feeding.Den:feeding.ReturnPoint;
                motion.LookAt(target);
                if(Vector2.Distance(motion.Position,target)>35) motion.TryTaskJump(target);
            }
            motion.AutonomousJumps=settings.jumps && !feeding.UsingRoute;

            PublishFeedingStatus();
        }
        void RefreshFoodVisual()
        {
            if(feeding.Stage==FeedingStage.Carrying || feeding.Stage==FeedingStage.Entering) view.HoldFood(foodView,pet);
            else foodView.Show(feeding.FoodVisible,feeding.Food);
        }
        void PublishFeedingStatus()
        {
            string status=feeding.Stage==FeedingStage.None?"":feeding.Status;
            if(status==feedingStatus) return;
            feedingStatus=status;
            try { File.WriteAllText(settingsFile+".feeding",status); }
            catch(IOException) { }
            Debug.Log("FEEDING_STAGE "+feeding.Stage);
        }
    }
}
