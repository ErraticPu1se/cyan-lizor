using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        SurfaceSupport? poleGrip,windowFloor;
        public bool OnWindowFloor => windowFloor.HasValue;
        void UpdateWindowFloor()
        {
            if(!windowFloor.HasValue)
            {
                var support=surfaces.SupportAt(Position);
                if(State==PetMotionState.Crawling && support.Kind==SurfaceKind.Floor && support.WindowId!=0 && Vector2.Distance(Position,support.Point)<30) windowFloor=support;
                else return;
            }
            if(State!=PetMotionState.Crawling || !(surfaces is IWindowFloors floors)
                || !floors.TryFloor(Position,windowFloor.Value.WindowId,out var floor) || Mathf.Abs(Position.y-floor.Point.y)>40)
            { windowFloor=null; if(State==PetMotionState.Crawling) ReleaseSupport(); return; }
            windowFloor=floor;
        }
        Vector2 GroundSupport(Vector2 point,out Vector2 normal)
        {
            if(windowFloor.HasValue && surfaces is IWindowFloors floors && floors.TryFloor(point,windowFloor.Value.WindowId,out var floor))
            { normal=Vector2.up; return floor.Point; }
            return poleGrip.HasValue?PoleSupport(point,out normal):surfaces.Nearest(point,out normal);
        }
        float polePoseBlend, depthBeforePole;
        bool restoringPoleRotation;
        public bool OnPole => poleGrip.HasValue;
        void UpdatePoleGrip()
        {
            if(windowFloor.HasValue || State!=PetMotionState.Crawling || !capabilities.ClimbPoles) { poleGrip=null; return; }
            var candidate=poleGrip??surfaces.SupportAt(Position);
            if(candidate.Kind!=SurfaceKind.Pole || !(surfaces is IPoleSupports poles)
                || !poles.TryPole(Position,candidate.WindowId,candidate.Segment,out var current)
                || Vector2.Distance(Position,current.Point)>35) { poleGrip=null; return; }
            // Crossing a rail by a pixel must not flip the whole body to its other side.
            var normal=poleGrip.HasValue && poleGrip.Value.Segment==current.Segment?poleGrip.Value.Normal:current.Normal;
            poleGrip=new SurfaceSupport(current.Kind,current.Point,normal,current.WindowId,current.Segment);
        }
        Vector2 PoleSupport(Vector2 point,out Vector2 normal)
        {
            var grip=poleGrip.Value;
            if(surfaces is IPoleSupports poles && poles.TryPole(point,grip.WindowId,grip.Segment,out var support))
            { normal=support.Segment==grip.Segment?grip.Normal:support.Normal; return support.Point; }
            return surfaces.Nearest(point,out normal);
        }
        void UpdatePoleRotation()
        {
            bool active=poleGrip.HasValue;
            if(active && !restoringPoleRotation)
            { depthBeforePole=(float)Get(graphics,"depthRotation"); restoringPoleRotation=true; }
            if(!restoringPoleRotation) return;
            polePoseBlend=Mathf.MoveTowards(polePoseBlend,active?1:0,.06f);
            float rotation=0;
            for(int i=0;i<4;i++)
            {
                var shoulder=BodyPosition(i<2?0:2); var other=BodyPosition(1);
                float side=(float)Call(pet.Context.Type("RWCustom.Custom"),"DistanceToLine",Get(feet[i],"pos"),shoulder,other)*(i<2?-1:1);
                if(Mathf.Abs(side)>10) rotation+=Mathf.Sign(side);
            }
            // A desktop rail has zero width: all grips can lie on one side of the body.
            // Keep the spine facing out; use the game's limb-side signal only for a small walking lean.
            float lean=Mathf.Clamp(rotation,-1,1)*.12f*Mathf.Clamp01(Behavior.RunSpeed/.5f);
            float target=Mathf.Lerp(depthBeforePole,lean,polePoseBlend);
            float current=(float)Get(graphics,"depthRotation");
            Set(graphics,"lastDepthRotation",current);
            float next=Mathf.Lerp(current,target,.1f);
            if(!active && polePoseBlend==0 && Mathf.Abs(next-depthBeforePole)<.001f)
            { next=depthBeforePole; restoringPoleRotation=false; }
            Set(graphics,"depthRotation",next);
        }
    }
}
