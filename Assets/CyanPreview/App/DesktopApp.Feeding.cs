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
        DenPath denPath;
        void ResetFeeding()
        {
            feeding.Cancel(); denPath=null; if(view!=null) { view.Visible=true; }
            foodView?.Dispose(); foodView=new FoodMorselView(transform); PublishFeedingStatus();
        }
        void BeginFeeding()
        { if(feeding.Begin()) { input.Cancel(); buttonHeld=false; native.ClearButtonEvents(); PublishFeedingStatus(); } }
        void CancelFeeding()
        { if(feeding.Frozen) { motion.PoseDen(DenPath.Emerge(feeding.Entrance,surfaces.Bounds),1); motion.FinishDenTransition(); }
            feeding.Cancel(); denPath=null; motion.Behavior.TaskTarget=null; motion.Behavior.Rest(); view.Visible=true; foodView.Show(false,Vector2.zero); PublishFeedingStatus(); }
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
                    if(feeding.Place(feeding.Food,surfaces.Bounds))
                    {
                        motion.Behavior.TaskTarget=null;
                        motion.ExploreClick(feeding.ChooseInspectionPoint(motion.Position,surfaces));
                        motion.LookAt(feeding.Food); petAudio?.Vocalize();
                    }
                    native.ClearButtonEvents(); break;
                }
            }
            petAudio?.Advance(Time.unscaledDeltaTime); jumpEffects.Advance(Time.unscaledDeltaTime);
            foodView.Show(feeding.FoodVisible,feeding.Food); PublishFeedingStatus();
            pet.Draw(motion.Interpolation); view.Refresh(pet); desktopRenderer.Render(native.WorkArea,settings.scale);
            return true;
        }
        void AdvanceFeeding(float seconds)
        {
            var previous=feeding.Stage;
            feeding.Advance(seconds,motion.Position,motion.MouthPosition,motion.State==PetMotionState.Grabbed,surfaces.Bounds,motion.State==PetMotionState.Crawling);
            motion.Behavior.TaskTarget=feeding.Target;
            if(previous!=FeedingStage.Carrying && feeding.Stage==FeedingStage.Carrying) petAudio?.FoodPickupCall();
            if(feeding.Stage==FeedingStage.Investigating || feeding.Stage==FeedingStage.Approaching) motion.LookAt(feeding.Food);
            if(previous!=FeedingStage.None && feeding.Stage==FeedingStage.None) motion.Behavior.Rest();
            if(feeding.Frozen) jumpEffects.Advance(seconds);
            if(previous!=feeding.Stage && feeding.Stage==FeedingStage.Entering) denPath=motion.BeginDenEntry(feeding.Entrance);
            if(previous!=feeding.Stage && feeding.Stage==FeedingStage.Emerging) denPath=DenPath.Emerge(feeding.Entrance,surfaces.Bounds);
            if(denPath!=null && (feeding.Stage==FeedingStage.Entering || feeding.Stage==FeedingStage.Emerging)) motion.PoseDen(denPath,feeding.TransitionProgress);
            if(previous==FeedingStage.Emerging && feeding.Stage==FeedingStage.None)
            { motion.PoseDen(denPath,1); motion.FinishDenTransition(); denPath=null; }
            view.Visible=feeding.Stage!=FeedingStage.Eating;
            foodView.Show(feeding.FoodVisible,feeding.Stage==FeedingStage.Entering || feeding.Stage==FeedingStage.Carrying?motion.MouthPosition:feeding.Food);
            PublishFeedingStatus();
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
