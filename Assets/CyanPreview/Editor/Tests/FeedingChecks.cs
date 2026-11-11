using System;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class FeedingChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        static readonly Rect Area=new Rect(-400,-250,800,500);
        static PetFeeding Enter(bool left)
        {
            var task=new PetFeeding(); task.Begin(); task.Place(new Vector2(left?-140:140,-240),Area);
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
            task.Place(Vector2.zero,Area);
            Check(task.Stage==FeedingStage.Investigating && !task.Target.HasValue,"Investigation bypassed");
            Check(Vector2.Distance(task.ChooseInspectionPoint(Vector2.left*100,new BackgroundSurfaces(Area)),task.Food)>35,"Inspection targets food directly");
            task.Cancel(); Check(!task.Active,"Placement cancellation failed");
            foreach(bool left in new[]{false,true})
            {
                task=Enter(left);
                for(int i=0;i<100;i++) task.Advance(.1f,task.Den,task.Den,false,Area,true,Area);
                Check(task.Stage==FeedingStage.Entering,"Head-only clearance hid visible tail");
                var outside=new Rect(task.Den-Vector2.one*30,Vector2.one*60);
                task.Advance(.1f,task.Den,task.Den,false,Area,true,outside);
                Check(task.Frozen && !task.FoodVisible,"Fully outside pet did not eat");
                for(int i=0;i<100;i++) task.Advance(0,task.Den,task.Den,false,Area,true,outside);
                Check(task.Stage==FeedingStage.Eating,"Pause advanced eating");
                for(int i=0;i<31;i++) task.Advance(.1f,task.Den,task.Den,false,Area,true,outside);
                Check(task.Stage==FeedingStage.Emerging && !task.Frozen,"Return did not start");
                task.Advance(.1f,task.Entrance,task.Entrance,false,Area,true,outside);
                Check(task.Active,"Return closed over offscreen tail");
                task.Advance(.1f,task.Entrance,task.Entrance,false,Area,true,new Rect(task.Entrance-Vector2.one*30,Vector2.one*60));
                Check(!task.Active,"Return did not complete");
                task=Enter(left); task.RequestReturn();
                Check(task.Stage==FeedingStage.Emerging && task.Target==task.Entrance,"Cancel outside did not request walking return");
                task.Advance(.1f,task.Den,task.Den,false,new Rect(0,0,900,700));
                Check(!task.Active,"Display rebuild retained obsolete route task");
                task=Enter(left);
                for(int i=0;i<610 && task.Stage==FeedingStage.Entering;i++) task.Advance(.1f,task.Entrance,task.Entrance,false,Area,true,Area);
                Check(task.Stage==FeedingStage.Emerging,"Blocked entry never returns");
            }
            var desktop=new FrameSurfaces(Area);
            var route=new DenRouteSurfaces(desktop,Area); var other=new DenRouteSurfaces(desktop,Area);
            var point=new Vector2(500,-230);
            Check(route.Constrain(point,8,out _)==desktop.Constrain(point,8,out _),"Inactive route changes movement");
            route.Open(false,520);
            Check(route.Constrain(point,8,out _)==point,"Exit still blocked by desktop wall");
            Check(other.Constrain(point,8,out _).x<400,"Route leaked to another pet");
            route.Close(); Check(route.Constrain(point,8,out _).x<400,"Closed route remains open");
            foreach(bool left in new[]{false,true})
            foreach(float length in new[]{80f,160f,300f})
            {
                var sized=new PetFeeding(); sized.Begin(); sized.Place(Vector2.zero,Area,length);
                Check(sized.DenDistance>=520 && sized.DenDistance>=length*3,"Den too close for full pet length");
                var source=new DesktopSurfaces(Area) {Mode=ClimbingMode.EdgesAndBackgrounds};
                var extension=new DenRouteSurfaces(source,Area); extension.Open(left,sized.DenDistance);
                var target=new Vector2(left?Area.xMin-sized.DenDistance:Area.xMax+sized.DenDistance,Area.yMin+14);
                Check(extension.Constrain(target,8,out _)==target,"Den outside permitted route");
                Check(extension.Nearest(target,out var normal)==target && normal==Vector2.zero,"Background route forced floor posture");
                source.Mode=ClimbingMode.EdgesOnly;
                Check(!extension.SupportsFreeClimbing,"Mode change ignored while outside");
                extension.Nearest(target,out normal); Check(normal==Vector2.up,"Edges-only route lost floor support");
                extension.Close(); Check(extension.Constrain(target,8,out _)==source.Constrain(target,8,out _),"Mode/bounds not restored");
            }
            var invalid=new PetFeeding(); invalid.Begin();
            Check(!invalid.Place(Vector2.zero,Area,float.NaN) && !invalid.Place(Vector2.zero,Area,-1),"Invalid pet length accepted");
            Debug.Log("FEEDING_PROTOTYPE_EDGE_CASES_PASSED");
        }
        public static void Motion(ReferenceContext context)
        {
            foreach(bool backgrounds in new[]{false,true})
            foreach(bool jumps in new[]{false,true})
            foreach(bool left in new[]{false,true})
            foreach(bool cancel in new[]{false,true})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                IPetSurfaces desktop=backgrounds?(IPetSurfaces)new BackgroundSurfaces(Area):new FrameSurfaces(Area);
                var surfaces=new DenRouteSurfaces(desktop,Area);
                var motion=new PetMotion(pet,surfaces,31) {Roam=false,AutonomousJumps=jumps};
                int launches=0; motion.JumpLaunched+=(p,v,power)=> { launches++; Check(!surfaces.Active,"Den route launched a jump"); };
                var task=new PetFeeding(); task.Begin();
                var food=desktop.Nearest(new Vector2(left?-140:140,-210),out var normal)+normal*10;
                task.Place(food,Area,motion.HeadToTailLength); motion.ExploreClick(task.ChooseInspectionPoint(motion.Position,desktop));
                bool ate=false,entered=false,returnedHeadFirst=false; int steps=0;
                while(task.Active && steps++<8000)
                {
                    task.Advance(.025f,motion.Position,motion.MouthPosition,false,Area,motion.State==PetMotionState.Crawling,motion.OccupiedBounds);
                    if(task.UsingRoute && !surfaces.Active) { surfaces.Open(task.ExitLeft,task.DenDistance); entered=true; }
                    if(!task.UsingRoute && surfaces.Active)
                    {
                        Check(motion.OccupiedBounds.xMin>=Area.xMin && motion.OccupiedBounds.xMax<=Area.xMax,"Route closed with body outside");
                        surfaces.Close();
                    }
                    motion.Behavior.TaskTarget=task.Target;
                    motion.AutonomousJumps=jumps && !task.UsingRoute;
                    if(task.Stage==FeedingStage.Eating)
                    {
                        ate=true; var bounds=motion.OccupiedBounds;
                        Check(task.ExitLeft?bounds.xMax<Area.xMin:bounds.xMin>Area.xMax,"Eating before full tail clearance");
                        if(cancel) { task.RequestReturn(); motion.Behavior.TaskTarget=task.Target; }
                    }
                    var before=motion.Position;
                    if(!task.Frozen) motion.Advance(.025f);
                    if(surfaces.Active)
                    {
                        Check(surfaces.SupportsFreeClimbing==desktop.SupportsFreeClimbing,"Feeding changed climbing mode");
                        Check(Vector2.Distance(before,motion.Position)<25,"Den movement teleported");
                    }
                    if(task.Stage==FeedingStage.Emerging && Area.Contains(motion.Position))
                        returnedHeadFirst |= (motion.MouthPosition.x-motion.Position.x)*(task.ExitLeft?1:-1)>0;
                    pet.Draw();
                }
                Check(jumps || launches==0,"Disabled propulsion ignored");
                Check(entered && ate && returnedHeadFirst && !task.Active,"Motion failed: backgrounds="+backgrounds+" jumps="+jumps+" left="+left+" position="+motion.Position+" stage="+task.Stage);
            }
            Debug.Log("FEEDING_REAL_MOTION_RETURN_PASSED");
        }
    }
}
