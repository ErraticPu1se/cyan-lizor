using System;
using System.IO;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class ClimbingModeChecks
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var bounds = new Rect(-500,-300,1000,600);
            var window = new DesktopWindow(1,new Rect(-100,-100,200,200));
            var edges = new DesktopSurfaces(bounds) { Mode = ClimbingMode.EdgesOnly };
            var free = new DesktopSurfaces(bounds) { Mode = ClimbingMode.EdgesAndBackgrounds };
            edges.UpdateWindows(new[] { window }); free.UpdateWindows(new[] { window });
            Check(!edges.SupportsFreeClimbing && free.SupportsFreeClimbing,"Modes share free climbing capability");
            Check(edges.WindowAtSurface(Vector2.zero)==0 && free.WindowAtSurface(Vector2.zero)==1,"Interior support ignored mode");
            Check(edges.Nearest(Vector2.zero,out _) != Vector2.zero && free.Nearest(Vector2.zero,out _)==Vector2.zero,"Edges mode supports empty space");
            var start = new Vector2(100,0);
            var step = edges.Approach(start,new Vector2(-100,0),22);
            Check(step.x==100 && Mathf.Abs(step.y)==22,"Edge route cut through window interior");
            Check(edges.Nearest(new Vector2(490,0),out _).x==500,"Desktop border disappeared");
            var moved = new DesktopWindow(1,new Rect(-20,-60,200,200));
            Check(edges.UpdateWindows(new[] {moved},new Vector2(86,0),true)==new Vector2(80,40),"Edges mode lost moving window grip");
            edges.UpdateWindows(new[] {window});
            var resized = new DesktopWindow(1,new Rect(-100,-100,300,240));
            Check(edges.UpdateWindows(new[] {resized},new Vector2(86,0),true)==new Vector2(100,20),"Edges mode lost resized window grip");
            edges.UpdateWindows(new DesktopWindow[0],new Vector2(186,20),true);
            Check(edges.SupportLost,"Closed edge retained support");
            var cover = new DesktopWindow(2,new Rect(-150,-150,300,300));
            edges.UpdateWindows(new[] {cover,window});
            Check(edges.WindowAtEdge(new Vector2(100,0),out _,out _)==0,"Hidden window edge is grippable");
            bool rejected=false;
            try { edges.Mode=(ClimbingMode)99; } catch(ArgumentOutOfRangeException) { rejected=true; }
            Check(rejected && edges.Mode==ClimbingMode.EdgesOnly,"Invalid mode corrupted surfaces");
            foreach(bool legacy in new[] {false,true})
            {
                var settings=JsonUtility.FromJson<DesktopSettings>("{\"schema\":1,\"windowEdges\":"+(legacy?"true":"false")+",\"id\":\"9320\",\"scale\":1.5}");
                settings.UpgradeClimbingMode(); settings.UpgradeClimbingMode();
                Check(settings.schema==2 && settings.climbingMode==(legacy?ClimbingMode.EdgesAndBackgrounds:ClimbingMode.EdgesOnly) && settings.scale==1.5f && settings.id=="9320","Legacy settings migration lost values");
            }
            var menu=new DesktopSettings(); var separate=new DesktopSettings();
            menu.ApplyMenu(DesktopMenuAction.EdgesOnly); menu.ApplyMenu(DesktopMenuAction.EdgesOnly);
            Check(menu.climbingMode==ClimbingMode.EdgesOnly && separate.climbingMode==ClimbingMode.EdgesAndBackgrounds,"Menu choices toggle or leak to another pet");
            menu.ApplyMenu(DesktopMenuAction.EdgesAndBackgrounds);
            Check(menu.climbingMode==ClimbingMode.EdgesAndBackgrounds,"Menu cannot restore free climbing");
            menu.climbingMode=(ClimbingMode)99; rejected=false;
            try { menu.UpgradeClimbingMode(); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Invalid serialized climbing mode accepted");
            edges.UpdateWindows(new[] {window});
            Check(PetJumpPlanner.TryPlanToTarget(new Vector2(-250,-286),new Vector2(-100,0),edges,LizardSpecies.Find("cyan").Movement,out var jump),"No jump route to window edge");
            Check(Vector2.Distance(jump.Landing,edges.Nearest(jump.Landing,out _))<=10,"Jump planned an unsupported landing");
            Check(!PetJumpPlanner.TryPlanToTarget(Vector2.zero,start,edges,new MovementCapabilities(false,2,14,50),out _),"Climbing mode overrode species capabilities");
            using(var pet=context.CreatePet())
            using(var second=context.CreatePet())
            {
                pet.Generate("9320"); second.Generate("9320");
                var surfaces=new DesktopSurfaces(bounds) {Mode=ClimbingMode.EdgesAndBackgrounds};
                surfaces.UpdateWindows(new[] {window});
                var motion=new PetMotion(pet,surfaces,5) {Roam=false,AutonomousJumps=false};
                var otherMotion=new PetMotion(second,free,5) {Roam=false};
                var position=motion.Position; var otherPosition=otherMotion.Position; var identity=pet.PetId;
                surfaces.Mode=ClimbingMode.EdgesOnly; motion.RefreshSurfaceSupport();
                Check(motion.State==PetMotionState.Airborne && motion.Position==position && pet.PetId==identity,"Mode switch teleported or regenerated unsupported pet");
                Check(otherMotion.Position==otherPosition && otherMotion.State==PetMotionState.Crawling && free.SupportsFreeClimbing,"Mode switch affected another pet");
                for(int i=0;i<5;i++) motion.Advance(.025f);
                Check(motion.Position.y<position.y && motion.State==PetMotionState.Airborne,"Unsupported pet did not fall");
                motion.BeginGrab(motion.Position);
                surfaces.Mode=ClimbingMode.EdgesAndBackgrounds; motion.RefreshSurfaceSupport();
                Check(motion.State==PetMotionState.Grabbed,"Mode switch released cursor grab");
                motion.EndGrab(); surfaces.Mode=ClimbingMode.EdgesOnly; motion.RefreshSurfaceSupport();
                for(int i=0;i<160;i++) motion.Advance(.025f);
                Check(motion.State==PetMotionState.Crawling && !float.IsNaN(motion.Position.y),"Falling pet failed to land after mode switch");
                // Planned propulsion may land on an edge; ordinary falling catches are a separate feature.
                motion.ShiftWithSurface(new Vector2(-250,-286)-motion.Position);
                for(int i=0;i<8;i++) motion.Advance(.025f);
                motion.MoveTo(jump.Landing);
                bool flew=false, landedOnWindow=false;
                for(int i=0;i<180;i++)
                {
                    motion.Advance(.025f);
                    flew |= motion.State==PetMotionState.Airborne;
                    if(flew && motion.State==PetMotionState.Crawling) { landedOnWindow=surfaces.WindowAtEdge(motion.Position,out _,out _)!=0; break; }
                }
                Check(landedOnWindow,"Planned jump did not grip its window edge");
                var edgePosition=motion.Position;
                surfaces.Mode=ClimbingMode.EdgesAndBackgrounds; motion.RefreshSurfaceSupport();
                surfaces.Mode=ClimbingMode.EdgesOnly; motion.RefreshSurfaceSupport();
                Check(motion.State==PetMotionState.Crawling && motion.Position==edgePosition,"Mode switch dropped or moved valid edge grip");
            }
            Debug.Log("CLIMBING_MODES_MIGRATION_SURFACES_MULTIPET_PASSED");
        }
    }
}
