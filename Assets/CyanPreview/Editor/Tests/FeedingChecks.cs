using System;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class FeedingChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Motion(ReferenceContext context)
        {
            foreach(bool backgrounds in new[]{false,true})
            foreach(bool jumps in new[]{false,true})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                var area=new Rect(-400,-250,800,500);
                IPetSurfaces surfaces=backgrounds?(IPetSurfaces)new BackgroundSurfaces(area):new FrameSurfaces(area);
                var motion=new PetMotion(pet,surfaces,31) {Roam=false,AutonomousJumps=jumps};
                int launches=0; motion.JumpLaunched+=(p,v,power)=>launches++;
                var task=new PetFeeding(); task.Begin();
                var food=surfaces.Nearest(new Vector2(140,-210),out var normal)+normal*10;
                task.Place(food,area); motion.ExploreClick(task.ChooseInspectionPoint(motion.Position,surfaces)); bool ate=false; int steps=0; DenPath path=null;
                while(task.Active && steps++<5000)
                {
                    var previous=task.Stage;
                    task.Advance(.025f,motion.Position,motion.MouthPosition,false,area,motion.State==PetMotionState.Crawling);
                    if(previous!=task.Stage && task.Stage==FeedingStage.Entering) path=motion.BeginDenEntry(task.Entrance);
                    if(previous!=task.Stage && task.Stage==FeedingStage.Emerging) path=DenPath.Emerge(task.Entrance,area);
                    if(task.Stage==FeedingStage.Entering || task.Stage==FeedingStage.Emerging) motion.PoseDen(path,task.TransitionProgress);
                    if(previous==FeedingStage.Emerging && task.Stage==FeedingStage.None) { motion.PoseDen(path,1); motion.FinishDenTransition(); }
                    pet.Draw();
                    motion.Behavior.TaskTarget=task.Target;
                    ate|=task.Stage==FeedingStage.Eating;
                    if(!task.Frozen) motion.Advance(.025f);
                }
                Check(jumps || launches==0,"Feeding ignored disabled propulsion");
                Check(ate && !task.Active,"Real motion did not complete feeding, backgrounds="+backgrounds+" position="+motion.Position+" stage="+task.Stage);
            }
            Debug.Log("FEEDING_REAL_MOTION_RETURN_PASSED");
        }
        static bool PlaceAndInspect(PetFeeding task,Vector2 point,Rect area)
        {
            if(!task.Place(point,area)) return false;
            for(int i=0;i<60 && task.Stage==FeedingStage.Investigating;i++) task.Advance(.1f,area.max+Vector2.one*100,Vector2.zero,false,area);
            return true;
        }
        public static void Run()
        {
            var area=new Rect(-400,-250,800,500); var food=new PetFeeding();
            food.Begin(); food.Place(Vector2.zero,area);
            Check(food.Stage==FeedingStage.Investigating && !food.Target.HasValue,"Food bypassed investigation");
            var waypoint=food.ChooseInspectionPoint(new Vector2(-100,0),new BackgroundSurfaces(area));
            Check(Vector2.Distance(waypoint,food.Food)>35,"Inspection waypoint is directly on food");
            food.Advance(.1f,food.Food,food.Food,false,area);
            Check(food.Stage==FeedingStage.Investigating,"Food was grabbed immediately during investigation"); food.Cancel();
            Check(food.Begin() && !food.Begin(),"Repeated feeding replaced an active task");
            Check(!PlaceAndInspect(food,new Vector2(float.NaN,0),area),"Invalid food accepted");
            food.Cancel(); Check(!food.Active,"Placement cancellation failed");
            food.Begin(); PlaceAndInspect(food,new Vector2(999,999),area); Check(area.Contains(food.Food),"Food placed off desktop"); food.Cancel();
            food.Begin(); PlaceAndInspect(food,Vector2.zero,area); food.Advance(.025f,Vector2.zero,Vector2.right*20,false,area);
            food.Advance(.025f,food.Entrance,food.Entrance,false,area,false);
            Check(food.Stage==FeedingStage.Carrying,"Airborne pet entered den");
            Check(food.Stage==FeedingStage.Carrying && food.Entrance.y==area.yMin+14,"Pickup/den selection failed");
            food.Advance(.025f,Vector2.zero,Vector2.right*30,true,area);
            Check(food.Stage==FeedingStage.Approaching && food.Food==Vector2.right*30,"Grab did not drop food");
            food.Advance(.025f,Vector2.right*30,Vector2.right*50,false,area);
            food.Advance(.025f,food.Entrance,food.Entrance,false,area);
            Check(food.Stage==FeedingStage.Entering,"Did not enter den");
            for(int i=0;i<75;i++) food.Advance(.025f,food.Entrance,food.Entrance,false,area);
            Check(food.Stage==FeedingStage.Eating && !food.FoodVisible,"Food remained visible inside den");
            var progress=food.TransitionProgress; food.Advance(0,food.Entrance,food.Entrance,false,area); Check(food.TransitionProgress==progress,"Pause advanced feeding");
            for(int i=0;i<205;i++) food.Advance(.025f,food.Entrance,food.Entrance,false,area);
            Check(!food.Active,"Automatic return did not finish");
            food.Begin(); PlaceAndInspect(food,Vector2.zero,area); food.Advance(.1f,Vector2.one*100,Vector2.zero,false,new Rect(0,0,300,200)); Check(!food.Active,"Display change stranded task");
            food.Begin(); PlaceAndInspect(food,Vector2.zero,area); for(int i=0;i<610;i++) food.Advance(.1f,Vector2.one*100,Vector2.zero,false,area); Check(!food.Active,"Unreachable food never expires");
            var other=new PetFeeding(); other.Begin(); food.Begin(); food.Cancel(); Check(other.Stage==FeedingStage.Placing,"One pet canceled another's food");
            foreach(FeedingStage stage in new[]{FeedingStage.Entering,FeedingStage.Eating,FeedingStage.Emerging})
            {
                food.Cancel(); food.Begin(); PlaceAndInspect(food,Vector2.zero,area); food.Advance(.025f,Vector2.zero,Vector2.zero,false,area); food.Advance(.025f,food.Entrance,food.Entrance,false,area);
                for(int i=0;i<300 && food.Stage!=stage;i++) food.Advance(.025f,food.Entrance,food.Entrance,false,area);
                Check(food.Stage==stage,"Could not reach tested den stage"); food.Cancel(); Check(!food.Frozen,"Cancel left pet hidden");
            }
            Debug.Log("FEEDING_PROTOTYPE_EDGE_CASES_PASSED");
        }
    }
}
