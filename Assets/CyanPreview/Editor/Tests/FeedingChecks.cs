using System;
using System.Linq;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class FeedingChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        static readonly Rect Area=new Rect(-400,-250,800,500);
        static PetFeeding Enter()
        {
            var task=new PetFeeding(); task.Begin(); task.Place(new Vector2(140,-240),Area);
            for(int i=0;i<60;i++) task.Advance(.1f,Vector2.zero,Vector2.zero,false,Area);
            task.Advance(.025f,task.Food,task.Food,false,Area);
            Check(task.Stage==FeedingStage.Carrying,"Pickup failed");
            task.Advance(.025f,task.Entrance,task.Entrance,false,Area,false);
            Check(task.Stage==FeedingStage.Carrying,"Airborne entry accepted");
            task.Advance(.025f,task.Entrance,task.Entrance,false,Area);
            Check(task.Stage==FeedingStage.Entering,"Entry failed"); return task;
        }
        public static void Run()
        {
            var task=new PetFeeding(); Check(task.Begin() && !task.Begin(),"Duplicate feeding accepted");
            Check(!task.Place(new Vector2(float.NaN,0),Area),"Invalid food accepted");
            Check(!task.Place(Vector2.zero,Area,float.NaN) && !task.Place(Vector2.zero,Area,float.MaxValue),"Invalid pet length accepted");
            task.Place(Vector2.zero,Area);
            Check(task.Stage==FeedingStage.Investigating && task.Target==task.InspectionPoint,"Investigation bypassed");
            task.Cancel(); Check(!task.Active,"Placement cancellation failed");
            var near=new PetFeeding(); near.Begin(); near.Place(Vector2.zero,Area);
            near.ChooseInspectionPoint(new Vector2(20,0),new BackgroundSurfaces(Area));
            near.Advance(.025f,new Vector2(20,0),Vector2.zero,false,Area);
            Check(near.Stage==FeedingStage.Carrying,"Nearby food waited for an investigation timer");
            var far=new PetFeeding(); far.Begin(); far.Place(Vector2.zero,Area);
            far.ChooseInspectionPoint(new Vector2(300,0),new BackgroundSurfaces(Area));
            far.Advance(.025f,new Vector2(300,0),new Vector2(300,0),false,Area);
            Check(far.Stage==FeedingStage.Investigating && far.Target.HasValue,"Distant food lost its approach waypoint");
            far.Advance(.025f,far.InspectionPoint,far.InspectionPoint,false,Area);
            Check(far.Stage==FeedingStage.Approaching && far.Target==far.Food,"Waypoint arrival retained mandatory wait");
            var medium=new PetFeeding(); medium.Begin(); medium.Place(Vector2.zero,Area);
            medium.ChooseInspectionPoint(new Vector2(-100,0),new BackgroundSurfaces(Area));
            Check(medium.InspectionPoint.x<0 && Mathf.Abs(medium.InspectionPoint.x)<Mathf.Abs(far.InspectionPoint.x),"Observers share a fixed inspection ring");
            var paused=new PetFeeding(); paused.Begin(); paused.Place(Vector2.zero,Area);
            paused.Advance(0,Vector2.zero,Vector2.zero,false,Area);
            paused.Advance(.025f,Vector2.zero,Vector2.zero,true,Area);
            Check(paused.Stage==FeedingStage.Investigating,"Paused or grabbed pet picked up food");

            task=Enter();
            Check(task.Den.y<=Area.yMin-520 && task.Den.x==task.Entrance.x,"Den is not below desktop");
            for(int i=0;i<100;i++) task.Advance(.1f,task.Den,task.Den,false,Area,true,Area);
            Check(task.Stage==FeedingStage.Entering,"Eating started with visible tail");
            var outside=new Rect(task.Den-Vector2.one*30,Vector2.one*60);
            task.Advance(.1f,task.Den,task.Den,false,Area,true,outside);
            Check(task.Frozen && !task.FoodVisible,"Outside pet did not eat");
            for(int i=0;i<100;i++) task.Advance(0,task.Den,task.Den,false,Area,true,outside);
            Check(task.Stage==FeedingStage.Eating,"Pause advanced eating");
            for(int i=0;i<31;i++) task.Advance(.1f,task.Den,task.Den,false,Area,true,outside);
            Check(task.Stage==FeedingStage.Emerging,"Automatic return failed");
            task.Advance(.1f,task.ReturnPoint,task.ReturnPoint,false,Area,true,outside);
            Check(task.Active,"Return closed over offscreen tail");
            task.Advance(.1f,task.ReturnPoint,task.ReturnPoint,false,Area,true,new Rect(task.ReturnPoint-Vector2.one*30,Vector2.one*60));
            Check(!task.Active,"Return did not complete");
            task=Enter(); task.RequestReturn(); Check(task.Stage==FeedingStage.Emerging,"Cancel outside lost return");
            task.Advance(.1f,task.Den,task.Den,false,new Rect(0,0,900,700)); Check(!task.Active,"Display rebuild retained stale task");
            foreach(float length in new[]{80f,160f,300f})
            {
                var sized=new PetFeeding(); sized.Begin(); sized.Place(Vector2.zero,Area,length);
                Check(sized.DenDistance>=520 && sized.DenDistance>=length*3,"Den too close");
                var source=new FrameSurfaces(Area); var route=new DenRouteSurfaces(source,Area); var other=new DenRouteSurfaces(source,Area);
                route.Open(sized.DenDistance); var below=new Vector2(0,Area.yMin-sized.DenDistance);
                Check(route.Constrain(below,8,out _)==below,"Bottom passage blocked");
                Check(other.Constrain(below,8,out _).y>Area.yMin,"Passage leaked to another pet");
                route.Close(); Check(route.Constrain(below,8,out _).y>Area.yMin,"Passage remains open");
            }
            Debug.Log("FEEDING_BOTTOM_PASSAGE_EDGE_CASES_PASSED");
        }
        public static void Motion(ReferenceContext context)
        {
            foreach(bool backgrounds in new[]{false,true})
            foreach(bool jumps in new[]{false,true})
            foreach(int cancel in new[]{0,1,2})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                IPetSurfaces desktop=backgrounds?(IPetSurfaces)new BackgroundSurfaces(Area):new FrameSurfaces(Area);
                var surfaces=new DenRouteSurfaces(desktop,Area);
                var motion=new PetMotion(pet,surfaces,31) {Roam=false,AutonomousJumps=jumps};
                int entryHops=0,returnHops=0; var task=new PetFeeding();
                motion.JumpLaunched+=(p,v,power)=> { if(task.Stage==FeedingStage.Entering) entryHops++; if(task.Stage==FeedingStage.Emerging) returnHops++; };
                task.Begin(); var food=desktop.Nearest(new Vector2(140,-210),out var normal)+normal*10;
                task.Place(food,Area,motion.HeadToTailLength); motion.ExploreClick(task.ChooseInspectionPoint(motion.Position,desktop));
                bool ate=false,cancelled=false; int steps=0;
                while(task.Active && steps++<8000)
                {
                    task.Advance(.025f,motion.Position,motion.MouthPosition,false,Area,motion.State==PetMotionState.Crawling,motion.OccupiedBounds);
                    if(task.Stage==FeedingStage.Eating) { ate=true; if(cancel==1) task.RequestReturn(); }
                    if(cancel==2 && !cancelled && task.Stage==FeedingStage.Entering && motion.State==PetMotionState.Airborne)
                    { task.RequestReturn(); cancelled=true; }
                    if(task.UsingRoute && !surfaces.Active) surfaces.Open(task.DenDistance);
                    if(!task.UsingRoute && surfaces.Active)
                    {
                        Check(motion.OccupiedBounds.yMin>=Area.yMin,"Passage closed with tail below desktop");
                        surfaces.Close(); motion.ReleaseSupport();
                    }
                    motion.Behavior.TaskTarget=task.Target; motion.Behavior.HoldPosition=task.UsingRoute;
                    motion.AutonomousJumps=jumps && !task.UsingRoute;
                    if(task.Stage==FeedingStage.Entering || task.Stage==FeedingStage.Emerging)
                    {
                        var target=task.Stage==FeedingStage.Entering?task.Den:task.ReturnPoint;
                        if(Vector2.Distance(motion.Position,target)>35) motion.TryTaskJump(target);
                    }
                    var before=motion.Position;
                    if(!task.Frozen) motion.Advance(.025f);
                    Check(Vector2.Distance(before,motion.Position)<65,"Passage teleported pet"); pet.Draw();
                }
                Check((ate || cancelled) && !task.Active && entryHops>0 && returnHops>0,"Bottom hop failed: backgrounds="+backgrounds+" jumps="+jumps+" position="+motion.Position+" stage="+task.Stage+" hops="+entryHops+"/"+returnHops);
                motion.Behavior.Rest(); motion.Roam=false; motion.AutonomousJumps=false;
                for(int i=0;i<400;i++) motion.Advance(.025f);
                Check(motion.State==PetMotionState.Crawling && motion.Position.y>=Area.yMin,"Return never settled after landing");
            }
            PlacementAndMouth(context);
            Debug.Log("FEEDING_BOTTOM_HOPS_AND_RETURN_PASSED");
        }
        static void PlacementAndMouth(ReferenceContext context)
        {
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320"); var motion=new PetMotion(pet,new BackgroundSurfaces(Area),31) {Roam=true,AutonomousJumps=true};
                motion.BeginIdleHold(); var start=motion.Position;
                float breath=(float)Get(pet.Graphics,"breath");
                for(int i=0;i<240;i++) { motion.ObserveCursor(start+Vector2.right*70); motion.Advance(.025f); }
                Check(Vector2.Distance(motion.Position,start)<12,"Placement hold wandered");
                Check((float)Get(pet.Graphics,"breath")!=breath,"Placement froze idle animation");
                Check(motion.Roam && motion.AutonomousJumps,"Placement changed saved movement options");
                motion.Behavior.HoldPosition=false; motion.MoveTo(start+Vector2.right*100);
                for(int i=0;i<80;i++) motion.Advance(.025f);
                Check(Vector2.Distance(motion.Position,start)>15,"Placement release did not restore movement");
                var root=new GameObject("Feeding view check");
                using(var view=new SpriteMeshView(root.transform))
                using(var food=new FoodMorselView(root.transform))
                {
                    pet.Draw(); view.Refresh(pet); view.HoldFood(food,pet);
                    var morsel=root.GetComponentsInChildren<SpriteRenderer>().Single();
                    Check(morsel.GetComponentInParent<UnityEngine.Rendering.SortingGroup>()!=null,"Held food outside pet sorting group");
                    var upper=((Array)Get(pet.Leaser,"sprites")).GetValue((int)Get(pet.Graphics,"SpriteHeadStart")+2);
                    Check(morsel.sortingOrder< Array.IndexOf(pet.OrderedSprites.ToArray(),upper)*2,"Food rendered above upper jaw");
                    Check(Vector2.Distance(morsel.transform.localPosition,pet.HeadPosition)<35,"Food missed head");
                    food.Show(true,Vector2.zero); Check(morsel.transform.parent==root.transform && morsel.sortingOrder==1000,"Dropped food retained jaw attachment");
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("FEEDING_IDLE_PLACEMENT_AND_MOUTH_LAYER_PASSED");
        }
    }
}
